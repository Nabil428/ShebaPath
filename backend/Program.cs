using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var pgHost = Environment.GetEnvironmentVariable("PGHOST")
    ?? throw new InvalidOperationException("PGHOST environment variable is required.");
var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
var pgUser = Environment.GetEnvironmentVariable("PGUSER")
    ?? throw new InvalidOperationException("PGUSER environment variable is required.");
var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD")
    ?? throw new InvalidOperationException("PGPASSWORD environment variable is required.");
var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE")
    ?? throw new InvalidOperationException("PGDATABASE environment variable is required.");

var connectionStringBuilder = new NpgsqlConnectionStringBuilder
{
    Host = pgHost,
    Port = int.Parse(pgPort),
    Username = pgUser,
    Password = pgPassword,
    Database = pgDatabase,
    SslMode = SslMode.Prefer
};

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString).Build());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limiting: max 10 requests per minute per client IP for login/register,
// to slow down brute-force / credential-stuffing attempts.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "bd_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Swagger is opt-in via env var so it isn't publicly exposed by default in
// production. Set ENABLE_SWAGGER=true on Render if you need it temporarily.
var swaggerEnabled = app.Environment.IsDevelopment()
    || string.Equals(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var apiBase = "/bd-services/api";

var health = app.MapGroup(apiBase);
health.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// ---------- Auth ----------
var auth = app.MapGroup($"{apiBase}/auth");

auth.MapPost("/register", async (RegisterRequest req, NpgsqlDataSource db, HttpContext http) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.FullName))
    {
        return Results.BadRequest(new { error = "Email, password and full name are required." });
    }
    if (req.Password.Length < 8)
    {
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });
    }

    var email = req.Email.Trim().ToLowerInvariant();
    var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

    await using var conn = await db.OpenConnectionAsync();
    try
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO bd_users (email, password_hash, full_name, phone) VALUES ($1, $2, $3, $4) RETURNING id, email, full_name, phone, created_at",
            conn);
        cmd.Parameters.AddWithValue(email);
        cmd.Parameters.AddWithValue(hash);
        cmd.Parameters.AddWithValue(req.FullName.Trim());
        cmd.Parameters.AddWithValue((object?)req.Phone ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return Results.Problem("Failed to create account.");
        }

        var user = ReadUser(reader);
        await SignInUser(http, user);
        return Results.Ok(user);
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        return Results.Conflict(new { error = "An account with this email already exists." });
    }
}).RequireRateLimiting("auth");

auth.MapPost("/login", async (LoginRequest req, NpgsqlDataSource db, HttpContext http) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new { error = "Email and password are required." });
    }

    var email = req.Email.Trim().ToLowerInvariant();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, email, full_name, phone, created_at, password_hash FROM bd_users WHERE email = $1",
        conn);
    cmd.Parameters.AddWithValue(email);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }

    var passwordHash = reader.GetString(5);
    if (!BCrypt.Net.BCrypt.Verify(req.Password, passwordHash))
    {
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }

    var user = ReadUser(reader);
    await SignInUser(http, user);
    return Results.Ok(user);
}).RequireRateLimiting("auth");

auth.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
});

auth.MapGet("/me", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        return Results.Json(new { error = "Not authenticated." }, statusCode: 401);
    }

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, email, full_name, phone, created_at FROM bd_users WHERE id = $1", conn);
    cmd.Parameters.AddWithValue(userId);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.Json(new { error = "Not authenticated." }, statusCode: 401);
    }
    return Results.Ok(ReadUser(reader));
}).RequireAuthorization();

// ---------- Account ----------
app.MapPatch($"{apiBase}/account", async (UpdateAccountRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        return Results.Json(new { error = "Not authenticated." }, statusCode: 401);
    }
    if (string.IsNullOrWhiteSpace(req.FullName))
    {
        return Results.BadRequest(new { error = "Full name is required." });
    }

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE bd_users SET full_name = $1, phone = $2 WHERE id = $3 RETURNING id, email, full_name, phone, created_at",
        conn);
    cmd.Parameters.AddWithValue(req.FullName.Trim());
    cmd.Parameters.AddWithValue((object?)req.Phone ?? DBNull.Value);
    cmd.Parameters.AddWithValue(userId);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.Problem("Failed to update account.");
    }
    return Results.Ok(ReadUser(reader));
}).RequireAuthorization();

// ---------- Guides ----------
app.MapGet($"{apiBase}/guides", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, category, title, summary, fees, processing_time, office, published_at FROM bd_guides ORDER BY title", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            slug = reader.GetString(0),
            category = reader.GetString(1),
            title = reader.GetString(2),
            summary = reader.GetString(3),
            fees = reader.IsDBNull(4) ? null : reader.GetString(4),
            processingTime = reader.IsDBNull(5) ? null : reader.GetString(5),
            office = reader.IsDBNull(6) ? null : reader.GetString(6),
            publishedAt = reader.GetDateTime(7)
        });
    }
    return Results.Ok(results);
});

app.MapGet($"{apiBase}/guides/{{slug}}", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, category, title, summary, steps, requirements, fees, processing_time, office, published_at FROM bd_guides WHERE slug = $1", conn);
    cmd.Parameters.AddWithValue(slug);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.NotFound(new { error = "Guide not found." });
    }
    return Results.Ok(new
    {
        slug = reader.GetString(0),
        category = reader.GetString(1),
        title = reader.GetString(2),
        summary = reader.GetString(3),
        steps = JsonSerializer.Deserialize<List<string>>(reader.GetString(4)),
        requirements = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)),
        fees = reader.IsDBNull(6) ? null : reader.GetString(6),
        processingTime = reader.IsDBNull(7) ? null : reader.GetString(7),
        office = reader.IsDBNull(8) ? null : reader.GetString(8),
        publishedAt = reader.GetDateTime(9)
    });
});

// ---------- Blog ----------
app.MapGet($"{apiBase}/blog", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, title, excerpt, cover_image_url, published_at FROM bd_blog_posts ORDER BY published_at DESC", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            slug = reader.GetString(0),
            title = reader.GetString(1),
            excerpt = reader.GetString(2),
            coverImageUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
            publishedAt = reader.GetDateTime(4)
        });
    }
    return Results.Ok(results);
});

app.MapGet($"{apiBase}/blog/{{slug}}", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, title, excerpt, content, cover_image_url, published_at FROM bd_blog_posts WHERE slug = $1", conn);
    cmd.Parameters.AddWithValue(slug);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.NotFound(new { error = "Post not found." });
    }
    return Results.Ok(new
    {
        slug = reader.GetString(0),
        title = reader.GetString(1),
        excerpt = reader.GetString(2),
        content = reader.GetString(3),
        coverImageUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
        publishedAt = reader.GetDateTime(5)
    });
});

app.Run();

static UserResponse ReadUser(NpgsqlDataReader reader) => new(
    reader.GetInt32(0),
    reader.GetString(1),
    reader.GetString(2),
    reader.IsDBNull(3) ? null : reader.GetString(3),
    reader.GetDateTime(4)
);

static async Task SignInUser(HttpContext http, UserResponse user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Name, user.FullName)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}

record RegisterRequest(string Email, string Password, string FullName, string? Phone);
record LoginRequest(string Email, string Password);
record UpdateAccountRequest(string FullName, string? Phone);
record UserResponse(int Id, string Email, string FullName, string? Phone, DateTime CreatedAt);
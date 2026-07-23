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

// ---------- Helpers ----------
static bool IsAdmin(HttpContext http) =>
    http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue("is_admin") == "true";

static IResult Unauthorized() => Results.Json(new { error = "Not authenticated." }, statusCode: 401);
static IResult Forbidden() => Results.Json(new { error = "Admin access required." }, statusCode: 403);

static List<string> ParseStringArray(string? json) =>
    string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

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
            "INSERT INTO bd_users (email, password_hash, full_name, phone) VALUES ($1, $2, $3, $4) RETURNING id, email, full_name, phone, created_at, is_admin",
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
        "SELECT id, email, full_name, phone, created_at, is_admin, password_hash FROM bd_users WHERE email = $1",
        conn);
    cmd.Parameters.AddWithValue(email);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }

    var passwordHash = reader.GetString(6);
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
        return Unauthorized();
    }

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, email, full_name, phone, created_at, is_admin FROM bd_users WHERE id = $1", conn);
    cmd.Parameters.AddWithValue(userId);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return Unauthorized();
    }
    return Results.Ok(ReadUser(reader));
}).RequireAuthorization();

// ---------- Account ----------
app.MapPatch($"{apiBase}/account", async (UpdateAccountRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        return Unauthorized();
    }
    if (string.IsNullOrWhiteSpace(req.FullName))
    {
        return Results.BadRequest(new { error = "Full name is required." });
    }

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE bd_users SET full_name = $1, phone = $2 WHERE id = $3 RETURNING id, email, full_name, phone, created_at, is_admin",
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

// ---------- Bookmarks (save guides to your account) ----------
var bookmarks = app.MapGroup($"{apiBase}/account/bookmarks").RequireAuthorization();

bookmarks.MapGet("/", async (HttpContext http, NpgsqlDataSource db) =>
{
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"SELECT g.slug, g.category, g.title, g.summary, g.fees, g.processing_time, g.office, g.published_at, g.last_verified
          FROM bd_bookmarks b JOIN bd_guides g ON g.slug = b.guide_slug
          WHERE b.user_id = $1 ORDER BY b.created_at DESC", conn);
    cmd.Parameters.AddWithValue(userId);
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
            publishedAt = reader.GetDateTime(7),
            lastVerified = reader.GetDateTime(8)
        });
    }
    return Results.Ok(results);
});

bookmarks.MapPost("/{slug}", async (string slug, HttpContext http, NpgsqlDataSource db) =>
{
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "INSERT INTO bd_bookmarks (user_id, guide_slug) VALUES ($1, $2) ON CONFLICT DO NOTHING", conn);
    cmd.Parameters.AddWithValue(userId);
    cmd.Parameters.AddWithValue(slug);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { saved = true });
});

bookmarks.MapDelete("/{slug}", async (string slug, HttpContext http, NpgsqlDataSource db) =>
{
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "DELETE FROM bd_bookmarks WHERE user_id = $1 AND guide_slug = $2", conn);
    cmd.Parameters.AddWithValue(userId);
    cmd.Parameters.AddWithValue(slug);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { saved = false });
});

// ---------- Guides (public) ----------
app.MapGet($"{apiBase}/guides", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, category, title, summary, fees, processing_time, office, published_at, last_verified, tags FROM bd_guides ORDER BY title", conn);
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
            publishedAt = reader.GetDateTime(7),
            lastVerified = reader.GetDateTime(8),
            tags = ParseStringArray(reader.IsDBNull(9) ? null : reader.GetString(9))
        });
    }
    return Results.Ok(results);
});

app.MapGet($"{apiBase}/guides/{{slug}}", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, category, title, summary, steps, requirements, fees, processing_time, office, published_at, last_verified, tags FROM bd_guides WHERE slug = $1", conn);
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
        steps = ParseStringArray(reader.GetString(4)),
        requirements = ParseStringArray(reader.GetString(5)),
        fees = reader.IsDBNull(6) ? null : reader.GetString(6),
        processingTime = reader.IsDBNull(7) ? null : reader.GetString(7),
        office = reader.IsDBNull(8) ? null : reader.GetString(8),
        publishedAt = reader.GetDateTime(9),
        lastVerified = reader.GetDateTime(10),
        tags = ParseStringArray(reader.IsDBNull(11) ? null : reader.GetString(11))
    });
});

// ---------- Blog (public) ----------
app.MapGet($"{apiBase}/blog", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, title, excerpt, cover_image_url, published_at, tags FROM bd_blog_posts ORDER BY published_at DESC", conn);
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
            publishedAt = reader.GetDateTime(4),
            tags = ParseStringArray(reader.IsDBNull(5) ? null : reader.GetString(5))
        });
    }
    return Results.Ok(results);
});

app.MapGet($"{apiBase}/blog/{{slug}}", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT slug, title, excerpt, content, cover_image_url, published_at, tags FROM bd_blog_posts WHERE slug = $1", conn);
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
        publishedAt = reader.GetDateTime(5),
        tags = ParseStringArray(reader.IsDBNull(6) ? null : reader.GetString(6))
    });
});

// ---------- Admin: Guides CRUD ----------
var adminGuides = app.MapGroup($"{apiBase}/admin/guides").RequireAuthorization();

adminGuides.MapPost("/", async (AdminGuideRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    try
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO bd_guides (slug, category, title, summary, steps, requirements, fees, processing_time, office, tags, last_verified)
              VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, now())", conn);
        cmd.Parameters.AddWithValue(req.Slug.Trim());
        cmd.Parameters.AddWithValue(req.Category.Trim());
        cmd.Parameters.AddWithValue(req.Title.Trim());
        cmd.Parameters.AddWithValue(req.Summary.Trim());
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Steps));
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Requirements));
        cmd.Parameters.AddWithValue((object?)req.Fees ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.ProcessingTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.Office ?? DBNull.Value);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Tags ?? new List<string>()));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { success = true });
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        return Results.Conflict(new { error = "A guide with this slug already exists." });
    }
});

adminGuides.MapPut("/{slug}", async (string slug, AdminGuideRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"UPDATE bd_guides SET category=$1, title=$2, summary=$3, steps=$4, requirements=$5,
          fees=$6, processing_time=$7, office=$8, tags=$9, last_verified=now() WHERE slug=$10", conn);
    cmd.Parameters.AddWithValue(req.Category.Trim());
    cmd.Parameters.AddWithValue(req.Title.Trim());
    cmd.Parameters.AddWithValue(req.Summary.Trim());
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Steps));
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Requirements));
    cmd.Parameters.AddWithValue((object?)req.Fees ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.ProcessingTime ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.Office ?? DBNull.Value);
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Tags ?? new List<string>()));
    cmd.Parameters.AddWithValue(slug);
    var affected = await cmd.ExecuteNonQueryAsync();
    return affected == 0 ? Results.NotFound(new { error = "Guide not found." }) : Results.Ok(new { success = true });
});

adminGuides.MapDelete("/{slug}", async (string slug, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM bd_guides WHERE slug = $1", conn);
    cmd.Parameters.AddWithValue(slug);
    var affected = await cmd.ExecuteNonQueryAsync();
    return affected == 0 ? Results.NotFound(new { error = "Guide not found." }) : Results.Ok(new { success = true });
});

// ---------- Admin: Blog CRUD ----------
var adminBlog = app.MapGroup($"{apiBase}/admin/blog").RequireAuthorization();
adminBlog.MapPost("/", async (AdminBlogRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    try
    {
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO bd_blog_posts (slug, title, excerpt, content, cover_image_url, tags)
              VALUES ($1, $2, $3, $4, $5, $6)", conn);
        cmd.Parameters.AddWithValue(req.Slug.Trim());
        cmd.Parameters.AddWithValue(req.Title.Trim());
        cmd.Parameters.AddWithValue(req.Excerpt.Trim());
        cmd.Parameters.AddWithValue(req.Content);
        cmd.Parameters.AddWithValue((object?)req.CoverImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Tags ?? new List<string>()));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { success = true });
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        return Results.Conflict(new { error = "A post with this slug already exists." });
    }
});

adminBlog.MapPut("/{slug}", async (string slug, AdminBlogRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"UPDATE bd_blog_posts SET title=$1, excerpt=$2, content=$3, cover_image_url=$4, tags=$5 WHERE slug=$6", conn);
    cmd.Parameters.AddWithValue(req.Title.Trim());
    cmd.Parameters.AddWithValue(req.Excerpt.Trim());
    cmd.Parameters.AddWithValue(req.Content);
    cmd.Parameters.AddWithValue((object?)req.CoverImageUrl ?? DBNull.Value);
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Tags ?? new List<string>()));
    cmd.Parameters.AddWithValue(slug);
    var affected = await cmd.ExecuteNonQueryAsync();
    return affected == 0 ? Results.NotFound(new { error = "Post not found." }) : Results.Ok(new { success = true });
});

adminBlog.MapDelete("/{slug}", async (string slug, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM bd_blog_posts WHERE slug = $1", conn);
    cmd.Parameters.AddWithValue(slug);
    var affected = await cmd.ExecuteNonQueryAsync();
    return affected == 0 ? Results.NotFound(new { error = "Post not found." }) : Results.Ok(new { success = true });
});

app.MapGet($"{apiBase}/admin/categories", async (NpgsqlDataSource db) =>
{
    var categories = new List<object>();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        SELECT id,name,slug,description
        FROM categories
        ORDER BY name
    ", conn);

    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        categories.Add(new
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Slug = reader.GetString(2),
            Description = reader.IsDBNull(3) ? "" : reader.GetString(3)
        });
    }

    return Results.Ok(categories);

}).RequireAuthorization();

app.MapPost($"{apiBase}/admin/categories", async (
    HttpContext http,
    CategoryDto dto,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        INSERT INTO categories
        (name,slug,description)

        VALUES

        (@name,@slug,@description)

        RETURNING id;
    ", conn);

    cmd.Parameters.AddWithValue("name", dto.Name);
    cmd.Parameters.AddWithValue("slug", dto.Slug);
    cmd.Parameters.AddWithValue("description",
        (object?)dto.Description ?? DBNull.Value);

    var id = (int)(await cmd.ExecuteScalarAsync())!;

    return Results.Ok(new
    {
        id,
        message = "Category created successfully"
    });

}).RequireAuthorization();

app.MapPut($"{apiBase}/admin/categories/{{id:int}}", async (
    int id,
    HttpContext http,
    CategoryDto dto,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        UPDATE categories

        SET
            name=@name,
            slug=@slug,
            description=@description

        WHERE id=@id
    ", conn);

    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("name", dto.Name);
    cmd.Parameters.AddWithValue("slug", dto.Slug);
    cmd.Parameters.AddWithValue("description",
        (object?)dto.Description ?? DBNull.Value);

    var rows = await cmd.ExecuteNonQueryAsync();

    if (rows == 0)
        return Results.NotFound();

    return Results.Ok();
})
.RequireAuthorization();

app.MapDelete($"{apiBase}/admin/categories/{{id:int}}", async (
    int id,
    HttpContext http,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(
        "DELETE FROM categories WHERE id=@id", conn);

    cmd.Parameters.AddWithValue("id", id);

    var rows = await cmd.ExecuteNonQueryAsync();

    if (rows == 0)
        return Results.NotFound();

    return Results.Ok();
})
.RequireAuthorization();

app.MapGet($"{apiBase}/admin/tags", async (NpgsqlDataSource db) =>
{
    List<object> tags = [];

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        SELECT id,name,slug
        FROM tags
        ORDER BY name
    ", conn);

    await using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        tags.Add(new
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Slug = reader.GetString(2)
        });
    }

    return Results.Ok(tags);

})
.RequireAuthorization();

app.MapPost($"{apiBase}/admin/tags", async (
    HttpContext http,
    TagDto dto,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        INSERT INTO tags(name,slug)

        VALUES(@name,@slug)

        RETURNING id;
    ", conn);

    cmd.Parameters.AddWithValue("name", dto.Name);
    cmd.Parameters.AddWithValue("slug", dto.Slug);

    var id = (int)(await cmd.ExecuteScalarAsync())!;

    return Results.Ok(new
    {
        id
    });

})
.RequireAuthorization();

app.MapPut($"{apiBase}/admin/tags/{{id:int}}", async (
    int id,
    HttpContext http,
    TagDto dto,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(@"
        UPDATE tags

        SET
            name=@name,
            slug=@slug

        WHERE id=@id
    ", conn);

    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("name", dto.Name);
    cmd.Parameters.AddWithValue("slug", dto.Slug);

    await cmd.ExecuteNonQueryAsync();

    return Results.Ok();

})
.RequireAuthorization();

app.MapDelete($"{apiBase}/admin/tags/{{id:int}}", async (
    int id,
    HttpContext http,
    NpgsqlDataSource db) =>
{
    if (!IsAdmin(http))
        return Forbidden();

    await using var conn = await db.OpenConnectionAsync();

    var cmd = new NpgsqlCommand(
        "DELETE FROM tags WHERE id=@id", conn);

    cmd.Parameters.AddWithValue("id", id);

    await cmd.ExecuteNonQueryAsync();

    return Results.Ok();

})
.RequireAuthorization();
app.Run();

static UserResponse ReadUser(NpgsqlDataReader reader) => new(
    reader.GetInt32(0),
    reader.GetString(1),
    reader.GetString(2),
    reader.IsDBNull(3) ? null : reader.GetString(3),
    reader.GetDateTime(4),
    reader.GetBoolean(5)
);

static async Task SignInUser(HttpContext http, UserResponse user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Name, user.FullName),
        new("is_admin", user.IsAdmin ? "true" : "false")
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}

record RegisterRequest(string Email, string Password, string FullName, string? Phone);
record LoginRequest(string Email, string Password);
record UpdateAccountRequest(string FullName, string? Phone);
record UserResponse(int Id, string Email, string FullName, string? Phone, DateTime CreatedAt, bool IsAdmin);
record AdminGuideRequest(
    string Slug, string Category, string Title, string Summary,
    List<string> Steps, List<string> Requirements,
    string? Fees, string? ProcessingTime, string? Office, List<string>? Tags);
record AdminBlogRequest(
    string Slug, string Title, string Excerpt, string Content,
    string? CoverImageUrl, List<string>? Tags);
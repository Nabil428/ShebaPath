using System.Security.Claims;
using System.Text.Json;
using System.Net.Http.Json;
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
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

var swaggerEnabled = app.Environment.IsDevelopment()
    || string.Equals(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var apiBase = "/bd-services/api";

app.MapGet($"{apiBase}/healthz", () => Results.Ok(new { status = "ok" }));

// ---------- Helpers ----------
static bool IsAdmin(HttpContext http) =>
    http.User.Identity?.IsAuthenticated == true && http.User.FindFirstValue("is_admin") == "true";

static IResult Unauthorized() => Results.Json(new { error = "Not authenticated." }, statusCode: 401);
static IResult Forbidden() => Results.Json(new { error = "Admin access required." }, statusCode: 403);

static List<string> ParseStringArray(string? json) =>
    string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

static string Slugify(string text) =>
    string.Join("-", text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

// Resolves a list of tag names to tag IDs, creating any tags that don't exist yet.
static async Task<List<int>> ResolveTagIdsAsync(NpgsqlConnection conn, List<string> tagNames)
{
    var ids = new List<int>();
    foreach (var raw in tagNames.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct())
    {
        var slug = Slugify(raw);
        await using var upsert = new NpgsqlCommand(
            "INSERT INTO tags (name, slug) VALUES ($1, $2) ON CONFLICT (slug) DO NOTHING", conn);
        upsert.Parameters.AddWithValue(raw);
        upsert.Parameters.AddWithValue(slug);
        await upsert.ExecuteNonQueryAsync();

        await using var select = new NpgsqlCommand("SELECT id FROM tags WHERE slug = $1", conn);
        select.Parameters.AddWithValue(slug);
        var id = (int)(await select.ExecuteScalarAsync())!;
        ids.Add(id);
    }
    return ids;
}

static async Task SetGuideTagsAsync(NpgsqlConnection conn, int guideId, List<int> tagIds)
{
    await using var del = new NpgsqlCommand("DELETE FROM guide_tags WHERE guide_id = $1", conn);
    del.Parameters.AddWithValue(guideId);
    await del.ExecuteNonQueryAsync();

    foreach (var tagId in tagIds)
    {
        await using var ins = new NpgsqlCommand(
            "INSERT INTO guide_tags (guide_id, tag_id) VALUES ($1, $2) ON CONFLICT DO NOTHING", conn);
        ins.Parameters.AddWithValue(guideId);
        ins.Parameters.AddWithValue(tagId);
        await ins.ExecuteNonQueryAsync();
    }
}

// Shared SELECT list for guide summaries — joins category name and aggregates tag names.
const string GuideSummaryColumns = @"
    g.id, g.slug, c.name AS category, g.title, g.summary, g.fees, g.processing_time,
    g.office, g.published_at, g.last_verified,
    COALESCE((SELECT array_agg(t.name ORDER BY t.name) FROM guide_tags gt JOIN tags t ON t.id = gt.tag_id WHERE gt.guide_id = g.id), ARRAY[]::text[]) AS tag_names";

static object ReadGuideSummary(NpgsqlDataReader reader) => new
{
    id = reader.GetInt32(0),
    slug = reader.GetString(1),
    category = reader.GetString(2),
    title = reader.GetString(3),
    summary = reader.GetString(4),
    fees = reader.IsDBNull(5) ? null : reader.GetString(5),
    processingTime = reader.IsDBNull(6) ? null : reader.GetString(6),
    office = reader.IsDBNull(7) ? null : reader.GetString(7),
    publishedAt = reader.GetDateTime(8),
    lastVerified = reader.GetDateTime(9),
    tags = reader.GetFieldValue<string[]>(10)
};

// ---------- Auth ----------
var auth = app.MapGroup($"{apiBase}/auth");

auth.MapPost("/register", async (RegisterRequest req, NpgsqlDataSource db, HttpContext http) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.FullName))
        return Results.BadRequest(new { error = "Email, password and full name are required." });
    if (req.Password.Length < 8)
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });

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
        if (!await reader.ReadAsync()) return Results.Problem("Failed to create account.");

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
        return Results.BadRequest(new { error = "Email and password are required." });

    var email = req.Email.Trim().ToLowerInvariant();

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, email, full_name, phone, created_at, is_admin, password_hash FROM bd_users WHERE email = $1", conn);
    cmd.Parameters.AddWithValue(email);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);

    var passwordHash = reader.GetString(6);
    if (!BCrypt.Net.BCrypt.Verify(req.Password, passwordHash))
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);

    var user = ReadUser(reader);
    await SignInUser(http, user);
    return Results.Ok(user);
}).RequireRateLimiting("auth");

auth.MapPost("/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
});

auth.MapPost("/forgot-password", async (ForgotPasswordRequest req, NpgsqlDataSource db, IHttpClientFactory httpFactory) =>
{
    var email = req.Email.Trim().ToLowerInvariant();
    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE bd_users SET reset_token = $1, reset_token_expires_at = now() + interval '1 hour' WHERE email = $2 RETURNING full_name",
        conn);
    cmd.Parameters.AddWithValue(token);
    cmd.Parameters.AddWithValue(email);
    await using var reader = await cmd.ExecuteReaderAsync();
    var found = await reader.ReadAsync();
    await reader.CloseAsync();

    // Always return success, even if the email wasn't found — this avoids
    // leaking which emails have accounts (a common security best practice).
    if (found)
    {
        var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY");
        var siteUrl = Environment.GetEnvironmentVariable("SITE_URL") ?? "https://shebapath.vercel.app/bd-services";
        if (!string.IsNullOrEmpty(resendApiKey))
        {
            var resetLink = $"{siteUrl}/reset-password?token={token}";
            var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resendApiKey);
            var emailPayload = new
            {
                from = Environment.GetEnvironmentVariable("RESEND_FROM_EMAIL") ?? "ShebaPath <onboarding@resend.dev>",
                to = new[] { email },
                subject = "Reset your ShebaPath password",
                html = $"<p>Click the link below to reset your password. This link expires in 1 hour.</p><p><a href=\"{resetLink}\">{resetLink}</a></p><p>If you didn't request this, you can ignore this email.</p>"
            };
            try
            {
                await client.PostAsJsonAsync("https://api.resend.com/emails", emailPayload);
            }
            catch
            {
                // Don't fail the request just because the email send failed —
                // log server-side if you add logging later.
            }
        }
    }

    return Results.Ok(new { success = true });
}).RequireRateLimiting("auth");

auth.MapPost("/reset-password", async (ResetPasswordRequest req, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
        return Results.BadRequest(new { error = "Token and new password are required." });
    if (req.NewPassword.Length < 8)
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });

    await using var conn = await db.OpenConnectionAsync();
    await using var checkCmd = new NpgsqlCommand(
        "SELECT id FROM bd_users WHERE reset_token = $1 AND reset_token_expires_at > now()", conn);
    checkCmd.Parameters.AddWithValue(req.Token);
    var userId = await checkCmd.ExecuteScalarAsync();
    if (userId is null)
        return Results.BadRequest(new { error = "This reset link is invalid or has expired." });

    var hash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await using var updateCmd = new NpgsqlCommand(
        "UPDATE bd_users SET password_hash = $1, reset_token = NULL, reset_token_expires_at = NULL WHERE id = $2", conn);
    updateCmd.Parameters.AddWithValue(hash);
    updateCmd.Parameters.AddWithValue((int)userId);
    await updateCmd.ExecuteNonQueryAsync();

    return Results.Ok(new { success = true });
}).RequireRateLimiting("auth");

auth.MapGet("/me", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Unauthorized();

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, email, full_name, phone, created_at, is_admin FROM bd_users WHERE id = $1", conn);
    cmd.Parameters.AddWithValue(userId);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Unauthorized();
    return Results.Ok(ReadUser(reader));
}).RequireAuthorization();

// ---------- Account ----------
app.MapPatch($"{apiBase}/account", async (UpdateAccountRequest req, HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Unauthorized();
    if (string.IsNullOrWhiteSpace(req.FullName)) return Results.BadRequest(new { error = "Full name is required." });

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE bd_users SET full_name = $1, phone = $2 WHERE id = $3 RETURNING id, email, full_name, phone, created_at, is_admin", conn);
    cmd.Parameters.AddWithValue(req.FullName.Trim());
    cmd.Parameters.AddWithValue((object?)req.Phone ?? DBNull.Value);
    cmd.Parameters.AddWithValue(userId);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.Problem("Failed to update account.");
    return Results.Ok(ReadUser(reader));
}).RequireAuthorization();

app.MapDelete($"{apiBase}/account", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Unauthorized();

    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM bd_users WHERE id = $1", conn);
    cmd.Parameters.AddWithValue(userId);
    await cmd.ExecuteNonQueryAsync();

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
}).RequireAuthorization();

auth.MapPost("/forgot-password", async (ForgotPasswordRequest req, NpgsqlDataSource db) =>
{
    var email = req.Email.Trim().ToLowerInvariant();
    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE bd_users SET reset_token = $1, reset_token_expires = now() + interval '30 minutes' WHERE email = $2",
        conn);
    cmd.Parameters.AddWithValue(token);
    cmd.Parameters.AddWithValue(email);
    var affected = await cmd.ExecuteNonQueryAsync();

    // No email service is configured yet, so the reset link is logged here
    // instead of being emailed. Wire up a real email provider (e.g. Resend,
    // SendGrid) here before relying on this in production.
    if (affected > 0)
    {
        Console.WriteLine($"[password reset] {email} -> https://shebapath.vercel.app/bd-services/reset-password?token={token}");
    }

    // Always return the same generic response, whether or not the email
    // exists — this avoids leaking which emails are registered.
    return Results.Ok(new { message = "If that email is registered, a reset link has been generated." });
}).RequireRateLimiting("auth");

auth.MapPost("/reset-password", async (ResetPasswordRequest req, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword))
        return Results.BadRequest(new { error = "Token and new password are required." });
    if (req.NewPassword.Length < 8)
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });

    await using var conn = await db.OpenConnectionAsync();
    await using var checkCmd = new NpgsqlCommand(
        "SELECT id FROM bd_users WHERE reset_token = $1 AND reset_token_expires > now()", conn);
    checkCmd.Parameters.AddWithValue(req.Token);
    var userId = await checkCmd.ExecuteScalarAsync();
    if (userId is null)
        return Results.BadRequest(new { error = "This reset link is invalid or has expired." });

    var hash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await using var updateCmd = new NpgsqlCommand(
        "UPDATE bd_users SET password_hash = $1, reset_token = NULL, reset_token_expires = NULL WHERE id = $2", conn);
    updateCmd.Parameters.AddWithValue(hash);
    updateCmd.Parameters.AddWithValue((int)userId);
    await updateCmd.ExecuteNonQueryAsync();

    return Results.Ok(new { success = true });
}).RequireRateLimiting("auth");

app.MapDelete($"{apiBase}/account", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Unauthorized();
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM bd_users WHERE id = $1", conn);
    cmd.Parameters.AddWithValue(userId);
    await cmd.ExecuteNonQueryAsync();

    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true });
}).RequireAuthorization();

// ---------- Bookmarks ----------
var bookmarks = app.MapGroup($"{apiBase}/account/bookmarks").RequireAuthorization();

bookmarks.MapGet("/", async (HttpContext http, NpgsqlDataSource db) =>
{
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        $@"SELECT {GuideSummaryColumns} FROM bd_bookmarks b
           JOIN bd_guides g ON g.id = b.guide_id
           JOIN categories c ON c.id = g.category_id
           WHERE b.user_id = $1 ORDER BY b.created_at DESC", conn);
    cmd.Parameters.AddWithValue(userId);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync()) results.Add(ReadGuideSummary(reader));
    return Results.Ok(results);
});

bookmarks.MapPost("/{slug}", async (string slug, HttpContext http, NpgsqlDataSource db) =>
{
    var userId = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"INSERT INTO bd_bookmarks (user_id, guide_id)
          SELECT $1, id FROM bd_guides WHERE slug = $2
          ON CONFLICT DO NOTHING", conn);
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
        @"DELETE FROM bd_bookmarks WHERE user_id = $1
          AND guide_id = (SELECT id FROM bd_guides WHERE slug = $2)", conn);
    cmd.Parameters.AddWithValue(userId);
    cmd.Parameters.AddWithValue(slug);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok(new { saved = false });
});

// ---------- Categories (public read) ----------
app.MapGet($"{apiBase}/categories", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("SELECT id, name, slug, description FROM categories ORDER BY name", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            id = reader.GetInt32(0),
            name = reader.GetString(1),
            slug = reader.GetString(2),
            description = reader.IsDBNull(3) ? null : reader.GetString(3)
        });
    }
    return Results.Ok(results);
});

// ---------- Guides (public) ----------
app.MapGet($"{apiBase}/guides", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        $@"SELECT {GuideSummaryColumns} FROM bd_guides g JOIN categories c ON c.id = g.category_id
           WHERE g.is_published = true ORDER BY g.is_featured DESC, g.title", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync()) results.Add(ReadGuideSummary(reader));
    return Results.Ok(results);
});

app.MapGet($"{apiBase}/guides/{{slug}}", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        $@"SELECT g.id, g.slug, g.category_id, c.name AS category, g.title, g.summary, g.steps, g.requirements,
           g.fees, g.processing_time, g.office, g.published_at, g.last_verified, g.keywords, g.meta_description, g.featured_image,
           COALESCE((SELECT array_agg(t.name ORDER BY t.name) FROM guide_tags gt JOIN tags t ON t.id = gt.tag_id WHERE gt.guide_id = g.id), ARRAY[]::text[]) AS tag_names
           FROM bd_guides g JOIN categories c ON c.id = g.category_id WHERE g.slug = $1", conn);
    cmd.Parameters.AddWithValue(slug);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.NotFound(new { error = "Guide not found." });
    return Results.Ok(new
    {
        id = reader.GetInt32(0),
        slug = reader.GetString(1),
        categoryId = reader.GetInt32(2),
        category = reader.GetString(3),
        title = reader.GetString(4),
        summary = reader.GetString(5),
        steps = ParseStringArray(reader.GetString(6)),
        requirements = ParseStringArray(reader.GetString(7)),
        fees = reader.IsDBNull(8) ? null : reader.GetString(8),
        processingTime = reader.IsDBNull(9) ? null : reader.GetString(9),
        office = reader.IsDBNull(10) ? null : reader.GetString(10),
        publishedAt = reader.GetDateTime(11),
        lastVerified = reader.GetDateTime(12),
        keywords = reader.IsDBNull(13) ? null : reader.GetString(13),
        metaDescription = reader.IsDBNull(14) ? null : reader.GetString(14),
        featuredImage = reader.IsDBNull(15) ? null : reader.GetString(15),
        tags = reader.GetFieldValue<string[]>(16)
    });
});

// ---------- Blog (public, unchanged) ----------
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
    if (!await reader.ReadAsync()) return Results.NotFound(new { error = "Post not found." });
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

// ---------- Admin: Categories ----------
var adminCategories = app.MapGroup($"{apiBase}/admin/categories").RequireAuthorization();

adminCategories.MapPost("/", async (CategoryDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "INSERT INTO categories (name, slug, description) VALUES ($1, $2, $3) RETURNING id", conn);
    cmd.Parameters.AddWithValue(dto.Name.Trim());
    cmd.Parameters.AddWithValue(Slugify(dto.Name));
    cmd.Parameters.AddWithValue((object?)dto.Description ?? DBNull.Value);
    var id = (int)(await cmd.ExecuteScalarAsync())!;
    return Results.Ok(new { id });
});

adminCategories.MapPut("/{id:int}", async (int id, CategoryDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE categories SET name=$1, description=$2 WHERE id=$3", conn);
    cmd.Parameters.AddWithValue(dto.Name.Trim());
    cmd.Parameters.AddWithValue((object?)dto.Description ?? DBNull.Value);
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
});

adminCategories.MapDelete("/{id:int}", async (int id, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM categories WHERE id=$1", conn);
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
});

// ---------- Admin: Tags ----------
app.MapGet($"{apiBase}/admin/tags", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("SELECT id, name, slug FROM tags ORDER BY name", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
        results.Add(new { id = reader.GetInt32(0), name = reader.GetString(1), slug = reader.GetString(2) });
    return Results.Ok(results);
}).RequireAuthorization();

app.MapDelete($"{apiBase}/admin/tags/{{id:int}}", async (int id, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM tags WHERE id=$1", conn);
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
}).RequireAuthorization();

app.MapPost($"{apiBase}/admin/tags", async (TagDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    try
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO tags (name, slug) VALUES ($1, $2) RETURNING id", conn);
        cmd.Parameters.AddWithValue(dto.Name.Trim());
        cmd.Parameters.AddWithValue(Slugify(dto.Name));
        var id = (int)(await cmd.ExecuteScalarAsync())!;
        return Results.Ok(new { id });
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        return Results.Conflict(new { error = "A tag with this name already exists." });
    }
}).RequireAuthorization();

app.MapPut($"{apiBase}/admin/tags/{{id:int}}", async (int id, TagDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("UPDATE tags SET name=$1, slug=$2 WHERE id=$3", conn);
    cmd.Parameters.AddWithValue(dto.Name.Trim());
    cmd.Parameters.AddWithValue(Slugify(dto.Name));
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
}).RequireAuthorization();

// ---------- Admin: image upload ----------
// NOTE: Render's free tier disk is ephemeral — uploaded files are wiped on
// every redeploy/restart. Fine for quick previews, not for permanent hosting.
// For production, swap this for a real object-storage service later.
app.MapPost($"{apiBase}/admin/upload", async (HttpContext http, IWebHostEnvironment env) =>
{
    if (!IsAdmin(http)) return Forbidden();

    var form = await http.Request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null || file.Length == 0) return Results.BadRequest(new { error = "No file uploaded." });
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest(new { error = "File too large (max 5MB)." });

    var webRoot = string.IsNullOrEmpty(env.WebRootPath)
        ? Path.Combine(env.ContentRootPath, "wwwroot")
        : env.WebRootPath;
    var uploadsPath = Path.Combine(webRoot, "uploads");
    Directory.CreateDirectory(uploadsPath);

    var extension = Path.GetExtension(file.FileName);
    var fileName = $"{Guid.NewGuid()}{extension}";
    var filePath = Path.Combine(uploadsPath, fileName);

    await using (var stream = File.Create(filePath))
    {
        await file.CopyToAsync(stream);
    }

    var imageUrl = $"{http.Request.Scheme}://{http.Request.Host}/uploads/{fileName}";
    return Results.Ok(new { imageUrl });
}).RequireAuthorization();

// ---------- Related guides ----------
app.MapGet($"{apiBase}/guides/{{slug}}/related", async (string slug, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        $@"SELECT {GuideSummaryColumns} FROM bd_guides g JOIN categories c ON c.id = g.category_id
           WHERE g.is_published = true AND g.slug != $1
           AND g.category_id = (SELECT category_id FROM bd_guides WHERE slug = $1)
           ORDER BY g.is_featured DESC, g.title LIMIT 3", conn);
    cmd.Parameters.AddWithValue(slug);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync()) results.Add(ReadGuideSummary(reader));
    return Results.Ok(results);
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
            @"INSERT INTO bd_guides (slug, category_id, title, summary, steps, requirements, fees, processing_time,
              office, featured_image, keywords, meta_description, is_featured, is_published, last_verified)
              VALUES ($1, $2, $3, $4, $5::jsonb, $6::jsonb, $7, $8, $9, $10, $11, $12, $13, $14, now()) RETURNING id", conn);
        cmd.Parameters.AddWithValue(req.Slug.Trim());
        cmd.Parameters.AddWithValue(req.CategoryId);
        cmd.Parameters.AddWithValue(req.Title.Trim());
        cmd.Parameters.AddWithValue(req.Summary.Trim());
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Steps));
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Requirements));
        cmd.Parameters.AddWithValue((object?)req.Fees ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.ProcessingTime ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.Office ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.FeaturedImage ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.Keywords ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)req.MetaDescription ?? DBNull.Value);
        cmd.Parameters.AddWithValue(req.IsFeatured);
        cmd.Parameters.AddWithValue(req.IsPublished);
        var guideId = (int)(await cmd.ExecuteScalarAsync())!;

        var tagIds = await ResolveTagIdsAsync(conn, req.Tags ?? new List<string>());
        await SetGuideTagsAsync(conn, guideId, tagIds);

        return Results.Ok(new { success = true, id = guideId });
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
        @"UPDATE bd_guides SET category_id=$1, title=$2, summary=$3, steps=$4::jsonb, requirements=$5::jsonb,
          fees=$6, processing_time=$7, office=$8, featured_image=$9, keywords=$10, meta_description=$11,
          is_featured=$12, is_published=$13, last_verified=now(), updated_at=now()
          WHERE slug=$14 RETURNING id", conn);
    cmd.Parameters.AddWithValue(req.CategoryId);
    cmd.Parameters.AddWithValue(req.Title.Trim());
    cmd.Parameters.AddWithValue(req.Summary.Trim());
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Steps));
    cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.Requirements));
    cmd.Parameters.AddWithValue((object?)req.Fees ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.ProcessingTime ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.Office ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.FeaturedImage ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.Keywords ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)req.MetaDescription ?? DBNull.Value);
    cmd.Parameters.AddWithValue(req.IsFeatured);
    cmd.Parameters.AddWithValue(req.IsPublished);
    cmd.Parameters.AddWithValue(slug);

    var result = await cmd.ExecuteScalarAsync();
    if (result is null) return Results.NotFound(new { error = "Guide not found." });

    var tagIds = await ResolveTagIdsAsync(conn, req.Tags ?? new List<string>());
    await SetGuideTagsAsync(conn, (int)result, tagIds);

    return Results.Ok(new { success = true });
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
              VALUES ($1, $2, $3, $4, $5, $6::jsonb)", conn);
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
        "UPDATE bd_blog_posts SET title=$1, excerpt=$2, content=$3, cover_image_url=$4, tags=$5::jsonb WHERE slug=$6", conn);
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

// ---------- Admin: Hero slides ----------
var adminHero = app.MapGroup($"{apiBase}/admin/hero-slides").RequireAuthorization();

adminHero.MapGet("/", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, guide_id, image_url, title, subtitle, button_text, button_link, display_order, is_active FROM hero_slides ORDER BY display_order", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            id = reader.GetInt32(0),
            guideId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
            imageUrl = reader.GetString(2),
            title = reader.GetString(3),
            subtitle = reader.IsDBNull(4) ? null : reader.GetString(4),
            buttonText = reader.IsDBNull(5) ? null : reader.GetString(5),
            buttonLink = reader.IsDBNull(6) ? null : reader.GetString(6),
            displayOrder = reader.GetInt32(7),
            isActive = reader.GetBoolean(8)
        });
    }
    return Results.Ok(results);
});

adminHero.MapPost("/", async (HeroSlideDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"INSERT INTO hero_slides (guide_id, image_url, title, subtitle, button_text, button_link, display_order, is_active)
          VALUES ($1, $2, $3, $4, $5, $6, $7, $8) RETURNING id", conn);
    cmd.Parameters.AddWithValue((object?)dto.GuideId ?? DBNull.Value);
    cmd.Parameters.AddWithValue(dto.ImageUrl);
    cmd.Parameters.AddWithValue(dto.Title);
    cmd.Parameters.AddWithValue((object?)dto.Subtitle ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)dto.ButtonText ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)dto.ButtonLink ?? DBNull.Value);
    cmd.Parameters.AddWithValue(dto.DisplayOrder);
    cmd.Parameters.AddWithValue(dto.IsActive);
    var id = (int)(await cmd.ExecuteScalarAsync())!;
    return Results.Ok(new { id });
});

adminHero.MapPut("/{id:int}", async (int id, HeroSlideDto dto, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"UPDATE hero_slides SET guide_id=$1, image_url=$2, title=$3, subtitle=$4, button_text=$5,
          button_link=$6, display_order=$7, is_active=$8 WHERE id=$9", conn);
    cmd.Parameters.AddWithValue((object?)dto.GuideId ?? DBNull.Value);
    cmd.Parameters.AddWithValue(dto.ImageUrl);
    cmd.Parameters.AddWithValue(dto.Title);
    cmd.Parameters.AddWithValue((object?)dto.Subtitle ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)dto.ButtonText ?? DBNull.Value);
    cmd.Parameters.AddWithValue((object?)dto.ButtonLink ?? DBNull.Value);
    cmd.Parameters.AddWithValue(dto.DisplayOrder);
    cmd.Parameters.AddWithValue(dto.IsActive);
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
});

adminHero.MapDelete("/{id:int}", async (int id, HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM hero_slides WHERE id=$1", conn);
    cmd.Parameters.AddWithValue(id);
    var rows = await cmd.ExecuteNonQueryAsync();
    return rows == 0 ? Results.NotFound() : Results.Ok(new { success = true });
});

// Public read for the home page slider
app.MapGet($"{apiBase}/hero-slides", async (NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        @"SELECT hs.id, g.slug AS guide_slug, hs.image_url, hs.title, hs.subtitle, hs.button_text, hs.button_link
          FROM hero_slides hs LEFT JOIN bd_guides g ON g.id = hs.guide_id
          WHERE hs.is_active = true ORDER BY hs.display_order", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            id = reader.GetInt32(0),
            guideSlug = reader.IsDBNull(1) ? null : reader.GetString(1),
            imageUrl = reader.GetString(2),
            title = reader.GetString(3),
            subtitle = reader.IsDBNull(4) ? null : reader.GetString(4),
            buttonText = reader.IsDBNull(5) ? null : reader.GetString(5),
            buttonLink = reader.IsDBNull(6) ? null : reader.GetString(6)
        });
    }
    return Results.Ok(results);
});

// ---------- Admin: dashboard stats ----------
app.MapGet($"{apiBase}/admin/dashboard", async (HttpContext http, NpgsqlDataSource db) =>
{
    if (!IsAdmin(http)) return Forbidden();
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(@"
        SELECT
            (SELECT COUNT(*) FROM bd_users) AS total_users,
            (SELECT COUNT(*) FROM bd_guides) AS total_guides,
            (SELECT COUNT(*) FROM bd_blog_posts) AS total_blogs,
            (SELECT COUNT(*) FROM bd_bookmarks) AS total_bookmarks", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (await reader.ReadAsync())
    {
        return Results.Ok(new
        {
            totalUsers = reader.GetInt32(0),
            totalGuides = reader.GetInt32(1),
            totalBlogs = reader.GetInt32(2),
            totalBookmarks = reader.GetInt32(3)
        });
    }
    return Results.Ok(new { totalUsers = 0, totalGuides = 0, totalBlogs = 0, totalBookmarks = 0 });
}).RequireAuthorization();

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
record ForgotPasswordRequest(string Email);
record ResetPasswordRequest(string Token, string NewPassword);
record UpdateAccountRequest(string FullName, string? Phone);
record UserResponse(int Id, string Email, string FullName, string? Phone, DateTime CreatedAt, bool IsAdmin);
record AdminGuideRequest(
    string Slug, int CategoryId, string Title, string Summary,
    List<string> Steps, List<string> Requirements,
    string? Fees, string? ProcessingTime, string? Office,
    string? FeaturedImage, string? Keywords, string? MetaDescription,
    bool IsFeatured, bool IsPublished, List<string>? Tags);
record AdminBlogRequest(string Slug, string Title, string Excerpt, string Content, string? CoverImageUrl, List<string>? Tags);
record CategoryDto(string Name, string? Description);
record TagDto(string Name);
record HeroSlideDto(int? GuideId, string ImageUrl, string Title, string? Subtitle, string? ButtonText, string? ButtonLink, int DisplayOrder, bool IsActive);
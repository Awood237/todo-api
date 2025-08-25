using Microsoft.EntityFrameworkCore;
using Npgsql;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Bind to Railway's injected PORT (fallback 8080 locally)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---- Build Postgres connection string ----
string BuildConnectionString()
{
    var raw = Environment.GetEnvironmentVariable("DATABASE_URL")
             ?? builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(raw))
        throw new InvalidOperationException("No connection string found. Set DATABASE_URL or DefaultConnection.");

    // Convert postgres:// URI -> Npgsql keyword string
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.AbsolutePath.Trim('/'),
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        };
        return csb.ToString();
    }

    return raw; // already keyword-style
}

var connectionString = BuildConnectionString();

// Services
builder.Services.AddDbContext<TodoContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// Single root + health
app.MapGet("/", () => "API is running!");
app.MapHealthChecks("/health");

app.MapControllers();

// Create DB if missing; don't fail startup if it can't connect yet
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TodoContext>();
    try
    {
        await context.Database.EnsureCreatedAsync();
        Console.WriteLine("Database ensure-created OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database ensure-created failed: {ex.Message}");
    }
}

app.Run();
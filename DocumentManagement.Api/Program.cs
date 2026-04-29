using System.Text;
using DocumentManagement.Api.Health;
using DocumentManagement.Api.Services;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Services;
using DocumentManagement.Infrastructure.Data;
using DocumentManagement.Infrastructure.Repositories;
using DocumentManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "DocumentManagement.Api";
});

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(AppContext.BaseDirectory, "logs", "api-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30);
});

// =======================
// DATABASE PROVIDER
// =======================
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
var databaseProvider = databaseOptions.GetProvider();
var rootPath = Directory.GetCurrentDirectory();

IDbConnectionFactory connectionFactory;
IDatabaseDialect databaseDialect;

if (databaseProvider == DatabaseProvider.SqlServer)
{
    if (string.IsNullOrWhiteSpace(databaseOptions.ConnectionString))
    {
        throw new InvalidOperationException("Thiếu cấu hình Database:ConnectionString cho SQL Server.");
    }

    connectionFactory = new SqlServerConnectionFactory(databaseOptions.ConnectionString);
    databaseDialect = new SqlServerDialect();
}
else
{
    var databasePath = string.IsNullOrWhiteSpace(databaseOptions.Path)
        ? Path.Combine(rootPath, "database", "app.db")
        : databaseOptions.Path;

    var databaseDirectory = Path.GetDirectoryName(databasePath);
    if (!string.IsNullOrWhiteSpace(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }

    connectionFactory = new SqliteConnectionFactory($"Data Source={databasePath}");
    databaseDialect = new SqliteDialect();
}

// =======================
// SERVICES
// =======================
builder.Services.AddControllers();
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(rootPath, "keys")));
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập dạng: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "Bearer",
                Name = "Authorization",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddSingleton(connectionFactory);
builder.Services.AddSingleton(databaseDialect);

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IAttachmentRepository, AttachmentRepository>();

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IOcrService, PdfExtractionService>();
builder.Services.AddSingleton<IFileStorageService>(_ =>
    new LocalFileStorageService(Path.Combine(rootPath, "storage", "attachments")));

builder.Services.AddScoped<JwtService>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"] ?? throw new Exception("JWT Secret is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],

            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

// =======================
// DATABASE MIGRATION
// =======================
using (var scope = app.Services.CreateScope())
{
    var activeConnectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

    if (activeConnectionFactory is SqlServerConnectionFactory sqlServerConnectionFactory)
    {
        SqlServerDatabaseMigrator.Migrate(sqlServerConnectionFactory);
    }
    else if (activeConnectionFactory is SqliteConnectionFactory sqliteConnectionFactory)
    {
        DatabaseMigrator.Migrate(sqliteConnectionFactory);
    }
    else
    {
        throw new InvalidOperationException("Database provider không được hỗ trợ.");
    }
}

// =======================
// HTTP PIPELINE
// =======================
app.UseSerilogRequestLogging();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        logger.LogError(
            exception,
            "Unhandled exception while processing {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            message = "Có lỗi hệ thống. Vui lòng thử lại hoặc liên hệ quản trị viên."
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program
{
}

using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Security;
using DocumentManagement.Application.Services;
using DocumentManagement.Infrastructure.Data;
using DocumentManagement.Infrastructure.Repositories;
using DocumentManagement.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dbPath = Path.Combine(AppContext.BaseDirectory, "database", "app.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var connectionString = $"Data Source={dbPath}";

builder.Services.AddSingleton(new SqliteConnectionFactory(connectionString));
builder.Services.AddSingleton<DatabaseInitializer>();

builder.Services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddSingleton<IAuthService, AuthService>();

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();

builder.Services.AddScoped<IDocumentService, DocumentService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var connectionFactory = scope.ServiceProvider.GetRequiredService<DocumentManagement.Infrastructure.Data.SqliteConnectionFactory>();
    DocumentManagement.Infrastructure.Data.DatabaseMigrator.Migrate(connectionFactory);
}

var dbInit = app.Services.GetRequiredService<DatabaseInitializer>();
dbInit.Initialize();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
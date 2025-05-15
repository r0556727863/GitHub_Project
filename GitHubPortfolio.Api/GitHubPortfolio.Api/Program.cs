using GitHubPortfolio.Service.Decorators;
using GitHubPortfolio.Service.Interfaces;
using GitHubPortfolio.Service.Options;
using GitHubPortfolio.Service.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ����� ������� ���������
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GitHubPortfolio API",
        Version = "v1",
        Description = "API ������ ��������� �-GitHub",
    });
});

// ����� �������� GitHub
builder.Services.Configure<GitHubOptions>(
    builder.Configuration.GetSection("GitHub"));

// ����� Memory Cache
builder.Services.AddMemoryCache();

// ����� ����� GitHub �� Decorator ������ �-Caching
builder.Services.AddTransient<IGitHubService, GitHubService>();
builder.Services.Decorate<IGitHubService, CachedGitHubService>();

// ����� CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// ����� �����
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// ����� ����� ������ HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GitHubPortfolio API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
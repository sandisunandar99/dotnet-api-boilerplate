using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.IO;
using System.Text;
using App.Data;
using App.Middlewares;
using dotenv.net;

// Load .env file at the very beginning
DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { ".env" }));

// Build configuration
var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
    .AddEnvironmentVariables();
var configuration = configurationBuilder.Build();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
builder.Configuration.AddConfiguration(configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ✅ Properly configure Swagger with JWT security
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dotnet API Boilerplate",
        Version = "v1",
        Description = "A boilerplate for building RESTful APIs with .NET 8, Entity Framework Core, MySQL, and JWT authentication.",
        Contact = new OpenApiContact
        {
            Name = "Sandi Sunandar",
            Email = "sandisunandar99@gmail.com"
        }
    });

    // ✅ Security Definition - Fixed configuration
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below. Example: 'eyJhbGc...'  (Do NOT include 'Bearer' - it will be added automatically)"
    });

    // ✅ CRITICAL: Add global security requirement
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Alternative: Use operation filter (you had this but it needs proper setup)
    // options.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21))
    )
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// ✅ Use custom middleware INSTEAD of standard authentication
app.UseMiddleware<JwtMiddleware>();

app.MapControllers();

app.Run();
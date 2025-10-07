using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using App.Data;
using App.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add OpenAPI/Swagger document information
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

    // Security Definition
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });

    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21))
    )
);

// JWT Configuration - Only for token generation, validation handled by custom middleware
// Note: Removed AddAuthentication() and AddJwtBearer() to avoid conflict with custom middleware

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // ✅ Correct path for .NET 8
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        c.RoutePrefix = "swagger";
    });
}


app.UseHttpsRedirection();

// Custom JWT middleware for token validation
app.UseMiddleware<JwtMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

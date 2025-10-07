using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace App.Middlewares;

/// <summary>
/// Custom JWT middleware for validating Bearer tokens
/// This middleware intercepts requests and validates JWT tokens if Authorization header is present
/// Excludes authentication endpoints like login/register from token validation
/// </summary>
public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    // Define endpoints that don't require JWT validation
    private readonly List<string> _excludedPaths = new()
    {
        "/api/auth/login",
        "/api/auth/register",
        "/swagger"
        // Removed "/" to ensure ALL API endpoints require JWT validation
    };

    public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task Invoke(HttpContext context)
    {
        // Debug logging - Shows middleware is processing requests
        Console.WriteLine($"🔐 JWT Middleware: Processing {context.Request.Method} {context.Request.Path.Value}");

        // Skip JWT validation for excluded paths
        if (IsExcludedPath(context.Request.Path.Value))
        {
            Console.WriteLine($"🔓 Excluded from JWT validation");
            await _next(context);
            return;
        }

        Console.WriteLine($"🔒 JWT validation required");

        // Check if Authorization header exists
        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            Console.WriteLine($"❌ No Authorization header - ACCESS DENIED");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Authorization header is required\", \"message\": \"Please provide a Bearer token in the Authorization header\"}");
            return;
        }

        Console.WriteLine($"✅ Authorization header found - validating token");

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid Authorization header format. Use 'Bearer <token>'\"}");
            return;
        }

        var token = authHeader.Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"JWT token is empty\"}");
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            if (string.IsNullOrEmpty(_configuration["Jwt:Key"]))
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"JWT configuration is missing\"}");
                return;
            }

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero // No tolerance for token expiration
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;

            // Extract and store user information in HttpContext.Items
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "nameid");
            var usernameClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub");

            if (userIdClaim != null)
                context.Items["UserId"] = userIdClaim.Value;

            if (usernameClaim != null)
                context.Items["Username"] = usernameClaim.Value;

            // Store the whole JWT token for potential future use
            context.Items["JwtToken"] = jwtToken;

            // Set user principal if needed
            var identity = new System.Security.Claims.ClaimsIdentity("Jwt");
            identity.AddClaims(jwtToken.Claims);
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        catch (SecurityTokenExpiredException)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"JWT token has expired\"}");
            return;
        }
        catch (SecurityTokenInvalidIssuerException)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token issuer\"}");
            return;
        }
        catch (SecurityTokenInvalidAudienceException)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token audience\"}");
            return;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token signature\"}");
            return;
        }
        catch (SecurityTokenException ex)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($" {{\"error\": \"Invalid JWT token: {ex.Message}\"}}");
            return;
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($" {{\"error\": \"Token validation failed: {ex.Message}\"}}");
            return;
        }

        await _next(context);
    }

    private bool IsExcludedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Check exact path matches
        if (_excludedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            return true;

        // Check if path starts with any excluded path
        return _excludedPaths.Any(excluded =>
            path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
    }
}

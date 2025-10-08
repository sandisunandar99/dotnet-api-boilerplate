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
        "/swagger",
        "/swagger/v1/swagger.json",
        "/swagger/index.html",
        "/_framework",
        "/_vs"
    };

    public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task Invoke(HttpContext context)
    {
        Console.WriteLine($"🔐 JWT Middleware: Processing {context.Request.Method} {context.Request.Path.Value}");

        // Skip JWT validation for excluded paths
        if (IsExcludedPath(context.Request.Path.Value))
        {
            Console.WriteLine($"🔓 Excluded from JWT validation: {context.Request.Path.Value}");
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

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        Console.WriteLine($"📋 Raw Authorization header: '{authHeader}'");

        if (string.IsNullOrEmpty(authHeader))
        {
            Console.WriteLine($"❌ Authorization header is empty");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Authorization header is empty\"}");
            return;
        }

        // ✅ FIX: More robust token extraction
        string token;

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // Extract token after "Bearer "
            token = authHeader.Substring(7).Trim();
        }
        else if (authHeader.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // Handle lowercase "bearer"
            token = authHeader.Substring(7).Trim();
        }
        else
        {
            // If no "Bearer " prefix, treat the whole string as token (fallback)
            token = authHeader.Trim();
        }

        Console.WriteLine($"🎫 Extracted token (first 20 chars): '{token.Substring(0, Math.Min(20, token.Length))}...'");
        Console.WriteLine($"📏 Token length: {token.Length}");

        // ✅ Validate token is not empty after extraction
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine($"❌ JWT token is empty after extraction");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"JWT token is empty\"}");
            return;
        }

        // ✅ Check if token has the correct JWT format (should have 2 dots)
        if (token.Split('.').Length != 3)
        {
            Console.WriteLine($"❌ JWT token is malformed - expected 3 parts separated by dots, got {token.Split('.').Length}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"JWT token is malformed. Token must be in format: header.payload.signature\"}");
            return;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // ✅ Validate that the token can be read before attempting validation
            if (!tokenHandler.CanReadToken(token))
            {
                Console.WriteLine($"❌ Token cannot be read - not a valid JWT format");
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Invalid JWT token format\"}");
                return;
            }

            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                Console.WriteLine($"❌ JWT configuration is missing");
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"JWT configuration is missing\"}");
                return;
            }

            var key = Encoding.UTF8.GetBytes(jwtKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };

            Console.WriteLine($"🔍 Validating token...");
            Console.WriteLine($"   Issuer: {_configuration["Jwt:Issuer"]}");
            Console.WriteLine($"   Audience: {_configuration["Jwt:Audience"]}");

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            Console.WriteLine($"✅ JWT token validated successfully");
            Console.WriteLine($"   Claims count: {jwtToken.Claims.Count()}");

            // Extract and store user information in HttpContext.Items
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "nameid");
            var usernameClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub");

            if (userIdClaim != null)
            {
                context.Items["UserId"] = userIdClaim.Value;
                Console.WriteLine($"   UserId: {userIdClaim.Value}");
            }

            if (usernameClaim != null)
            {
                context.Items["Username"] = usernameClaim.Value;
                Console.WriteLine($"   Username: {usernameClaim.Value}");
            }

            // Store the whole JWT token for potential future use
            context.Items["JwtToken"] = jwtToken;

            // Set user principal
            context.User = principal;
        }
        catch (SecurityTokenExpiredException ex)
        {
            Console.WriteLine($"❌ JWT token has expired: {ex.Message}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"JWT token has expired\"}");
            return;
        }
        catch (SecurityTokenInvalidIssuerException ex)
        {
            Console.WriteLine($"❌ Invalid JWT token issuer: {ex.Message}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token issuer\"}");
            return;
        }
        catch (SecurityTokenInvalidAudienceException ex)
        {
            Console.WriteLine($"❌ Invalid JWT token audience: {ex.Message}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token audience\"}");
            return;
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            Console.WriteLine($"❌ Invalid JWT token signature: {ex.Message}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Invalid JWT token signature\"}");
            return;
        }
        catch (SecurityTokenException ex)
        {
            Console.WriteLine($"❌ Invalid JWT token: {ex.Message}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\": \"Invalid JWT token: {ex.Message}\"}}");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Token validation failed: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($"{{\"error\": \"Token validation failed: {ex.Message}\"}}");
            return;
        }

        await _next(context);
    }

    private bool IsExcludedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        path = path.ToLowerInvariant();

        return _excludedPaths.Any(excluded =>
            path.StartsWith(excluded.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }
}
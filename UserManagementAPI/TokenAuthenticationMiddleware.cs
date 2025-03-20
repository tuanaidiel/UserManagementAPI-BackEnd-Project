using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UserManagementAPI.Middleware
{
    public class TokenAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TokenAuthenticationMiddleware> _logger;

        // List of paths that don't require authentication
        private readonly string[] _publicPaths = new[] 
        { 
            "/swagger", 
            "/api/auth/login", 
            "/api/auth/register" 
        };

        public TokenAuthenticationMiddleware(
            RequestDelegate next, 
            IConfiguration configuration, 
            ILogger<TokenAuthenticationMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip authentication for public paths
            if (IsPublicPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // Try to get the token from the Authorization header
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Authentication failed: No token provided");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new 
                { 
                    error = "Authentication failed. No token provided." 
                });
                
                await context.Response.WriteAsync(result);
                return;
            }

            try
            {
                // Validate the token
                var principal = ValidateToken(token);
                
                // Set the user on the HttpContext
                context.User = principal;
                
                // Store user ID for potential use in controller
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    context.Items["UserId"] = userId;
                }
                
                _logger.LogInformation("User authenticated successfully: {UserId}", userId);
                
                // Continue down the middleware pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Authentication failed: Invalid token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                
                var result = JsonSerializer.Serialize(new 
                { 
                    error = "Authentication failed. Invalid token." 
                });
                
                await context.Response.WriteAsync(result);
            }
        }

        private ClaimsPrincipal ValidateToken(string token)
        {
            // For demo purposes, we're using a simple secret key
            // In production, use proper key management
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:SecretKey"] ?? "YourSecretKeyHere-MakeSureItIsLongEnough");

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }

        private bool IsPublicPath(PathString path)
        {
            return _publicPaths.Any(p => path.StartsWithSegments(p));
        }
    }

    // Extension method
    public static class TokenAuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenAuthenticationMiddleware>();
        }
    }
}
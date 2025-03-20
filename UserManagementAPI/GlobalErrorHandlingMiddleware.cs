using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace UserManagementAPI.Middleware
{
    public class GlobalErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalErrorHandlingMiddleware> _logger;

        public GlobalErrorHandlingMiddleware(RequestDelegate next, ILogger<GlobalErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var errorResponse = new ErrorResponse();
            var statusCode = HttpStatusCode.InternalServerError; // 500 by default

            // Customize response based on exception type
            switch (exception)
            {
                case ArgumentException argEx:
                    statusCode = HttpStatusCode.BadRequest;
                    errorResponse.Message = "Invalid arguments provided.";
                    break;
                    
                case UnauthorizedAccessException _:
                    statusCode = HttpStatusCode.Unauthorized;
                    errorResponse.Message = "Unauthorized access.";
                    break;
                    
                case KeyNotFoundException _:
                    statusCode = HttpStatusCode.NotFound;
                    errorResponse.Message = "Requested resource not found.";
                    break;
                    
                default:
                    errorResponse.Message = "An internal server error occurred.";
                    break;
            }

            // Set status code
            context.Response.StatusCode = (int)statusCode;
            
            // Log the exception with appropriate level based on status code
            if ((int)statusCode >= 500)
            {
                _logger.LogError(exception, "Server error occurred: {Message}", exception.Message);
            }
            else
            {
                _logger.LogWarning("Client error occurred: {StatusCode} - {Message}", 
                    (int)statusCode, exception.Message);
            }

            // In development, include more details
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                errorResponse.DeveloperMessage = exception.ToString();
            }

            // Serialize and write the error response
            var result = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            
            await context.Response.WriteAsync(result);
        }
    }

    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string? DeveloperMessage { get; set; }
        public string? RequestId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Extension method
    public static class GlobalErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalErrorHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalErrorHandlingMiddleware>();
        }
    }
}
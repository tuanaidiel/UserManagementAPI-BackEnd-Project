using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace UserManagementAPI.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log the request
            await LogRequest(context);

            // Capture the original response body stream
            var originalBodyStream = context.Response.Body;

            // Create a new memory stream to capture the response
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                // Continue down the middleware pipeline
                await _next(context);

                // Log the response
                await LogResponse(context, responseBody, originalBodyStream);
            }
            finally
            {
                // Ensure the original response body is restored
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task LogRequest(HttpContext context)
        {
            context.Request.EnableBuffering();

            var requestTime = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString();
            
            // Store the request ID for correlation with the response
            context.Items["RequestId"] = requestId;
            context.Items["RequestTime"] = requestTime;

            // Log basic request info
            _logger.LogInformation(
                "Request {RequestId} received at {RequestTime} - {Method} {Scheme}://{Host}{Path}{QueryString}",
                requestId,
                requestTime,
                context.Request.Method,
                context.Request.Scheme,
                context.Request.Host,
                context.Request.Path,
                context.Request.QueryString);

            // If it's important to log the request body (careful with sensitive data)
            if (context.Request.ContentLength > 0)
            {
                var buffer = new byte[context.Request.ContentLength.Value];
                var bytesRead = 0;
                var totalBytesRead = 0;
                
                // Read the entire request body
                while (totalBytesRead < buffer.Length && 
                       (bytesRead = await context.Request.Body.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;
                }
                
                var requestBody = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);
                
                _logger.LogDebug("Request {RequestId} body: {RequestBody}", requestId, requestBody);
                
                // Reset the position to the beginning for the next middleware
                context.Request.Body.Position = 0;
            }
        }

        private async Task LogResponse(HttpContext context, MemoryStream responseBody, Stream originalBodyStream)
        {
            var requestId = context.Items["RequestId"] as string;
            
            // Safely get the request time with null check
            var requestTime = context.Items["RequestTime"] as DateTime?;
            var responseTime = DateTime.UtcNow;
            var elapsedMs = requestTime.HasValue ? (responseTime - requestTime.Value).TotalMilliseconds : 0;

            // Log basic response info
            _logger.LogInformation(
                "Response {RequestId} generated at {ResponseTime} - {StatusCode} in {ElapsedMilliseconds}ms",
                requestId,
                responseTime,
                context.Response.StatusCode,
                elapsedMs);

            // Copy the response to the original stream
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBodyStream);
            
            // If it's important to log the response body (careful with sensitive data)
            if (responseBody.Length > 0 && !context.Request.Path.StartsWithSegments("/swagger"))
            {
                responseBody.Position = 0;
                var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
                
                _logger.LogDebug("Response {RequestId} body: {ResponseBody}", requestId, responseBodyText);
            }
        }
    }

    // Extension method to make it easier to add the middleware to the pipeline
    public static class RequestResponseLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
        }
    }
}
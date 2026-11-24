using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SkyMate.Application.AI;

namespace SkyMate.API.ExceptionHandling;

/// <summary>Maps expected assistant-provider failures to safe Problem Details responses.</summary>
public sealed class AssistantExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AssistantExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            AssistantProviderRateLimitException => (
                StatusCodes.Status429TooManyRequests,
                "Assistant rate limit reached",
                "The assistant is temporarily rate limited. Try again shortly."),
            AssistantConfigurationException => (
                StatusCodes.Status503ServiceUnavailable,
                "Assistant configuration is missing",
                "The assistant provider is not configured. Contact the service administrator."),
            AssistantProviderTimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "Assistant provider timed out",
                "The assistant could not respond before the configured timeout."),
            AssistantProviderException => (
                StatusCodes.Status502BadGateway,
                "Assistant provider failed",
                "The assistant provider could not complete this request."),
            _ => (0, string.Empty, string.Empty)
        };

        if (status == 0 || httpContext.Response.HasStarted)
        {
            return false;
        }

        logger.LogWarning(exception, "Assistant request failed with status {StatusCode}.", status);
        httpContext.Response.StatusCode = status;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
        return true;
    }
}

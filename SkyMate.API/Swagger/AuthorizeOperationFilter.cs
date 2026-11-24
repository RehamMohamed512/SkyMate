using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SkyMate.API.Swagger;

/// <summary>Adds Bearer security details to protected operations in Swagger.</summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    /// <summary>Updates the OpenAPI operation based on its authorization attributes.</summary>
    /// <param name="operation">The operation being documented.</param>
    /// <param name="context">The current operation and document metadata.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodAttributes = context.MethodInfo.GetCustomAttributes(inherit: true);
        var controllerAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(inherit: true) ?? [];
        var attributes = methodAttributes.Concat(controllerAttributes).ToArray();

        // AllowAnonymous overrides an Authorize attribute on the controller or action.
        if (attributes.OfType<AllowAnonymousAttribute>().Any() ||
            !attributes.OfType<AuthorizeAttribute>().Any())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
    }
}

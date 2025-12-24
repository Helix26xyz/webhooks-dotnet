using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace webhooks.ApiService.src.filters
{
    /// <summary>
    /// Action filter that controls enum serialization format based on request header
    /// Header: X-ENUMS-INT=1 returns integer values, otherwise returns string names (default)
    /// </summary>
    public class EnumSerializationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Nothing to do before action executes
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Check if client wants integer enum values
            if (context.HttpContext.Request.Headers.TryGetValue("X-ENUMS-INT", out var headerValue) 
                && headerValue == "1")
            {
                // Client wants integers - wrap result with custom serialization options
                if (context.Result is ObjectResult objectResult && objectResult.Value != null)
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        // Don't add JsonStringEnumConverter - use default integer serialization
                    };

                    context.Result = new JsonResult(objectResult.Value, options)
                    {
                        StatusCode = objectResult.StatusCode
                    };
                }
            }
            // If header not present or not "1", use default string enum serialization (configured globally)
        }
    }
}

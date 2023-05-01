using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace OrderAPI.Middleware;

public class ErrorHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<ErrorHandlingMiddleware> _logger;

  public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
      _logger.LogError(ex, "An unhandled exception occurred");

      var problemDetails = new ProblemDetails
      {
        Status = (int)HttpStatusCode.InternalServerError,
        Title = "An error occurred",
        Detail = ex.Message
      };

      context.Response.StatusCode = problemDetails.Status.Value;
      context.Response.ContentType = "application/json";
      await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
  }
}
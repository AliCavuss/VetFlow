using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using VetFlow.Domain.Common;

namespace VetFlow.Web.Filters;

/// <summary>
/// Central handling of business errors: <see cref="NotFoundException"/> → 404,
/// <see cref="DomainException"/> → back to the previous page with the message (MVC) or 400 ProblemDetails (API).
/// </summary>
public sealed class DomainExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var isApi = context.HttpContext.Request.Path.StartsWithSegments("/api");

        if (context.Exception is NotFoundException notFound)
        {
            context.Result = isApi
                ? new NotFoundObjectResult(new ProblemDetails { Title = "Not found", Detail = notFound.Message, Status = 404 })
                : new NotFoundResult();
            context.ExceptionHandled = true;
        }
        else if (context.Exception is DomainException domain)
        {
            if (isApi)
            {
                context.Result = new BadRequestObjectResult(new ProblemDetails { Title = "Business rule violation", Detail = domain.Message, Status = 400 });
            }
            else
            {
                tempDataFactory.GetTempData(context.HttpContext)["Error"] = domain.Message;
                context.Result = new RedirectResult(LocalReferer(context.HttpContext));
            }

            context.ExceptionHandled = true;
        }
    }

    private static string LocalReferer(HttpContext http)
    {
        var referer = http.Request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var uri)
               && string.Equals(uri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? uri.PathAndQuery
            : "/";
    }
}

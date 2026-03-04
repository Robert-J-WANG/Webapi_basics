using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Middlewares;

public class OrderExceptionHandler (IProblemDetailsService problemDetailsService):IExceptionHandler{

public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
{
    // 这里做“异常 → 状态码”的映射（跟第7章一样，只是换了入口）
    int status;
    string title;
    string detail;

    switch (exception)
    {
        case OrderNotFoundException:
            status = StatusCodes.Status404NotFound;
            title = "Not Found";
            detail = exception.Message;
            break;

        case OrderConflictException:
            status = StatusCodes.Status409Conflict;
            title = "Conflict";
            detail = exception.Message;
            break;

        default:
            status = StatusCodes.Status500InternalServerError;
            title = "Internal Server Error";
            detail = "Unexpected error.";
            break;
    }

    httpContext.Response.StatusCode = status;

    var problemContext = new ProblemDetailsContext
    {
        HttpContext = httpContext,
        ProblemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }
    };

    // 让框架按 ProblemDetails 标准输出（content-type/序列化等交给框架）
    await problemDetailsService.WriteAsync(problemContext);

    return true; // 表示异常已经处理完了
}
}
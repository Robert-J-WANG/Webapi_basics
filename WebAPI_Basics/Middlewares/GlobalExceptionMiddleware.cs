using System.Net;
using WebAPI_Basics.Domain;

namespace WebAPI_Basics.Middlewares;

public sealed class GlobalExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 关键：next(context) 代表后续全部流程（路由、Controller、Service、Repo/EF）
            await next(context);
        }
        catch (OrderNotFoundException ex)
        {
            await WritePlainErrorAsync(context, HttpStatusCode.NotFound, ex.Message);
        }
        catch (OrderConflictException ex)
        {
            await WritePlainErrorAsync(context, HttpStatusCode.Conflict, ex.Message);
        }
        catch (Exception)
        {
            // 第9章再加 ILogger 记录细节；本章只做最小可用兜底
            await WritePlainErrorAsync(context, HttpStatusCode.InternalServerError, "Unexpected error.");
        }
    }

    private static async Task WritePlainErrorAsync(HttpContext context, HttpStatusCode status, string message)
    {
        // 如果响应已经开始写（header/body 已部分输出），就不要再强行改状态码
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(message);
    }
}
namespace AIChat.Services;

public class SimpleAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _username;
    private readonly string _password;

    public SimpleAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _username = Environment.GetEnvironmentVariable("LOGIN_USERNAME");
        _password = Environment.GetEnvironmentVariable("LOGIN_PASSWORD");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/static") ||
            context.Request.Path.StartsWithSegments("/_framework") ||
            context.Request.Path.StartsWithSegments("/favicon.ico") ||
            context.Request.Path.StartsWithSegments("/css") ||
            context.Request.Path.StartsWithSegments("/js"))
        {
            await _next(context);
            return;
        }

        if (context.Session.GetString("IsAuthenticated") == "true")
        {
            await _next(context);
            return;
        }

        if (context.Request.Method == "POST" && context.Request.Path == "/login")
        {
            var form = await context.Request.ReadFormAsync();
            var username = form["username"];
            var password = form["password"];
            if (username == _username && password == _password)
            {
                context.Session.SetString("IsAuthenticated", "true");
                context.Response.Redirect("/");
                return;
            }
            else
            {
                await ShowLoginAsync(context, true);
                return;
            }
        }

        if (context.Request.Path == "/login")
        {
            await ShowLoginAsync(context, false);
            return;
        }

        context.Response.Redirect("/login");
    }

    private async Task ShowLoginAsync(HttpContext context, bool failed)
    {
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync($@"
                    <html><body style='background:#f5f5f5;'>
                    <div style='display:flex;justify-content:center;align-items:center;height:100vh;'>
                    <div style='background:white;padding:40px 32px 32px 32px;border-radius:12px;box-shadow:0 4px 32px #0002;min-width:340px;max-width:90vw;'>
                        <h2 style='text-align:center;margin-bottom:24px;'>Login</h2>
                        {(failed ? "<p style='color:red;text-align:center;'>Invalid credentials</p>" : "")}
                        <form method='post' style='display:flex;flex-direction:column;gap:16px;'>
                        <input name='username' placeholder='Username' style='padding:10px;font-size:1.1em;border-radius:6px;border:1px solid #ccc;' autofocus required />
                        <input name='password' type='password' placeholder='Password' style='padding:10px;font-size:1.1em;border-radius:6px;border:1px solid #ccc;' required />
                        <button type='submit' style='padding:12px;font-size:1.1em;border-radius:6px;background:#0078d4;color:white;border:none;cursor:pointer;margin-top:8px;'>Login</button>
                        </form>
                    </div>
                    </div>
                    </body></html>");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SSOLoginService.Api.Services.Interfaces;

namespace SSOLoginService.Api.Controllers;

[AllowAnonymous]
public class WebViewController : Controller
{
    private readonly IEnumerable<ISSOProvider> _ssoProviders;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebViewController> _logger;

    public WebViewController(
        IEnumerable<ISSOProvider> ssoProviders,
        IConfiguration configuration,
        ILogger<WebViewController> logger)
    {
        _ssoProviders = ssoProviders;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("/sso/webview")]
    public async Task<IActionResult> WebView([FromQuery] string? provider = null)
    {
        var ssoProvider = ResolveProvider(provider);
        if (ssoProvider == null)
        {
            return Content("<html><body><h2>پروایدر SSO نامعتبر است</h2></body></html>", "text/html; charset=utf-8");
        }

        var state = Guid.NewGuid().ToString("N");
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/sso/callback?provider={ssoProvider.ProviderType.ToString().ToLower()}&state={state}";
        var loginUrl = await ssoProvider.GetAuthorizationUrlAsync(state, callbackUrl);

        var html = $@"<!DOCTYPE html>
<html lang='fa' dir='rtl'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>در حال انتقال به سامانه احراز هویت</title>
    <style>
        body {{
            font-family: Tahoma, Arial, sans-serif;
            background: #f5f5f5;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            direction: rtl;
        }}
        .loading {{
            text-align: center;
            padding: 40px;
        }}
        .loading h2 {{
            color: #333;
            font-size: 18px;
        }}
        .loading p {{
            color: #888;
            font-size: 13px;
        }}
        .spinner {{
            border: 4px solid #f3f3f3;
            border-top: 4px solid #4361ee;
            border-radius: 50%;
            width: 40px;
            height: 40px;
            animation: spin 1s linear infinite;
            margin: 0 auto 20px;
        }}
        @keyframes spin {{ 0% {{ transform: rotate(0deg); }} 100% {{ transform: rotate(360deg); }} }}
    </style>
</head>
<body>
    <div class='loading'>
        <div class='spinner'></div>
        <h2>در حال انتقال به درگاه احراز هویت</h2>
        <p>لطفاً صبر کنید...</p>
    </div>
    <script>window.location.href = '{loginUrl}';</script>
</body>
</html>";

        return Content(html, "text/html; charset=utf-8");
    }

    private ISSOProvider? ResolveProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return _ssoProviders.FirstOrDefault(p => p.IsActive);

        if (Enum.TryParse<SSOProviderType>(provider, true, out var type))
            return _ssoProviders.FirstOrDefault(p => p.ProviderType == type && p.IsActive);

        return _ssoProviders.FirstOrDefault(p => p.IsActive);
    }
}

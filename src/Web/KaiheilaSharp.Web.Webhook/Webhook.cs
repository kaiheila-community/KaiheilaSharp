// KaiheilaSharp - Kaiheila .NET Libraries
// 
// Copyright (C) 2021 KaiheilaSharpGroup
// 
//  This file is part of KaiheilaSharp Project. It is subject to the
// license terms in the LICENSE file found in the top-level directory
// of this distribution and at https://opensource.org/licenses/MIT.
// No part of KaiheilaSharp Project, including this file, may be copied,
// modified, propagated, or distributed except according to the terms
// contained in the LICENSE file.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KaiheilaSharp.Web.Webhook;

public class Webhook
{
    #region 事件

    public delegate void WebhookMessageReceivedDelegate(string message);
    public static event WebhookMessageReceivedDelegate WebhookMessageReceivedEvent;

    #endregion

    #region 配置项

    private string _key;
    private string _verifyToken;
    private readonly string _endpoint;
    private readonly IPAddress _ipAddress;
    private readonly int _port = -1;
    private Action<WebApplicationBuilder> _webApplicationConfigurator;

    #endregion

    #region 私有共用成员

    private ILogger<RequestDelegate> _logger;

    #endregion

    #region 构造函数

    public Webhook(string endpoint)
    {
        _ipAddress = IPAddress.Loopback;
        _port = 5000;
        _endpoint = endpoint;
    }
    public Webhook(string endpoint, IPAddress ipAddress, int port)
    {
        _ipAddress = ipAddress;
        _port = port;
        _endpoint = endpoint;
    }

    #endregion

    public Webhook WithWebApplicationConfiguration(Action<WebApplicationBuilder> webApplicationConfigurator)
    {
        _webApplicationConfigurator = webApplicationConfigurator;
        return this;
    }
    public Webhook WithEncryptKey(string key)
    {
        _key = key;
        return this;
    }
    public Webhook WithVerifyToken(string verifyToken)
    {
        _verifyToken = verifyToken;
        return this;
    }
    public Webhook Subscribe(WebhookMessageReceivedDelegate @delegate)
    {
        WebhookMessageReceivedEvent += @delegate;
        return this;
    }
    public Task Run()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(_ipAddress, _port);
        });

        _webApplicationConfigurator.Invoke(builder);

        var app = builder.Build();
        _logger = app.Services.GetService<ILogger<RequestDelegate>>();
        app.UseMiddleware<WebhookMiddleware>();
        app.MapPost(_endpoint, async context =>
        {
            await _handleRequest.Invoke(context, _logger, _verifyToken, _key);
        });

        return app.RunAsync();
    }

    #region 请求处理委托

    private readonly Func<HttpContext, ILogger<RequestDelegate>, string, string, Task> _handleRequest = async (context, logger, verifyToken, key) =>
    {
        try
        {
            var data = context.Items["Content"]?.ToString();

            if (data is null)
            {
                throw new Exception("No data received.");
            }

            var json = JsonDocument.Parse(data).RootElement;
            var encrypted = json.TryGetProperty("encrypt", out var encryptString);
            if (encrypted)
            {
                logger.LogDebug("Encrypted message received.");
                var message = await WebhookSecret.Decrypt(encryptString.GetString(), key);
                data = message;
                json = JsonDocument.Parse(message).RootElement;
                logger.LogDebug("Encrypted message decrypt successfully.");
            }
            var isChallenge = json.GetProperty("s").GetInt32() == 0 && json.GetProperty("d").GetProperty("channel_type").GetString() == "WEBHOOK_CHALLENGE";
            if (isChallenge)
            {
                logger.LogInformation("Received a Webhook challenge request.");
                var challenge = json.GetProperty("d").GetProperty("challenge").GetString();
                var token = json.GetProperty("d").GetProperty("verify_token").GetString();
                if (verifyToken is not "" && verifyToken != string.Empty)
                {
                    if (token != verifyToken)
                    {
                        throw new Exception("Token invalid.");
                    }
                }
                context.Response.StatusCode = 200;
                await context.Response.WriteAsJsonAsync(new Dictionary<string,string>
                {
                    {"challenge", challenge}
                });
            }
            else
            {
                context.Response.StatusCode = 200;
                logger.LogInformation("Received a regular message. Trigger webhook message received event.");
                WebhookMessageReceivedEvent?.Invoke(data);
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            logger.LogError("Caught an error when processing the webhook post data: {message}\n{trace}", ex.Message, ex.StackTrace);
        }
    };

    #endregion
}

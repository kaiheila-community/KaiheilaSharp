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
    private readonly int _port;
    private Action<WebApplicationBuilder> _webApplicationConfigurator;

    #endregion

    #region 私有共用成员

    private ILogger<RequestDelegate> _logger;

    #endregion

    #region 构造函数

    /// <summary>
    /// 创建 Webhook 实例，使用默认的监听 IP(127.0.0.1) 和
    /// 端口(5000)设置，API 终结点为 <paramref name="endpoint"/>
    /// </summary>
    /// <param name="endpoint">API 终结点</param>
    public Webhook(string endpoint)
    {
        _ipAddress = IPAddress.Loopback;
        _port = 5000;
        _endpoint = endpoint;
    }

    /// <summary>
    /// 创建 Webhook 实例，设置监听 IP(<paramref name="ipAddress"/>) 和
    /// 端口(<paramref name="port"/>)设置，API 终结点为 <paramref name="endpoint"/>
    /// </summary>
    /// <param name="endpoint">API 终结点</param>
    /// <param name="ipAddress">监听 IP 地址</param>
    /// <param name="port">监听端口</param>
    public Webhook(string endpoint, IPAddress ipAddress, int port)
    {
        _ipAddress = ipAddress;
        _port = port;
        _endpoint = endpoint;
    }

    #endregion

    /// <summary>
    /// 使用 <see cref="WebApplicationBuilder"/> 配置 <see cref="WebApplication"/>
    /// </summary>
    /// <param name="webApplicationConfigurator"><see cref="WebApplicationBuilder"/> Action 委托</param>
    /// <returns>当前 Webhook 实例</returns>
    public Webhook WithWebApplicationConfiguration(Action<WebApplicationBuilder> webApplicationConfigurator)
    {
        _webApplicationConfigurator = webApplicationConfigurator;
        return this;
    }
    /// <summary>
    /// 设置使用加密密钥，如果未设置，收到加密信息时会抛出异常
    /// </summary>
    /// <param name="key">加密密钥</param>
    /// <returns>当前 Webhook 实例</returns>
    public Webhook WithEncryptKey(string key)
    {
        _key = key;
        return this;
    }
    /// <summary>
    /// 设置机器人验证 Token，如果未设置，将不会进行检查
    /// </summary>
    /// <param name="verifyToken">验证 Token</param>
    /// <returns>当前 Webhook 实例</returns>
    public Webhook WithVerifyToken(string verifyToken)
    {
        _verifyToken = verifyToken;
        return this;
    }
    /// <summary>
    /// 订阅消息事件
    /// </summary>
    /// <param name="delegate">消息事件处理委托</param>
    /// <returns></returns>
    public Webhook Subscribe(WebhookMessageReceivedDelegate @delegate)
    {
        WebhookMessageReceivedEvent += @delegate;
        return this;
    }
    /// <summary>
    /// 开启 Kestrel 服务器
    /// </summary>
    /// <returns></returns>
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
                logger.LogError("Received null data from {address}:{p}.",
                    context.Connection.RemoteIpAddress is null ? "NULL" : context.Connection.RemoteIpAddress,
                    context.Connection.RemotePort);
                return;
            }

            var json = JsonDocument.Parse(data).RootElement;
            var encrypted = json.TryGetProperty("encrypt", out var encryptString);
            if (encrypted)
            {
                if (key is "" or null)
                {
                    logger.LogError("Encrypted message received but encryption key is empty.");
                    return;
                }
                logger.LogDebug("Encrypted message received.");
                var message = await WebhookSecret.Decrypt(encryptString.GetString(), key);
                data = message;
                json = JsonDocument.Parse(message).RootElement;
                logger.LogDebug("Encrypted message decrypt successfully.");
            }
            var isChallenge = json.GetProperty("s").GetInt32() == 0 && json.GetProperty("d").GetProperty("channel_type").GetString() == "WEBHOOK_CHALLENGE";
            var requireVerification = json.GetProperty("d").TryGetProperty("verify_token", out var verifyTokenProperty);
            if (verifyToken is not ("" or null) && requireVerification)
            {
                if (verifyTokenProperty.GetString() != verifyToken)
                {
                    logger.LogError("Message verify token invalid. Please check your security system.");
                    return;
                }
            }
            if (isChallenge)
            {
                logger.LogInformation("Received a Webhook challenge request.");
                var challenge = json.GetProperty("d").GetProperty("challenge").GetString();
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

# KaiheilaSharp.Web.Webhook

开黑啦机器人 Webhook 连接库。支持直接 Webhook 连接和通过 [kaiheila/krust](https://hub.docker.com/r/kaiheila/krust) 进行 Websocket 转发后的消息。

若进行直接的 Webhook 连接，建议使用 Nginx 等 Web 服务器进行反向代理和配置 SSL 访问。

若使用 Websocket 转发消息，请不要将端口暴露至公网。

```c#
// 创建一个 Webhook 实例，传入参数为 API 路径，监听 IP 地址，监听端口
// 监听 IP 地址和监听端口为可选参数，不传入则使用默认值 127.0.0.1:5000
// 你可以在 WithWebApplicationConfiguration() 方法中进行进一步配置
// WithWebApplicationConfiguration() 中的配置将会覆盖构造函数中的配置
var webhook = new Webhook("/", IPAddress.Loopback, 5000)
    .WithEncryptKey("你的加密字符串，留空为无加密模式")
    .WithVerifyToken("你的验证密钥")
    .WithWebApplicationConfiguration(builder =>
    {
        // 在此处进行 WebApplication 的配置
        
        // 如果你使用 Serilog，可以在此处进行配置
        // 默认情况下若不进行 Logger 的配置，将不会有日志输出
        builder.Host.UseSerilog();
    })
    // 配置 Event 事件，可以多个串起来
    .Subscribe(message =>
    {
        Log.Logger.Information(message);
    })
    .Subscribe(message =>
    {
        Console.WriteLine(message);
    });

// 运行
await webhook.Run();
```

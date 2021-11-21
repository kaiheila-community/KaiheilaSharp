# KaiheilaSharp.Web.Webhook

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
    .Subscribe(Log.Logger.Information);

await webhook.Run();
```

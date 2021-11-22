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

using System.Diagnostics;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KaiheilaSharp.Web.Webhook;

// Deflate code are from https://github.com/kaiheila-community/kaiheila-dotnet
// Licensed under MIT license

public class WebhookMiddleware
{
    private readonly RequestDelegate _next;
    private const string CompressKey = "compress";

    public WebhookMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ILogger<WebhookMiddleware> logger)
    {
        var s = Stopwatch.StartNew();
        if (context.Request.Query.ContainsKey(CompressKey) && context.Request.Query[CompressKey] == "0")
        {
            using var sr = new StreamReader(context.Request.Body);
            var data = await sr.ReadToEndAsync();
            context.Items["Content"] = data;
            await _next(context);
            s.Stop();
            logger.LogInformation("Response returned in {time} ms", s.ElapsedMilliseconds);
            return;
        }

        // Decompress Deflate
        var stream = new MemoryStream();
        await context.Request.Body.CopyToAsync(stream);
        await context.Request.Body.DisposeAsync();

        // Magic headers of zlib:
        // 78 01 - No Compression/low
        // 78 9C - Default Compression
        // 78 DA - Best Compression
        stream.Position = 2;

        var  deflateStream = new DeflateStream(stream, CompressionMode.Decompress, true);
        var resultStream = new MemoryStream();
        await deflateStream.CopyToAsync(resultStream);
        await deflateStream.DisposeAsync();
        await stream.DisposeAsync();

        // Rewind
        resultStream.Position = 0;

        var reader = new StreamReader(resultStream);
        var result = await reader.ReadToEndAsync();
        await resultStream.DisposeAsync();

        context.Items["Content"] = result;
        reader.Dispose();

        await _next(context);
        s.Stop();
        logger.LogInformation("Response returned in {time} ms", s.ElapsedMilliseconds);
    }
}

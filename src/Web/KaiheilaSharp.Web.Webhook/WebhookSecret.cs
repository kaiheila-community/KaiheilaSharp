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

using System.Security.Cryptography;
using System.Text;

namespace KaiheilaSharp.Web.Webhook;

internal static class WebhookSecret
{
    internal static async Task<string> Decrypt(string secretData, string encryptKey)
    {
        var b64decryptedBytes = Convert.FromBase64String(secretData);
        var b64decrypted = Encoding.UTF8.GetString(b64decryptedBytes);
        var iv = Encoding.UTF8.GetBytes(b64decrypted[..16]);
        var newSecret = b64decrypted[16..];
        var secret = Convert.FromBase64String(newSecret);
        var keyString = encryptKey;
        while (keyString.Length < 32)
        {
            keyString += '\0';
        }

        var key = Encoding.UTF8.GetBytes(keyString);

        var aes = Aes.Create();
        aes.Key = key;
        aes.KeySize = 256;
        await using var memoryStream = new MemoryStream(secret);
        await using var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(key, iv), CryptoStreamMode.Read);
        using var s = new StreamReader(cryptoStream);
        return await s.ReadToEndAsync();
    }
}

using System.Data;
using Flurl.Http;
using Microsoft.Data.Sqlite;

namespace Marisa.Utils;

public static class BrowserCookie
{
    public static CookieJar GetChromeCookies()
    {
        byte[] GetBytes(IDataRecord reader, int columnIndex)
        {
            const int chunkSize = 2 * 1024;

            var  buffer = new byte[chunkSize];
            long bytesRead;
            long fieldOffset = 0;

            using var stream = new MemoryStream();
            while ((bytesRead = reader.GetBytes(columnIndex, fieldOffset, buffer, 0, buffer.Length)) > 0)
            {
                stream.Write(buffer, 0, (int)bytesRead);
                fieldOffset += bytesRead;
            }

            return stream.ToArray();
        }

        var result = new CookieJar();

        // chrome 的 cookie 位置
        var chromeCookiePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +
            @"\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies";

        if (!File.Exists(chromeCookiePath)) return result;

        // 复制一遍，避免数据库锁
        File.Copy(chromeCookiePath, chromeCookiePath + ".copy", true);

        using var conn = new SqliteConnection($"Data Source={chromeCookiePath}.copy;Mode=ReadOnly");
        using var cmd  = conn.CreateCommand();

        cmd.CommandText = "SELECT name,encrypted_value,host_key,path FROM cookies";

        var key = Cipher.AesGcm256.GetKey();

        conn.Open();

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var encryptedData = GetBytes(reader, 1);

            var name = reader.GetString(0);

            if (name.StartsWith("__")) continue;


            var url = $"https://{reader.GetString(2).TrimStart('.')}{reader.GetString(3)}";

            Cipher.AesGcm256.Prepare(encryptedData, out var nonce, out var ciphertextTag);

            var value = Cipher.AesGcm256.Decrypt(ciphertextTag, key, nonce);

            result.AddOrReplace(name, value, url);
        }

        conn.Close();

        return result;
    }
}
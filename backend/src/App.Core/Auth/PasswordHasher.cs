using System.Security.Cryptography;

namespace App.Core.Auth;

/// <summary>
/// 密码哈希技术组件（无状态）：PBKDF2-HMAC-SHA256，随机盐 + 定长比较。
/// 编码格式：<c>PBKDF2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>，入库只存该编码串，禁止明文。
/// </summary>
public sealed class PasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>
    /// 生成密码哈希（每次调用使用新的随机盐）
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>可入库的哈希编码串</returns>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>
    /// 校验明文密码与存储的哈希是否匹配（定长比较，避免计时侧信道）
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="storedHash">入库的哈希编码串</param>
    /// <returns>匹配返回 true；哈希格式非法或为空时返回 false</returns>
    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');
        if (parts.Length != 4
            || !string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

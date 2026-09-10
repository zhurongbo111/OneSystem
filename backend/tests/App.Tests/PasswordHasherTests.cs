using App.Core.Auth;

namespace App.Tests;

/// <summary>
/// 密码哈希组件测试
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher = new();

    [Fact]
    public void Hash_同一密码两次哈希_应产生不同结果且不含明文()
    {
        const string password = "admin123";

        var first = _passwordHasher.Hash(password);
        var second = _passwordHasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain(password, first);
        Assert.StartsWith("PBKDF2$", first);
    }

    [Fact]
    public void Verify_正确密码_应返回true()
    {
        var hash = _passwordHasher.Hash("admin123");

        Assert.True(_passwordHasher.Verify("admin123", hash));
    }

    [Fact]
    public void Verify_错误密码_应返回false()
    {
        var hash = _passwordHasher.Hash("admin123");

        Assert.False(_passwordHasher.Verify("admin124", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("PBKDF2$abc$c2FsdA==$a2V5")]
    [InlineData("PBKDF2$100000$not-base64$not-base64")]
    [InlineData("UNKNOWN$100000$c2FsdA==$a2V5")]
    public void Verify_哈希格式非法_应返回false(string storedHash)
    {
        Assert.False(_passwordHasher.Verify("admin123", storedHash));
    }
}

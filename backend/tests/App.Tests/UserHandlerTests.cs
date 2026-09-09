using System.Security.Claims;
using App.Core.Errors;
using App.Core.Handlers;

namespace App.Tests;

/// <summary>
/// 用户 Handler 测试
/// </summary>
public class UserHandlerTests
{
    [Fact]
    public void GetCurrentUser_含claims_应还原用户()
    {
        var handler = new UserHandler();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "1"),
            new Claim("username", "admin"),
            new Claim("displayName", "管理员"),
        }, "test"));

        var user = handler.GetCurrentUser(principal);

        Assert.Equal("1", user.Id);
        Assert.Equal("admin", user.Username);
        Assert.Equal("管理员", user.DisplayName);
    }

    [Fact]
    public void GetCurrentUser_缺少username_应抛业务异常40100()
    {
        var handler = new UserHandler();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "1") }, "test"));

        var ex = Assert.Throws<BusinessException>(() => handler.GetCurrentUser(principal));

        Assert.Equal(ErrorCode.Unauthorized, ex.Code);
    }
}

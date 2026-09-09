using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Features.Users;
using App.Core.Features.Users.GetCurrentUser;

namespace App.Tests;

/// <summary>
/// 获取当前用户用例处理器测试
/// </summary>
public class GetCurrentUserRequestHandlerTests
{
    private sealed class FakeCurrentUser : ICurrentUser
    {
        public FakeCurrentUser(string id, string username, string displayName)
        {
            Id = id;
            Username = username;
            DisplayName = displayName;
        }

        public string Id { get; }

        public string Username { get; }

        public string DisplayName { get; }
    }

    [Fact]
    public async Task HandleAsync_认证信息完整_应还原用户()
    {
        var handler = new GetCurrentUserRequestHandler(new FakeCurrentUser("1", "admin", "管理员"));

        var user = await handler.HandleAsync(new GetCurrentUserRequest());

        Assert.Equal("1", user.Id);
        Assert.Equal("admin", user.Username);
        Assert.Equal("管理员", user.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_缺少用户名_应抛业务异常40100()
    {
        var handler = new GetCurrentUserRequestHandler(new FakeCurrentUser("1", string.Empty, string.Empty));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => handler.HandleAsync(new GetCurrentUserRequest()));

        Assert.Equal(ErrorCode.Unauthorized, ex.Code);
    }
}

using App.Core.Dtos;
using App.Core.Errors;
using App.Core.Responses;

namespace App.Tests;

/// <summary>
/// 统一响应工厂测试
/// </summary>
public class ApiResponseFactoryTests
{
    [Fact]
    public void Ok_应返回成功码与数据()
    {
        var response = ApiResponseFactory.Ok(new UserDto { Id = "1", Username = "admin", DisplayName = "管理员" });

        Assert.Equal(ErrorCode.Success, response.Code);
        Assert.Equal("success", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("admin", response.Data!.Username);
    }

    [Fact]
    public void Fail_应返回指定错误码与信息()
    {
        var response = ApiResponseFactory.Fail<string>(ErrorCode.LoginFailed, "用户名或密码错误");

        Assert.Equal(ErrorCode.LoginFailed, response.Code);
        Assert.Equal("用户名或密码错误", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public void Fail_无数据重载_应返回失败响应()
    {
        var response = ApiResponseFactory.Fail(ErrorCode.Unauthorized, "未登录或 token 无效");

        Assert.Equal(ErrorCode.Unauthorized, response.Code);
        Assert.Equal("未登录或 token 无效", response.Message);
    }
}

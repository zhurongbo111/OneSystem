using FluentValidation;

namespace App.Core.Mediation;

/// <summary>
/// 非泛型校验器扩展：FluentValidation 的 <see cref="IValidator"/>（非泛型）实例无法直接
/// 与带 T 参数的 <c>ValidateAsync</c> 扩展方法绑定（C# 扩展方法推断不支持"非泛型接收器 + 开放类型参数"的组合）。
/// 本扩展构造 <see cref="ValidationContext{T}"/> 后调用非泛型接口方法
/// <c>IValidator.ValidateAsync(IValidationContext)</c>（校验器基类内部按实例运行时类型做泛型绑定），
/// 供 <see cref="Mediator"/> 全局统一格式校验使用。
/// </summary>
internal static class NonGenericValidatorExtensions
{
    /// <summary>
    /// 对指定实例执行校验（按实例运行时类型泛型绑定）
    /// </summary>
    /// <param name="validator">已注册的校验器（注册类型与实例类型一致）</param>
    /// <param name="instance">待校验的请求实例</param>
    /// <returns>格式校验结果</returns>
    public static Task<FluentValidation.Results.ValidationResult> ValidateAsync(this IValidator validator, object instance)
    {
        var context = new FluentValidation.ValidationContext<object>(instance);
        return validator.ValidateAsync(context);
    }
}

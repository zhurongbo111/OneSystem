using FluentValidation;

namespace App.Core.Features.Batches.UpdateBatchStatus;

/// <summary>
/// 批次停用 / 启用请求格式校验：状态取值 0 / 1
/// </summary>
public sealed class UpdateBatchStatusRequestValidator : AbstractValidator<UpdateBatchStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateBatchStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("批次 id 不能为空");
        RuleFor(x => x.Status).InclusiveBetween(0, 1).WithMessage("状态必须为 0（停用）/ 1（启用）");
    }
}

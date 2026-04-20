namespace SteelForce.Core.Models;

public class ValidationResult
{

    /// <summary>
    /// 关联构件的 GUID
    /// </summary>
    public string ComponentGuid { get; set; } = string.Empty;
    
    /// <summary>
    /// 构件名称（用于可读性）
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// 实际计算出的挠度 v (mm)
    /// 计算公式: v = (5 * q * L⁴) / (384 * E * I)
    /// </summary>
    public double ActualDeflection { get; set; }

    /// <summary>
    /// 允许挠度限值 (mm)
    /// 计算公式: L / DeflectionLimitRatio
    /// </summary>
    public double AllowedDeflection { get; set; }

    /// <summary>
    /// 挠度利用率 (实际/允许)
    /// 比值越接近 1 越接近限值
    /// </summary>
    public double DeflectionUtilizationRatio { get; set; }

    /// <summary>
    /// 校验是否通过
    /// 条件: ActualDeflection <= AllowedDeflection
    /// </summary>
    public bool IsPassed { get; set; }

    /// <summary>
    /// 校验结果消息
    /// 通过时显示成功信息，失败时显示详细原因
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 详细错误原因
    /// 包含具体的技术细节和修正建议
    /// </summary>
    public string? ErrorReason { get; set; }

    /// <summary>
    /// 计算所用的参数记录
    /// 用于审计和调试
    /// </summary>
    public DeflectionCalculationParameters? CalculationParameters { get; set; }

    /// <summary>
    /// 校验时间戳
    /// </summary>
    public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// AI 建议（预留字段）
    /// 用于存储 LLM 生成的修正建议
    /// </summary>
    public string? AIAdvice { get; set; }
    
    /// <summary>
    /// 创建成功的校验结果
    /// </summary>
    public static ValidationResult CreateSuccess(
        string componentGuid,
        string componentName,
        double actualDeflection,
        double allowedDeflection,
        DeflectionCalculationParameters parameters)
    {
        // 计算挠度利用率
        var utilization = allowedDeflection > 0 ? actualDeflection / allowedDeflection : 0;
        // 计算安全距离(允许 - 实际)
        var margin = allowedDeflection - actualDeflection;

        return new ValidationResult
        {
            ComponentGuid = componentGuid,
            ComponentName = componentName,
            ActualDeflection = actualDeflection,
            AllowedDeflection = allowedDeflection,
            DeflectionUtilizationRatio = utilization,
            IsPassed = actualDeflection <= allowedDeflection,
            Message = $"挠度校验通过.实际挠度{actualDeflection:F2}mm, 允许挠度{allowedDeflection:F2}mm, 挠度利用率{utilization:F2}",
            CalculationParameters = parameters,
            ValidationTimestamp = DateTime.UtcNow
        };


    }

    /// <summary>
    /// 创建失败的校验结果
    /// </summary>
    public static ValidationResult CreateFailure(
        string componentGuid,
        string componentName,
        double actualDeflection,
        double allowedDeflection,
        DeflectionCalculationParameters parameters,
        string errorReason)
    {
        var overRatio = allowedDeflection > 0 ? (actualDeflection - allowedDeflection) / allowedDeflection : 0;
        
        return new ValidationResult
        {
            ComponentGuid = componentGuid,
            ComponentName = componentName,
            ActualDeflection = actualDeflection,
            AllowedDeflection = allowedDeflection,
            DeflectionUtilizationRatio = allowedDeflection > 0 ? actualDeflection / allowedDeflection : 0,
            IsPassed = false,
            Message = $"挠度校验不通过。实际挠度 {actualDeflection:F2}mm > 允许值 {allowedDeflection:F2}mm，超限 {overRatio*100:F1}%。",
            ErrorReason = errorReason,
            CalculationParameters = parameters,
            ValidationTimestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建参数缺失的错误结果
    /// </summary>
    public static ValidationResult CreateInvalidParameters(
        string componentGuid,
        string componentName,
        string missingParameters)
    {
        return new ValidationResult
        {
            ComponentGuid = componentGuid,
            ComponentName = componentName,
            IsPassed = false,
            Message = $"无法执行校验，缺少必要参数: {missingParameters}",
            ErrorReason = $"构件缺少以下力学参数: {missingParameters}。请检查 IFC 文件是否包含完整的属性集，或考虑使用标准截面库进行参数补偿。",
            ValidationTimestamp = DateTime.UtcNow
        };
    }
     
}
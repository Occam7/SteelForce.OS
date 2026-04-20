namespace SteelForce.Core.Models;

/// <summary>
/// 挠度计算参数记录
/// 用于审计和调试计算过程
/// </summary>
public class DeflectionCalculationParameters
{
    /// <summary>构件长度 L (mm)</summary>
    public double Length { get; set; }
    
    /// <summary>惯性矩 I (mm⁴)</summary>
    public double MomentOfInertia { get; set; }
    
    /// <summary>弹性模量 E (MPa)</summary>
    public double ElasticModulus { get; set; }
    
    /// <summary>设计线荷载 q (N/mm)</summary>
    public double DesignLoad { get; set; }
    
    /// <summary>挠度限值分母</summary>
    public double DeflectionLimitRatio { get; set; }
    
    /// <summary>使用的计算公式</summary>
    public string Formula { get; set; } = "v = (5 * q * L⁴) / (384 * E * I)";
    
    /// <summary>计算时间戳</summary>
    public DateTime CalculationTimestamp { get; set; } = DateTime.UtcNow;
}
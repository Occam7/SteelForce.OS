namespace SteelForce.Core.Models;

/// <summary>
/// 构件的支撑条件
/// </summary>
public enum SupportCondition
{
    /// <summary>
    /// 简支梁
    /// </summary>
    SimplySupported,
        
    /// <summary>
    /// 悬臂 一头焊丝 一头悬空
    /// </summary>
    Cantilever,
        
    /// <summary>
    /// 两头固定
    /// </summary>
    FixedBothEnds
}
public class BimComponent
{
    /// <summary>
    /// 判定出的支撑条件,决定了计算引擎应选择哪个物理公式
    /// </summary>
    public SupportCondition Support { get; set; } = SupportCondition.SimplySupported;

    /// <summary>
    /// 记录在IFC中侦察到的邻居数量
    /// 用于审计和逻辑回溯
    /// </summary>
    public int ConnectionCount { get; set; }
    
    /// <summary>
    /// 构件全局唯一标识符 (GUID)
    /// </summary>
    public string Guid { get; set; } = string.Empty;

    /// <summary>
    /// 构件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IFC 实体类型 (如 IfcColumn, IfcBeam) 因为类型不同,构件相关的挠度属性不同,所以需要记录类型
    /// </summary>
    public string IfcType { get; set; } = string.Empty;

    /// <summary>
    /// 构件材质 (如 Q235, Q355, Q390)
    /// 用于标准截面库查询和弹性模量 E 的确定
    /// </summary>
    public string Material { get; set; } = string.Empty;

    /// <summary>
    /// 构件长度 (毫米)
    /// </summary>
    public double Length { get; set; }

    /// <summary>
    /// 构件惯性矩 I (毫米^4)
    /// IFC 只存 IfcProfileDef（截面形状） 要么用截面形状计算,要么用SectionType查标准截面库查询
    /// </summary>
    public double MomentOfInertia { get; set; } 

    /// <summary>
    /// 构件弹性模量 E (MPa 或 N/mm²)
    /// 用于标准截面库查询和计算挠度
    /// 在工程经验中通常恒定为 206,000Mpa，但如果模型是铝合金或其他材质，硬编码 E 就会出事故。引入 Material 允许后续挂载材质库。
    /// </summary>
    public double ElasticModulus { get; set; }  

    /// <summary>
    /// 构件设计荷载q (N/mm)
    /// 用于计算挠度
    /// </summary>
    public double DesignLoad { get; set; }       

    /// <summary>
    /// 挠度限值分母。例如填 250，代表允许挠度为 L/250
    /// </summary>
    public double DeflectionLimitRatio { get; set; } = 250;

    /// <summary>
    /// 构件截面类型 (如 H400x200, I20a, L100x10)
    /// 用于标准截面库查询惯性矩 I
    /// </summary>
    public string? SectionType { get; set; } = string.Empty;

    /// <summary>
    /// 标记是否从几何数据计算得到长度
    /// </summary>
    public bool LengthFromGeometry { get; set; }

    /// <summary>
    /// 标记是否从标准截面库查询得到惯性矩
    /// </summary>
    public bool InertiaFromStandardLibrary { get; set; }
    
    /// <summary>
    /// 标记是否从材料库查询得到弹性模量
    /// </summary>
    public bool ElasticModulusFromMaterialLibrary { get; set; }

    /// <summary>
    /// 验证构件是否包含必要的力学参数用于挠度计算
    /// </summary>
    /// <returns>如果参数有效返回 true，否则返回 false</returns>
    public bool HasValidMechanicalProperties()
    {
        return Length > 0 && 
               MomentOfInertia > 0 && 
               ElasticModulus > 0 && 
               DeflectionLimitRatio > 0;
    }

     /// <summary>
    /// 计算允许挠度限值 (mm)
    /// </summary>
    /// <returns>允许挠度值</returns>
    public double GetAllowedDeflection()
    {
        if (DeflectionLimitRatio <= 0) 
        {
            throw new InvalidOperationException($"构件 {Guid} 的挠度限值比例无效: {DeflectionLimitRatio}");
        }
        return Length / DeflectionLimitRatio;
    }

    /// <summary>
    /// 获取构件参数的完整描述，用于日志和调试
    /// </summary>
    /// <returns>参数描述字符串</returns>
    public string GetParameterSummary()
    {
        return $"构件: {Name} ({IfcType}), " +
               $"L={Length}mm, I={MomentOfInertia:E3}mm⁴, E={ElasticModulus}MPa, " +
               $"q={DesignLoad}N/mm, 限值=L/{DeflectionLimitRatio}";
    }
    
}
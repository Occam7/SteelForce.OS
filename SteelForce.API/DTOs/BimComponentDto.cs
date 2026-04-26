namespace SteelForce.API.DTOs;

public class BimComponentDto
{
    public string Guid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IfcType { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double Length { get; set; }
    public double MomentOfInertia { get; set; }
    public double ElasticModulus { get; set; }
    public double DesignLoad { get; set; }
    public double DeflectionLimitRatio { get; set; }
    public string? SectionType { get; set; }
    
    // 新增：物理姿态与连接信息
    public string SupportCondition { get; set; } = string.Empty; // 简支、悬臂等
    public int ConnectionCount { get; set; }

    // 新增：数据来源审计标志位
    public bool LengthFromGeometry { get; set; }
    public bool InertiaFromStandardLibrary { get; set; }
    public bool ElasticModulusFromMaterialLibrary { get; set; }
}
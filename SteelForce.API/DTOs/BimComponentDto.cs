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
    public bool LengthFromGeometry { get; set; }
}

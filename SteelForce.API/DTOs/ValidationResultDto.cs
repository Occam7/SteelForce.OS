namespace SteelForce.API.DTOs;

public class ValidationResultDto
{
    public string ComponentGuid { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public double ActualDeflection { get; set; }
    public double AllowedDeflection { get; set; }
    public double DeflectionUtilizationRatio { get; set; }
    public bool IsPassed { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorReason { get; set; }
    public DateTime ValidationTimestamp { get; set; }
    public string? AIAdvice { get; set; } // AI 修复建议
}

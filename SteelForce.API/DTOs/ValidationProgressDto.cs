namespace SteelForce.API.DTOs;

public class ValidationProgressDto
{
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public double Percentage { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
}

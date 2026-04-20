namespace SteelForce.Core.Models;

public class ValidationProgress
{
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public double Percentage { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
}

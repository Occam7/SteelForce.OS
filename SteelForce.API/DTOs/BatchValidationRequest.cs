namespace SteelForce.API.DTOs;

public class BatchValidationRequest
{
    public List<BimComponentDto> Components { get; set; } = new();
    public int? MaxDegreeOfParallelism { get; set; }
}

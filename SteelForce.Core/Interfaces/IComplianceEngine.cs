namespace SteelForce.Core.Interfaces;

using SteelForce.Core.Models;

public interface IComplianceEngine
{
    ValidationResult Validate(BimComponent component);
    
    Task<IReadOnlyList<ValidationResult>> ValidateBatchAsync(
        IEnumerable<BimComponent> components,
        int? maxDegreeOfParallelism = null,
        IProgress<ValidationProgress>? progress = null);
}
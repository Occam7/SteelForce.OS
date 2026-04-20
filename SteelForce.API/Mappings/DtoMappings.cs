using SteelForce.API.DTOs;
using SteelForce.Core.Models;

namespace SteelForce.API.Mappings;

public static class DtoMappings
{
    public static BimComponentDto ToDto(this BimComponent component)
    {
        return new BimComponentDto
        {
            Guid = component.Guid,
            Name = component.Name,
            IfcType = component.IfcType,
            Material = component.Material,
            Length = component.Length,
            MomentOfInertia = component.MomentOfInertia,
            ElasticModulus = component.ElasticModulus,
            DesignLoad = component.DesignLoad,
            DeflectionLimitRatio = component.DeflectionLimitRatio,
            SectionType = component.SectionType,
            LengthFromGeometry = component.LengthFromGeometry
        };
    }

    public static BimComponent ToModel(this BimComponentDto dto)
    {
        return new BimComponent
        {
            Guid = dto.Guid,
            Name = dto.Name,
            IfcType = dto.IfcType,
            Material = dto.Material,
            Length = dto.Length,
            MomentOfInertia = dto.MomentOfInertia,
            ElasticModulus = dto.ElasticModulus,
            DesignLoad = dto.DesignLoad,
            DeflectionLimitRatio = dto.DeflectionLimitRatio,
            SectionType = dto.SectionType,
            LengthFromGeometry = dto.LengthFromGeometry
        };
    }

    public static ValidationResultDto ToDto(this ValidationResult result)
    {
        return new ValidationResultDto
        {
            ComponentGuid = result.ComponentGuid,
            ComponentName = result.ComponentName,
            ActualDeflection = result.ActualDeflection,
            AllowedDeflection = result.AllowedDeflection,
            DeflectionUtilizationRatio = result.DeflectionUtilizationRatio,
            IsPassed = result.IsPassed,
            Message = result.Message,
            ErrorReason = result.ErrorReason,
            ValidationTimestamp = result.ValidationTimestamp
        };
    }

    public static ValidationProgressDto ToDto(this ValidationProgress progress)
    {
        return new ValidationProgressDto
        {
            ProcessedCount = progress.ProcessedCount,
            TotalCount = progress.TotalCount,
            Percentage = progress.Percentage,
            PassedCount = progress.PassedCount,
            FailedCount = progress.FailedCount
        };
    }
}

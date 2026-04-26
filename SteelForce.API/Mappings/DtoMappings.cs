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
            
            // --- 新增：工业级审计字段 ---
            SupportCondition = component.Support.ToString(), // 将枚举转换为字符串传给前端
            ConnectionCount = component.ConnectionCount,    // 连接数
            LengthFromGeometry = component.LengthFromGeometry,
            InertiaFromStandardLibrary = component.InertiaFromStandardLibrary, // 是否查了标准库
            ElasticModulusFromMaterialLibrary = component.ElasticModulusFromMaterialLibrary // 是否查了材质库
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
            
            // --- 新增：反向映射 ---
            // 将字符串解析回枚举，如果解析失败则默认简支梁
            Support = Enum.TryParse<SupportCondition>(dto.SupportCondition, out var support) 
                      ? support : SupportCondition.SimplySupported,
            ConnectionCount = dto.ConnectionCount,
            LengthFromGeometry = dto.LengthFromGeometry,
            InertiaFromStandardLibrary = dto.InertiaFromStandardLibrary,
            ElasticModulusFromMaterialLibrary = dto.ElasticModulusFromMaterialLibrary
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
            
            // --- 新增：AI 辅助审计字段 ---
            AIAdvice = result.AIAdvice, // 将内核生成的建议传给前端
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
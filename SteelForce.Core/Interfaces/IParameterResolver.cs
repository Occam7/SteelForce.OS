namespace SteelForce.Core.Interfaces;

using SteelForce.Core.Models;

/// <summary>
/// 参数解析器接口
/// 用于补充缺失的力学参数（I, E 等）
/// </summary>
public interface IParameterResolver
{
    bool TryInferSectionType(BimComponent bimComponent, out string sectionType);
    bool TryResolveInertiaFromSectionLibrary(string sectionType, out double inertia);
    bool TryResolveElasticModulusFromMaterialLibrary(string material, out double elasticModulus);
    double GetDefaultElasticModulus(string material);
    double EstimateInertiaFromDimensions(BimComponent component);
}
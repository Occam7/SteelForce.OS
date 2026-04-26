namespace SteelForce.Core.Interfaces;

using SteelForce.Core.Models;

public interface IParameterResolver
{
    // 1. 标准化型号名称
    bool TryInferSectionType(BimComponent bimComponent, out string sectionType);
    
    // 2. 惯性矩入口（内部自动处理 查表 或 公式）
    bool TryResolveInertia(string sectionType, out double inertia);
    
    // 3. 弹性模量入口
    bool TryResolveElasticModulus(string material, out double elasticModulus);
    
    // 4. 获取规范限值（L/250, L/400 等）
    double GetDeflectionLimit(BimComponent component);
    
    // 5. 兜底弹性模量
    double GetDefaultElasticModulus();
}
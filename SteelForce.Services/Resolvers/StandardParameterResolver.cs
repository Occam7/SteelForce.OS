namespace SteelForce.Services.Resolvers;

using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;

/// <summary>
/// 标准参数解析器实现
/// 提供基于规则库的参数补充逻辑
/// </summary>
public class StandardParameterResolver : IParameterResolver
{
    // 标准钢材弹性模量 (MPa)
    private static readonly Dictionary<string, double> ElasticModulusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Q235"] = 206000,
        ["Q235B"] = 206000,
        ["Q235A"] = 206000,
        ["Q355"] = 206000,
        ["Q355B"] = 206000,
        ["Q390"] = 206000,
        ["Q420"] = 206000,
        ["Q460"] = 206000,
        ["S235"] = 210000,  // 欧标
        ["S355"] = 210000,
        ["A36"] = 200000,   // 美标
        ["A572"] = 200000
    };

    // 常见 H 型钢截面惯性矩 (mm⁴) - 简化数据
    private static readonly Dictionary<string, double> HSectionInertiaMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["H100x100"] = 3.86e6,
        ["H125x125"] = 8.38e6,
        ["H150x150"] = 1.62e7,
        ["H175x175"] = 2.86e7,
        ["H200x200"] = 4.72e7,
        ["H250x250"] = 1.08e8,
        ["H300x300"] = 2.02e8,
        ["H350x350"] = 3.33e8,
        ["H400x200"] = 2.37e8,
        ["H400x400"] = 5.24e8,
        ["H500x200"] = 3.82e8,
        ["H500x500"] = 1.09e9,
        ["H600x200"] = 5.38e8,
        ["H600x300"] = 9.92e8,
        ["H700x300"] = 1.42e9,
        ["H800x300"] = 1.94e9,
        ["H900x300"] = 2.55e9
    };

    public bool TryInferSectionType(BimComponent component, out string sectionType)
    {
        sectionType = string.Empty;

        // 1. 优先从构件名称推断
        if (!string.IsNullOrEmpty(component.Name))
        {
            var nameUpper = component.Name.ToUpperInvariant();

            // 尝试匹配 H 型钢命名模式 (如 H400x200, H400*200, H 400x200)
            var hPattern = System.Text.RegularExpressions.Regex.Match(nameUpper, @"H[\s_]*([0-9]+)[\s_xX*×]([0-9]+)");
            if (hPattern.Success)
            {
                var height = hPattern.Groups[1].Value;
                var width = hPattern.Groups[2].Value;
                sectionType = $"H{height}x{width}";
                return true;
            }

            // 尝试匹配 I 型钢
            var iPattern = System.Text.RegularExpressions.Regex.Match(nameUpper, @"I([0-9]+)");
            if (iPattern.Success)
            {
                sectionType = $"I{iPattern.Groups[1].Value}";
                return true;
            }
        }

        // 2. 从已有 SectionType 推断
        if (!string.IsNullOrEmpty(component.SectionType))
        {
            var sectionUpper = component.SectionType.ToUpperInvariant();

            // 标准化 H 型钢命名
            var hMatch = System.Text.RegularExpressions.Regex.Match(sectionUpper, @"H[\s_]*([0-9]+)[\s_xX*×]([0-9]+)");
            if (hMatch.Success)
            {
                sectionType = $"H{hMatch.Groups[1].Value}x{hMatch.Groups[2].Value}";
                return true;
            }
        }

        // 3. 从构件尺寸推断
        if (component.Length > 0)
        {
            // 这里可以根据长度和其他几何信息推断截面
            // 简化的逻辑：如果构件名称包含特定关键词
            if (!string.IsNullOrEmpty(component.Name))
            {
                var nameLower = component.Name.ToLowerInvariant();
                if (nameLower.Contains("column") || nameLower.Contains("柱"))
                {
                    // 默认柱使用 H400x400
                    sectionType = "H400x400";
                    return true;
                }
                else if (nameLower.Contains("beam") || nameLower.Contains("梁"))
                {
                    // 默认梁使用 H400x200
                    sectionType = "H400x200";
                    return true;
                }
            }
        }

        return !string.IsNullOrEmpty(sectionType);
    }

    public bool TryResolveInertiaFromSectionLibrary(string sectionType, out double inertia)
    {
        inertia = 0;

        if (string.IsNullOrEmpty(sectionType))
            return false;

        // 标准化截面类型命名
        var normalizedType = sectionType.ToUpperInvariant().Replace(" ", "").Replace("*", "x").Replace("X", "x").Replace("×", "x");

        // 从内置库查询
        if (HSectionInertiaMap.TryGetValue(normalizedType, out inertia))
        {
            return true;
        }

        // 尝试模糊匹配
        var partialMatch = HSectionInertiaMap
            .Keys
            .FirstOrDefault(k => normalizedType.Contains(k) || k.Contains(normalizedType));

        if (partialMatch != null)
        {
            inertia = HSectionInertiaMap[partialMatch];
            return true;
        }

        // 如果无法从库中找到，尝试根据截面尺寸估算
        var dimensionMatch = System.Text.RegularExpressions.Regex.Match(normalizedType, @"H(\d+)x(\d+)");
        if (dimensionMatch.Success)
        {
            var height = double.Parse(dimensionMatch.Groups[1].Value);
            var width = double.Parse(dimensionMatch.Groups[2].Value);

            // 简化的惯性矩估算公式：I ≈ (b * h^3) / 12 (矩形截面近似)
            // 对于 H 型钢，使用更复杂的近似
            var approxInertia = (width * Math.Pow(height, 3)) / 12 * 0.8; // 0.8 是形状系数
            inertia = approxInertia;
            return true;
        }

        return false;
    }

    public bool TryResolveElasticModulusFromMaterialLibrary(string material, out double elasticModulus)
    {
        elasticModulus = 0;

        if (string.IsNullOrEmpty(material))
            return false;

        // 标准化材料名称
        var normalizedMaterial = material.ToUpperInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

        // 从内置库查询
        if (ElasticModulusMap.TryGetValue(normalizedMaterial, out elasticModulus))
        {
            return true;
        }

        // 尝试部分匹配
        var partialMatch = ElasticModulusMap
            .Keys
            .FirstOrDefault(k => normalizedMaterial.Contains(k) || k.Contains(normalizedMaterial));

        if (partialMatch != null)
        {
            elasticModulus = ElasticModulusMap[partialMatch];
            return true;
        }

        return false;
    }

    public double GetDefaultElasticModulus(string material)
    {
        if (string.IsNullOrEmpty(material))
            return 206000; // 默认 Q235 的弹性模量

        if (TryResolveElasticModulusFromMaterialLibrary(material, out var elasticModulus))
        {
            return elasticModulus;
        }

        // 根据材料名称推断
        var materialUpper = material.ToUpperInvariant();

        if (materialUpper.Contains("Q195") || materialUpper.Contains("Q215") || materialUpper.Contains("Q235"))
            return 206000;

        if (materialUpper.Contains("Q275") || materialUpper.Contains("Q295") ||
            materialUpper.Contains("Q345") || materialUpper.Contains("Q355") ||
            materialUpper.Contains("Q390") || materialUpper.Contains("Q420") ||
            materialUpper.Contains("Q460") || materialUpper.Contains("Q500") ||
            materialUpper.Contains("Q550") || materialUpper.Contains("Q620") ||
            materialUpper.Contains("Q690"))
            return 206000; // 中国钢结构设计标准规定所有钢材 E = 206000 MPa

        if (materialUpper.Contains("S235") || materialUpper.Contains("S275") ||
            materialUpper.Contains("S355"))
            return 210000; // 欧标钢材

        if (materialUpper.Contains("A36") || materialUpper.Contains("A572") ||
            materialUpper.Contains("A992"))
            return 200000; // 美标钢材

        // 默认返回 Q235 的弹性模量
        return 206000;
    }

    public double EstimateInertiaFromDimensions(BimComponent component)
    {
        // 如果没有截面类型信息，无法估算
        if (string.IsNullOrEmpty(component.SectionType))
            return 0;

        // 尝试解析 H 型钢尺寸
        var hMatch = System.Text.RegularExpressions.Regex.Match(
            component.SectionType.ToUpperInvariant(),
            @"H(\d+)x(\d+)");

        if (hMatch.Success)
        {
            var height = double.Parse(hMatch.Groups[1].Value);
            var width = double.Parse(hMatch.Groups[2].Value);

            // H 型钢惯性矩近似公式（强轴）
            // I_x ≈ (B * H^3 - (B - t_w) * (H - 2t_f)^3) / 12
            // 简化估算：I ≈ 0.8 * (B * H^3) / 12
            var estimatedI = 0.8 * (width * Math.Pow(height, 3)) / 12;
            return estimatedI;
        }

        // 尝试解析 I 型钢尺寸
        var iMatch = System.Text.RegularExpressions.Regex.Match(
            component.SectionType.ToUpperInvariant(),
            @"I(\d+)");

        if (iMatch.Success)
        {
            var height = double.Parse(iMatch.Groups[1].Value);
            // I 型钢惯性矩近似估算
            // 假设宽度约为高度的 0.5 倍
            var width = height * 0.5;
            var estimatedI = 0.7 * (width * Math.Pow(height, 3)) / 12;
            return estimatedI;
        }

        return 0;
    }
}

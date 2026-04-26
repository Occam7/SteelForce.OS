using System.Text.Json;
using System.Text.RegularExpressions;
using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;

namespace SteelForce.Services.Resolvers;

// 1. 数据模具
public class SteelSection
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double H { get; set; }
    public double B { get; set; }
    public double t1 { get; set; }
    public double t2 { get; set; }
    public double Ix { get; set; } // mm^4
}

public class MaterialRecord
{
    public string Name { get; set; } = string.Empty;
    public double E { get; set; }
    public double Density { get; set; } // 预留密度字段，方便后期计算自重
}

public class StandardParameterResolver : IParameterResolver
{
    private Dictionary<string, SteelSection> _sectionLibrary = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, double> _materialLibrary = new(StringComparer.OrdinalIgnoreCase);
    
    
    public StandardParameterResolver(string sectionJsonPath, string materialJsonPath)
    {
        LoadMaterialLibrary(materialJsonPath);
        LoadSectionLibrary(sectionJsonPath);
    }

    private void LoadSectionLibrary(string sectionJsonPath)
    {
        try
        {
            if (!File.Exists(sectionJsonPath)) return;
            var json = File.ReadAllText(sectionJsonPath);
            var sections = JsonSerializer.Deserialize<List<SteelSection>>(json);
            if (sections != null) _sectionLibrary = sections.ToDictionary(s => s.Name, s => s);
        }
        catch (Exception e) { Console.WriteLine($"[Error] 截面库加载失败: {e.Message}"); }
    }

    private void LoadMaterialLibrary(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath)) return;
            var json = File.ReadAllText(jsonPath);
            var materials = JsonSerializer.Deserialize<List<MaterialRecord>>(json);
            if (materials != null)
            {
                _materialLibrary = materials.ToDictionary(m => m.Name, m => m.E);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Error] 材质库加载失败: {e.Message}");
        }
    }

    // --- 业务逻辑 1：型号标准化 ---
    public bool TryInferSectionType(BimComponent component, out string sectionType)
    {
        sectionType = string.Empty;
        string source = !string.IsNullOrEmpty(component.SectionType) ? component.SectionType : component.Name;
        if (string.IsNullOrEmpty(source)) return false;

        var text = source.ToUpperInvariant().Replace(" ", "");

        // 识别 H 型钢（HW, HM, HN, HT, HP）
        var hMatch = Regex.Match(text, @"(H[WMNT P]?)[\s_]*([0-9]+)[\s_xX*×]([0-9]+)");
        if (hMatch.Success)
        {
            sectionType = $"H{hMatch.Groups[2].Value}x{hMatch.Groups[3].Value}";
            return true;
        }
        return false;
    }

    // --- 业务逻辑 2：惯性矩核心（查表+公式） ---
    public bool TryResolveInertia(string sectionType, out double inertia)
    {
        inertia = 0;
        if (string.IsNullOrEmpty(sectionType)) return false;

        var key = sectionType.ToUpperInvariant().Replace(" ", "").Replace("*", "x");

        // A. 优先查表（219条权威数据）
        if (_sectionLibrary.TryGetValue(key, out var section))
        {
            inertia = section.Ix;
            return true;
        }

        // B. 物理公式反算（针对非标件）
        return TryInferInertiaByFormula(key, out inertia);
    }

    private bool TryInferInertiaByFormula(string key, out double inertia)
    {
        inertia = 0;
        var match = Regex.Match(key, @"H(\d+)X(\d+)X([\d.]+)X([\d.]+)");
        if (match.Success)
        {
            double h = double.Parse(match.Groups[1].Value);
            double b = double.Parse(match.Groups[2].Value);
            double t1 = double.Parse(match.Groups[3].Value);
            double t2 = double.Parse(match.Groups[4].Value);
            
            // 物理公式：$$I_x = \frac{Bh^3 - (B-t_1)(H-2t_2)^3}{12}$$
            inertia = (b * Math.Pow(h, 3) - (b - t1) * Math.Pow(h - 2 * t2, 3)) / 12.0;
            return true;
        }
        return false;
    }

    // --- 业务逻辑 3：材质与限值 ---
    public bool TryResolveElasticModulus(string material, out double elasticModulus)
    {
        elasticModulus = 0;
        if (string.IsNullOrEmpty(material)) return false;
        var normalized = material.ToUpperInvariant().Replace(" ", "").Replace("-", "");
        if (_materialLibrary.TryGetValue(normalized, out elasticModulus))
        {
            return true;
        }

        return false;
    }

    public double GetDefaultElasticModulus() => 206000;

    public double GetDeflectionLimit(BimComponent component)
    {
        var name = component.Name?.ToUpperInvariant() ?? "";
        if (name.Contains("吊车") || name.Contains("CRANE")) return 800; // L/800
        if (name.Contains("主梁") || name.Contains("GIRDER")) return 400; // L/400
        return 250; // 默认 L/250
    }
}
// SteelForce.OS - 调试追踪程序
// 详细追踪 XbimParser 和 StandardParameterResolver 的数据流
//
// 使用方法:
// 1. 复制此代码到 SteelForce.OS/SteelForce.Console/Program.cs
// 2. 修改 filePath 变量指向你的 IFC 文件
// 3. 运行项目

using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;
using SteelForce.Services.Parsers;
using SteelForce.Services.Resolvers;

Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("           SteelForce.OS - 数据流调试追踪工具");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

// 配置
var filePath = "/Users/jayden/RiderProjects/SteelForce.OS/data/Building-Architecture.ifc";
var maxComponentsToDebug = 3; // 只调试前 3 个构件，避免输出过多

if (!File.Exists(filePath))
{
    Console.WriteLine($"❌ 文件不存在: {filePath}");
    Console.WriteLine("请修改 filePath 变量指向正确的 IFC 文件路径");
    Console.ReadKey();
    return;
}

Console.WriteLine($"📁 目标文件: {filePath}");
Console.WriteLine($"🔢 调试构件数量: 前 {maxComponentsToDebug} 个");
Console.WriteLine();

try
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           阶段 1: 创建服务实例");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("📦 创建 StandardParameterResolver...");
    var parameterResolver = new StandardParameterResolver();
    Console.WriteLine("✅ StandardParameterResolver 创建成功");
    Console.WriteLine();

    Console.WriteLine("📦 创建 XbimParser (注入 StandardParameterResolver)...");
    var parser = new XbimParser(parameterResolver);
    Console.WriteLine("✅ XbimParser 创建成功");
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           阶段 2: 解析 IFC 文件");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("🔍 开始解析 IFC 文件...");
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var components = parser.Parse(filePath).ToList();
    stopwatch.Stop();

    Console.WriteLine($"✅ 解析完成！");
    Console.WriteLine($"   • 找到 {components.Count} 个构件");
    Console.WriteLine($"   • 耗时: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine();

    if (components.Count == 0)
    {
        Console.WriteLine("⚠️  未找到任何构件");
        Console.ReadKey();
        return;
    }

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           阶段 3: 详细追踪每个构件的数据流程");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    for (int i = 0; i < Math.Min(components.Count, maxComponentsToDebug); i++)
    {
        var component = components[i];
        Console.WriteLine($"────────────────────────────────────────────────────────────");
        Console.WriteLine($"🔍 构件 {i + 1}: {component.Name}");
        Console.WriteLine($"────────────────────────────────────────────────────────────");
        Console.WriteLine();

        Console.WriteLine("📋 1. 从 XbimParser 获取的原始数据:");
        Console.WriteLine("   ├─ GUID:        " + component.Guid);
        Console.WriteLine("   ├─ 名称:        " + component.Name);
        Console.WriteLine("   ├─ IFC 类型:    " + component.IfcType);
        Console.WriteLine("   ├─ 材料:        " + (string.IsNullOrEmpty(component.Material) ? "(未找到)" : component.Material));
        Console.WriteLine("   ├─ 长度:        " + (component.Length > 0 ? $"{component.Length:F0} mm" : "(未计算)"));
        Console.WriteLine("   ├─ 截面类型:    " + (string.IsNullOrEmpty(component.SectionType) ? "(未找到)" : component.SectionType));
        Console.WriteLine();

        Console.WriteLine("🔧 2. StandardParameterResolver 参数解析:");
        Console.WriteLine();

        Console.WriteLine("   a) 推断截面类型:");
        var inferResult = parameterResolver.TryInferSectionType(component, out var inferredSection);
        Console.WriteLine("      ├─ 输入材料: " + (string.IsNullOrEmpty(component.Material) ? "(null)" : component.Material));
        Console.WriteLine("      ├─ 输入名称: " + (string.IsNullOrEmpty(component.Name) ? "(null)" : component.Name));
        Console.WriteLine("      ├─ 推断结果: " + (inferResult ? "✅ 成功" : "❌ 失败"));
        Console.WriteLine("      └─ 推断截面: " + (string.IsNullOrEmpty(inferredSection) ? "(null)" : inferredSection));
        Console.WriteLine();

        Console.WriteLine("   b) 从材料库获取弹性模量 E:");
        var eResult = parameterResolver.TryResolveElasticModulusFromMaterialLibrary(component.Material ?? "", out var resolvedE);
        var defaultE = parameterResolver.GetDefaultElasticModulus(component.Material ?? "");
        Console.WriteLine("      ├─ 输入材料: " + (string.IsNullOrEmpty(component.Material) ? "(null)" : component.Material));
        Console.WriteLine("      ├─ 库查询结果: " + (eResult ? "✅ 命中" : "❌ 未命中"));
        Console.WriteLine("      ├─ 库查询值: " + (eResult ? $"{resolvedE:F0} MPa" : "(无)"));
        Console.WriteLine("      └─ 默认值:     " + $"{defaultE:F0} MPa");
        Console.WriteLine();

        Console.WriteLine("   c) 从截面库获取惯性矩 I:");
        var iResult = parameterResolver.TryResolveInertiaFromSectionLibrary(inferredSection, out var resolvedI);
        var estimatedI = parameterResolver.EstimateInertiaFromDimensions(component);
        Console.WriteLine("      ├─ 输入截面: " + (string.IsNullOrEmpty(inferredSection) ? "(null)" : inferredSection));
        Console.WriteLine("      ├─ 库查询结果: " + (iResult ? "✅ 命中" : "❌ 未命中"));
        Console.WriteLine("      ├─ 库查询值: " + (iResult ? $"{resolvedI:E2} mm⁴" : "(无)"));
        Console.WriteLine("      └─ 估算值:     " + (estimatedI > 0 ? $"{estimatedI:E2} mm⁴" : "(无法估算)"));
        Console.WriteLine();

        Console.WriteLine("📊 3. 最终 BimComponent 状态:");
        Console.WriteLine("   ├─ 弹性模量 E: " + (component.ElasticModulus > 0 ? $"{component.ElasticModulus:F0} MPa" : "(未设置)"));
        Console.WriteLine("   ├─ 惯性矩 I:   " + (component.MomentOfInertia > 0 ? $"{component.MomentOfInertia:E2} mm⁴" : "(未设置)"));
        Console.WriteLine("   ├─ 设计荷载 q: " + (component.DesignLoad > 0 ? $"{component.DesignLoad:F2} N/mm" : "(未设置)"));
        Console.WriteLine("   └─ 限值分母:   " + (component.DeflectionLimitRatio > 0 ? $"L/{component.DeflectionLimitRatio:F0}" : "(未设置)"));
        Console.WriteLine();
    }

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           阶段 4: 所有构件汇总");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();

    Console.WriteLine("📋 构件列表汇总:");
    Console.WriteLine(new string('-', 140));
    Console.WriteLine($"{"#",-4} {"GUID",-38} {"名称",-25} {"类型",-12} {"长度(mm)",-12} {"材质",-15} {"E(MPa)",-10}");
    Console.WriteLine(new string('-', 140));

    for (int i = 0; i < components.Count; i++)
    {
        var c = components[i];
        var lengthStr = c.Length > 0 ? $"{c.Length:F0}" : "-";
        var materialStr = string.IsNullOrEmpty(c.Material) ? "-" : c.Material;
        var eStr = c.ElasticModulus > 0 ? $"{c.ElasticModulus:F0}" : "-";

        Console.WriteLine($"{i + 1,-4} {c.Guid,-38} {c.Name,-25} {c.IfcType,-12} {lengthStr,-12} {materialStr,-15} {eStr,-10}");
    }

    Console.WriteLine(new string('-', 140));
    Console.WriteLine();

    Console.WriteLine("📈 统计信息:");
    Console.WriteLine("   • 总构件数: " + components.Count);
    Console.WriteLine("   • 有材料:   " + components.Count(c => !string.IsNullOrEmpty(c.Material)));
    Console.WriteLine("   • 有长度:   " + components.Count(c => c.Length > 0));
    Console.WriteLine("   • 有 E:      " + components.Count(c => c.ElasticModulus > 0));
    Console.WriteLine("   • 有 I:      " + components.Count(c => c.MomentOfInertia > 0));
    Console.WriteLine();

    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           调试完成！");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine("           ❌ 发生错误");
    Console.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.WriteLine($"错误信息: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("堆栈跟踪:");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine();
}
Console.WriteLine();
Console.WriteLine("按任意键退出...");
Console.ReadKey();

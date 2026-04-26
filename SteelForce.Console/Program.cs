// // SteelForce.OS - 调试追踪程序
// // 详细追踪 XbimParser 和 StandardParameterResolver 的数据流
// //
// // 使用方法:
// // 1. 复制此代码到 SteelForce.OS/SteelForce.Console/Program.cs
// // 2. 修改 filePath 变量指向你的 IFC 文件
// // 3. 运行项目
//
// using SteelForce.Core.Interfaces;
// using SteelForce.Core.Models;
// using SteelForce.Services.Parsers;
// using SteelForce.Services.Resolvers;
//
// System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
// System.System.Console.WriteLine("           SteelForce.OS - 数据流调试追踪工具");
// System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
// System.System.Console.WriteLine();
//
// // 配置
// var filePath = "/Users/jayden/RiderProjects/SteelForce.OS/data/Building-Architecture.ifc";
// var maxComponentsToDebug = 3; // 只调试前 3 个构件，避免输出过多
//
// if (!File.Exists(filePath))
// {
//     System.System.Console.WriteLine($"❌ 文件不存在: {filePath}");
//     System.System.Console.WriteLine("请修改 filePath 变量指向正确的 IFC 文件路径");
//     Console.ReadKey();
//     return;
// }
//
// System.System.Console.WriteLine($"📁 目标文件: {filePath}");
// System.System.Console.WriteLine($"🔢 调试构件数量: 前 {maxComponentsToDebug} 个");
// System.System.Console.WriteLine();
//
// try
// {
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           阶段 1: 创建服务实例");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("📦 创建 StandardParameterResolver...");
//     var parameterResolver = new StandardParameterResolver();
//     System.System.Console.WriteLine("✅ StandardParameterResolver 创建成功");
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("📦 创建 XbimParser (注入 StandardParameterResolver)...");
//     var parser = new XbimParser(parameterResolver);
//     System.System.Console.WriteLine("✅ XbimParser 创建成功");
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           阶段 2: 解析 IFC 文件");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("🔍 开始解析 IFC 文件...");
//     var stopwatch = System.Diagnostics.Stopwatch.StartNew();
//     var components = parser.Parse(filePath).ToList();
//     stopwatch.Stop();
//
//     System.System.Console.WriteLine($"✅ 解析完成！");
//     System.System.Console.WriteLine($"   • 找到 {components.Count} 个构件");
//     System.System.Console.WriteLine($"   • 耗时: {stopwatch.ElapsedMilliseconds}ms");
//     System.System.Console.WriteLine();
//
//     if (components.Count == 0)
//     {
//         System.System.Console.WriteLine("⚠️  未找到任何构件");
//         Console.ReadKey();
//         return;
//     }
//
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           阶段 3: 详细追踪每个构件的数据流程");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine();
//
//     for (int i = 0; i < Math.Min(components.Count, maxComponentsToDebug); i++)
//     {
//         var component = components[i];
//         System.System.Console.WriteLine($"────────────────────────────────────────────────────────────");
//         System.System.Console.WriteLine($"🔍 构件 {i + 1}: {component.Name}");
//         System.System.Console.WriteLine($"────────────────────────────────────────────────────────────");
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("📋 1. 从 XbimParser 获取的原始数据:");
//         System.System.Console.WriteLine("   ├─ GUID:        " + component.Guid);
//         System.System.Console.WriteLine("   ├─ 名称:        " + component.Name);
//         System.System.Console.WriteLine("   ├─ IFC 类型:    " + component.IfcType);
//         System.System.Console.WriteLine("   ├─ 材料:        " + (string.IsNullOrEmpty(component.Material) ? "(未找到)" : component.Material));
//         System.System.Console.WriteLine("   ├─ 长度:        " + (component.Length > 0 ? $"{component.Length:F0} mm" : "(未计算)"));
//         System.System.Console.WriteLine("   ├─ 截面类型:    " + (string.IsNullOrEmpty(component.SectionType) ? "(未找到)" : component.SectionType));
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("🔧 2. StandardParameterResolver 参数解析:");
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("   a) 推断截面类型:");
//         var inferResult = parameterResolver.TryInferSectionType(component, out var inferredSection);
//         System.System.Console.WriteLine("      ├─ 输入材料: " + (string.IsNullOrEmpty(component.Material) ? "(null)" : component.Material));
//         System.System.Console.WriteLine("      ├─ 输入名称: " + (string.IsNullOrEmpty(component.Name) ? "(null)" : component.Name));
//         System.System.Console.WriteLine("      ├─ 推断结果: " + (inferResult ? "✅ 成功" : "❌ 失败"));
//         System.System.Console.WriteLine("      └─ 推断截面: " + (string.IsNullOrEmpty(inferredSection) ? "(null)" : inferredSection));
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("   b) 从材料库获取弹性模量 E:");
//         var eResult = parameterResolver.TryResolveElasticModulusFromMaterialLibrary(component.Material ?? "", out var resolvedE);
//         var defaultE = parameterResolver.GetDefaultElasticModulus(component.Material ?? "");
//         System.System.Console.WriteLine("      ├─ 输入材料: " + (string.IsNullOrEmpty(component.Material) ? "(null)" : component.Material));
//         System.System.Console.WriteLine("      ├─ 库查询结果: " + (eResult ? "✅ 命中" : "❌ 未命中"));
//         System.System.Console.WriteLine("      ├─ 库查询值: " + (eResult ? $"{resolvedE:F0} MPa" : "(无)"));
//         System.System.Console.WriteLine("      └─ 默认值:     " + $"{defaultE:F0} MPa");
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("   c) 从截面库获取惯性矩 I:");
//         var iResult = parameterResolver.TryResolveInertiaFromSectionLibrary(inferredSection, out var resolvedI);
//         var estimatedI = parameterResolver.EstimateInertiaFromDimensions(component);
//         System.System.Console.WriteLine("      ├─ 输入截面: " + (string.IsNullOrEmpty(inferredSection) ? "(null)" : inferredSection));
//         System.System.Console.WriteLine("      ├─ 库查询结果: " + (iResult ? "✅ 命中" : "❌ 未命中"));
//         System.System.Console.WriteLine("      ├─ 库查询值: " + (iResult ? $"{resolvedI:E2} mm⁴" : "(无)"));
//         System.System.Console.WriteLine("      └─ 估算值:     " + (estimatedI > 0 ? $"{estimatedI:E2} mm⁴" : "(无法估算)"));
//         System.System.Console.WriteLine();
//
//         System.System.Console.WriteLine("📊 3. 最终 BimComponent 状态:");
//         System.System.Console.WriteLine("   ├─ 弹性模量 E: " + (component.ElasticModulus > 0 ? $"{component.ElasticModulus:F0} MPa" : "(未设置)"));
//         System.System.Console.WriteLine("   ├─ 惯性矩 I:   " + (component.MomentOfInertia > 0 ? $"{component.MomentOfInertia:E2} mm⁴" : "(未设置)"));
//         System.System.Console.WriteLine("   ├─ 设计荷载 q: " + (component.DesignLoad > 0 ? $"{component.DesignLoad:F2} N/mm" : "(未设置)"));
//         System.System.Console.WriteLine("   └─ 限值分母:   " + (component.DeflectionLimitRatio > 0 ? $"L/{component.DeflectionLimitRatio:F0}" : "(未设置)"));
//         System.System.Console.WriteLine();
//     }
//
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           阶段 4: 所有构件汇总");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("📋 构件列表汇总:");
//     System.System.Console.WriteLine(new string('-', 140));
//     System.System.Console.WriteLine($"{"#",-4} {"GUID",-38} {"名称",-25} {"类型",-12} {"长度(mm)",-12} {"材质",-15} {"E(MPa)",-10}");
//     System.System.Console.WriteLine(new string('-', 140));
//
//     for (int i = 0; i < components.Count; i++)
//     {
//         var c = components[i];
//         var lengthStr = c.Length > 0 ? $"{c.Length:F0}" : "-";
//         var materialStr = string.IsNullOrEmpty(c.Material) ? "-" : c.Material;
//         var eStr = c.ElasticModulus > 0 ? $"{c.ElasticModulus:F0}" : "-";
//
//         System.System.Console.WriteLine($"{i + 1,-4} {c.Guid,-38} {c.Name,-25} {c.IfcType,-12} {lengthStr,-12} {materialStr,-15} {eStr,-10}");
//     }
//
//     System.System.Console.WriteLine(new string('-', 140));
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("📈 统计信息:");
//     System.System.Console.WriteLine("   • 总构件数: " + components.Count);
//     System.System.Console.WriteLine("   • 有材料:   " + components.Count(c => !string.IsNullOrEmpty(c.Material)));
//     System.System.Console.WriteLine("   • 有长度:   " + components.Count(c => c.Length > 0));
//     System.System.Console.WriteLine("   • 有 E:      " + components.Count(c => c.ElasticModulus > 0));
//     System.System.Console.WriteLine("   • 有 I:      " + components.Count(c => c.MomentOfInertia > 0));
//     System.System.Console.WriteLine();
//
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           调试完成！");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
// }
// catch (Exception ex)
// {
//     System.System.Console.WriteLine();
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine("           ❌ 发生错误");
//     System.System.Console.WriteLine("═══════════════════════════════════════════════════════════════");
//     System.System.Console.WriteLine();
//     System.System.Console.WriteLine($"错误信息: {ex.Message}");
//     System.System.Console.WriteLine();
//     System.System.Console.WriteLine("堆栈跟踪:");
//     System.System.Console.WriteLine(ex.StackTrace);
//     System.System.Console.WriteLine();
// }
// System.System.Console.WriteLine();
// System.System.Console.WriteLine("按任意键退出...");
// Console.ReadKey();
using System;
using System.Linq;
using Xbim.Ifc;
using Xbim.Common;
using Xbim.Ifc.Validation;

using System.Text.Json;
using System;
namespace SteelForce.Console
{
    
    public class SteelSection
    {
        public string Category { get; set; } = string.Empty; // HW, HM等 [cite: 115]
        public string Name { get; set; } = string.Empty;     // 型号名称 [cite: 116]
        public double H { get; set; }                        // 高度 (mm) [cite: 116]
        public double B { get; set; }                        // 宽度 (mm) [cite: 117]
        public double Ix { get; set; }                       // 强轴惯性矩 (mm⁴) [cite: 118]
    }
    
    class Program
    {
        static void Main(string[] args)
        {


            // 1. 定位文件路径 (Rider 编译后文件在 bin 目录下) 
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "SteelLibrary.json");

            System.Console.WriteLine($"[开始测试] 正在读取路径: {jsonPath}");

            try 
            {
                // 2. 检查文件是否存在 [cite: 107]
                if (!File.Exists(jsonPath))
                {
                    System.Console.WriteLine("❌ 错误：找不到 JSON 文件！请检查 Rider 的 'Copy to Output Directory' 设置。");
                    return;
                }

                // 3. 读取并反序列化 [cite: 107]
                string jsonContent = File.ReadAllText(jsonPath);
                var sections = JsonSerializer.Deserialize<List<SteelSection>>(jsonContent);

                if (sections != null)
                {
                    System.Console.WriteLine($"✅ 成功加载 {sections.Count} 条截面数据！\n");
                    System.Console.WriteLine("------------------------------------------------------------");
                    System.Console.WriteLine($"{"分类",-10} | {"型号",-15} | {"高度",-10} | {"惯性矩 Ix (mm⁴)",-20}");
                    System.Console.WriteLine("------------------------------------------------------------");

                    // 4. 输出前 5 条数据进行验证 
                    foreach (var s in sections.Take(5))
                    {
                        // 使用标准格式化输出，确保数据对齐
                        System.Console.WriteLine($"{s.Category,-10} | {s.Name,-15} | {s.H,8} | {s.Ix,18:N0}");
                    }
                    System.Console.WriteLine("------------------------------------------------------------");
                }
            }
            catch (Exception ex)
            {
                // 记录错误但保证程序不直接崩溃 [cite: 133, 134]
                System.Console.WriteLine($"❌ 测试运行中发生异常: {ex.Message}");
            }
        }
    }
}
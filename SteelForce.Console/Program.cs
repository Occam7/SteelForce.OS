using System.Text;
using SteelForce.Core.Models;
using SteelForce.Services.Compliance;
using SteelForce.Services.Parsers;
using SteelForce.Services.Resolvers;

// 1. 基础配置
// 确保 JSON 文件在 Infrastructure/Data 目录下，并且属性设置为“如果较新则复制”
string baseDir = AppDomain.CurrentDomain.BaseDirectory;
string sectionJson = Path.Combine(baseDir, "Data", "SteelLibrary.json");
string materialJson = Path.Combine(baseDir, "Data", "MaterialLibrary.json");

// 请将此路径指向你刚才保存的测试 IFC 文件
string ifcFilePath = "/Users/jayden/RiderProjects/SteelForce.OS/data/Test.ifc";

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("=== SteelForce.OS 工业审计级测试启动 ===");
Console.WriteLine($"[配置] 截面库: {Path.GetFileName(sectionJson)}");
Console.WriteLine($"[配置] 材质库: {Path.GetFileName(materialJson)}");
Console.WriteLine("------------------------------------------");

try
{
    // 2. 初始化核心组件
    // 参数解析器：负责查表和公式反算
    var resolver = new StandardParameterResolver(sectionJson, materialJson);
    
    // IFC 解析器：负责从模型提取几何、属性和连接关系
    var parser = new XbimParser(resolver);
    
    // 合规性引擎：负责根据姿态执行物理计算
    var engine = new DeflectionComplianceEngine();

    // 3. 执行解析
    Console.WriteLine($"[1/3] 正在解析 IFC 文件: {Path.GetFileName(ifcFilePath)}...");
    var components = parser.Parse(ifcFilePath).ToList();
    Console.WriteLine($"[OK] 提取到 {components.Count} 个结构构件。");

    // 4. 执行合规性批量校验
    Console.WriteLine("[2/3] 正在执行多线程批量校验...");
    var results = await engine.ValidateBatchAsync(components);

    // 5. 输出审计报告
    Console.WriteLine("[3/3] 校验完成，生成详细结果：\n");
    Console.WriteLine("{0,-25} | {1,-12} | {2,-15} | {3,-10} | {4,-10}", 
        "构件名称 (GUID)", "物理姿态", "使用的公式", "实际挠度", "结论");
    Console.WriteLine(new string('-', 85));

    foreach (var res in results)
    {
        string status = res.IsPassed ? "✅ 合格" : "❌ 不合格";
        
        // 提取计算参数中的公式，验证是否触发了 (qL^4)/8EI
        string formula = res.CalculationParameters?.Formula ?? "未知";
        string support = components.First(c => c.Guid == res.ComponentGuid).Support.ToString();

        Console.WriteLine("{0,-25} | {1,-12} | {2,-15} | {3,10:F2}mm | {4,-10}", 
            $"{res.ComponentName} ({res.ComponentGuid.Substring(0,8)})", 
            support,
            formula.Replace("v = ", "").Replace(" ", ""), // 简化显示
            res.ActualDeflection, 
            status);

        if (!res.IsPassed)
        {
            Console.WriteLine($"   > 原因: {res.Message} (允许值: {res.AllowedDeflection:F2}mm)");
        }
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n[FATAL ERROR] 程序运行中断: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Console.ResetColor();
}

Console.WriteLine("\n------------------------------------------");
Console.WriteLine("测试结束，按任意键退出...");
Console.ReadKey();
using SteelForce.Core.Interfaces;
using SteelForce.Services.Compliance;
using SteelForce.Services.Parsers;
using SteelForce.Services.Resolvers;
using System.Reflection;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. 配置控制器，并增加对枚举字符串的支持（让 API 返回的 SupportCondition 更具可读性）
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SteelForce.OS API",
        Version = "v1",
        Description = "钢结构BIM挠度合规校验系统API (工业审计增强版)"
    });
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// 2. 依赖注入配置

// 解析层保持 Scoped，确保每个文件解析请求都有独立的 Xbim 环境
builder.Services.AddScoped<IIfcParser, XbimParser>();

// 关键修改：将参数解析器改为 Singleton
// 理由：截面库加载涉及 I/O 且数据量大，单例模式可实现内存缓存，确保 O(1) 查询效率
// 关键修改：同时传入截面库和材质库的路径
builder.Services.AddSingleton<IParameterResolver>(sp => 
{
    // 定义数据目录
    var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
    
    // 解析两个 JSON 文件的路径
    var sectionPath = Path.Combine(dataDir, "SteelLibrary.json");
    var materialPath = Path.Combine(dataDir, "MaterialLibrary.json");
    
    // 调用具有 2 个形参的构造函数
    return new StandardParameterResolver(sectionPath, materialPath);
});

// 合规引擎保持 Scoped，处理并发校验任务
builder.Services.AddScoped<IComplianceEngine, DeflectionComplianceEngine>();

var app = builder.Build();

// 3. 中间件管道配置
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SteelForce.OS API v1");
    });
}

// 如果之后要对接微信小程序或其他前端，建议在此处配置 CORS
// app.UseCors(opt => opt.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
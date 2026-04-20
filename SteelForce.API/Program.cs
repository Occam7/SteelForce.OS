using SteelForce.Core.Interfaces;
using SteelForce.Services.Compliance;
using SteelForce.Services.Parsers;
using SteelForce.Services.Resolvers;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SteelForce.OS API",
        Version = "v1",
        Description = "钢结构BIM挠度合规校验系统API"
    });
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddScoped<IIfcParser, XbimParser>();
builder.Services.AddScoped<IParameterResolver, StandardParameterResolver>();
builder.Services.AddScoped<IComplianceEngine, DeflectionComplianceEngine>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SteelForce.OS API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
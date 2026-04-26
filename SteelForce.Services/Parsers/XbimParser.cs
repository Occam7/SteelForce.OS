using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace SteelForce.Services.Parsers;

public class XbimParser : IIfcParser
{
    private readonly IParameterResolver _parameterResolver;

    public XbimParser(IParameterResolver parameterResolver)
    {
        _parameterResolver = parameterResolver;
    }

    public IEnumerable<BimComponent> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"IFC 文件不存在: {filePath}");
        }

        using var model = IfcStore.Open(filePath);
        var components = new List<BimComponent>();

        var structuralElements = model.Instances
            .OfType<IIfcBuildingElement>()
            .Where(e => IsStructuralSteelElement(e));

        foreach (var element in structuralElements)
        {
            var component = ParseElement(element, model);
            if (component != null)
            {
                components.Add(component);
            }
        }

        return components;
    }

    private bool IsStructuralSteelElement(IIfcBuildingElement element)
    {
        var ifcType = element.GetType().Name.ToUpperInvariant();

        var supportedTypes = new[]
        {
            "IFCCOLUMN",
            "IFCBEAM",
            "IFCBRACE",
            "IFCMEMBER",
            "IFCGIRDER",
            "IFCPILE",
            "IFCFOOTING"
        };

        return supportedTypes.Any(t => ifcType.Contains(t));
    }

    private BimComponent? ParseElement(IIfcBuildingElement element, IModel model)
    {
        var component = new BimComponent
        {
            Guid = element.GlobalId,
            Name = element.Name?.ToString() ?? "Unknown",
            IfcType = element.GetType().Name.Replace("Ifc", "")
        };
        
        ExtractProperties(element, component);
        ExtractMaterial(element, component);
        DetectSupportCondition(element, component);

        double? finalLength = GetLengthFromQto(element)
                              ?? GetLengthFromStandardPset(element)
                              ?? GetLengthFromFallback(element);

        if (finalLength.HasValue && finalLength.Value > 0)
        {
            component.Length = ConvertToMillimeters(finalLength.Value, model);
            component.LengthFromGeometry = false;
        }
        else
        {
            double? geoLen = GetLengthFromGeometry(element, model);
            if (geoLen.HasValue && geoLen.Value > 0)
            {
                component.Length = geoLen.Value;
                component.LengthFromGeometry = true;
            }
        }
        
        ResolveMissingParameters(component);
        return component;
    }

    private void ResolveMissingParameters(BimComponent component)
    {
        // 识别型号
        if (string.IsNullOrEmpty(component.SectionType))
        {
            if (_parameterResolver.TryInferSectionType(component, out var inferredSection))
            {
                component.SectionType = inferredSection;
            }
        }
        
        // 调用材质库添加弹性模量
        if (component.ElasticModulus <= 0)
        {
            if (!_parameterResolver.TryResolveElasticModulus(component.Material ?? "", out var resolvedE))
            {
                component.ElasticModulus = _parameterResolver.GetDefaultElasticModulus();
            }
            else
            {
                component.ElasticModulus = resolvedE;
            }
        }
        
        // 惯性矩
        if (component.MomentOfInertia <= 0)
        {
            _parameterResolver.TryResolveInertia(component.SectionType ?? "", out var resolvedI);
            component.MomentOfInertia = resolvedI;
        }

        if (component.DesignLoad <= 0)
        {
            component.DesignLoad = 1.0;
        }

        if (component.DeflectionLimitRatio <= 0 || component.DeflectionLimitRatio == 250)
        {
            component.DeflectionLimitRatio = _parameterResolver.GetDeflectionLimit(component);
        }
    }

    private void ExtractProperties(IIfcBuildingElement element, BimComponent component)
    {
        try
        {
            var props = element.IsDefinedBy
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>().SelectMany(p => p.HasProperties).OfType<IIfcPropertySingleValue>();

            foreach (var prop in props)
            {
                string name = prop?.Name.ToString()?.ToUpperInvariant() ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                var rawValue = prop.NominalValue?.Value;
                if (rawValue == null) continue;

                try
                {
                    // 惯性矩 (I)
                    if (name.Contains("INERTIA") || name.Contains("MOMENT"))
                    {
                        component.MomentOfInertia = Convert.ToDouble(rawValue);
                    }
                    // 弹性模量 (E)
                    else if (name.Contains("ELASTIC") || name.Contains("MODULUS"))
                    {
                        component.ElasticModulus = Convert.ToDouble(rawValue);
                    }
                    // 设计荷载 (q)
                    else if (name.Contains("LOAD"))
                    {
                        component.DesignLoad = Convert.ToDouble(rawValue);
                    }
                    // 挠度限值 (L/250, L/1000等)
                    else if (name.Contains("LIMIT") || name.Contains("DEFLECTION"))
                    {
                        component.DeflectionLimitRatio = Convert.ToDouble(rawValue);
                    }
                    // 截面与材质索引
                    else if (name.Contains("SECTION") || name.Contains("PROFILE"))
                    {
                        component.SectionType = rawValue.ToString();
                    }
                    else if (name.Contains("MATERIAL"))
                    {
                        component.Material = rawValue.ToString();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[数据转换异常] 构件: {element.GlobalId}, 属性: {name}, 原因: {ex.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[属性提取异常] 构件: {element.GlobalId}, 原因: {e.Message}");
        }
    }

    private void ExtractMaterial(IIfcBuildingElement element, BimComponent component)
    {
        try
        {
            var materialAssociations = element.HasAssociations
                .OfType<IIfcRelAssociatesMaterial>();

            foreach (var matAssoc in materialAssociations)
            {
                var material = matAssoc.RelatingMaterial;

                if (material is IIfcMaterialLayerSetUsage layerSetUsage)
                {
                    var layerSet = layerSetUsage.ForLayerSet;
                    var layer = layerSet?.MaterialLayers.FirstOrDefault();
                    if (layer?.Material != null)
                    {
                        try
                        {
                            if (layer.Material.Name != null)
                            {
                                component.Material = layer.Material.Name.ToString();
                            }
                            else
                            {
                                component.Material = "Unknown";
                            }
                            return;
                        }
                        catch
                        {
                            component.Material = "Unknown";
                        }
                    }
                }
                else if (material is IIfcMaterial singleMaterial)
                {
                    try
                    {
                        if (singleMaterial.Name != null)
                        {
                            component.Material = singleMaterial.Name.ToString();
                        }
                        else
                        {
                            component.Material = "Unknown";
                        }
                        return;
                    }
                    catch
                    {
                        component.Material = "Unknown";
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[材质提取异常] 构件: {element.GlobalId}, 原因: {e.Message}");
            component.Material = "Error_Check_Model"; 
        }
    }

    
    private double? GetLengthFromGeometry(IIfcBuildingElement element, IModel model)
    {
        if (element?.Representation == null) return null;

        try
        {
            var axisRep = element.Representation.Representations
                .FirstOrDefault(r => r.RepresentationIdentifier?.ToString().ToUpper() == "AXIS");

            if (axisRep != null)
            {
                var axisLength = CalculateAxisLength(axisRep, model);
                if (axisLength.HasValue && axisLength.Value > 0) return axisLength;
            }

            foreach (var rep in element.Representation.Representations)
            {
                foreach (var item in rep.Items)
                {
                    if (item is IIfcExtrudedAreaSolid extruded)
                    {
                        return CalculateLengthFromExtrusion(extruded, model, element.GlobalId);
                    }
                    else if (item is IIfcBooleanResult booleanResult)
                    {
                        return CalculateLengthFromBooleanResult(booleanResult, model, element.GlobalId);
                    }
                }
            }

            return null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[几何解析异常] 构件: {element.GlobalId}, 原因: {e.Message}");
            return null;
        }
    }

    private double? CalculateLengthFromExtrusion(IIfcExtrudedAreaSolid extrudedSolid, IModel model, string globalId)
    {
        try
        {
            var depth = extrudedSolid.Depth;
            if (depth > 0)
            {
                return ConvertToMillimeters(depth, model);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[几何细节异常] 构件: {globalId}, 类型: ExtrudedAreaSolid, 原因: {e.Message}");
        }

        return null;
    }

    private double? CalculateLengthFromBooleanResult(IIfcBooleanResult booleanResult, IModel model, string globalId)
    {
        try
        {
            if (booleanResult.FirstOperand is IIfcExtrudedAreaSolid firstExtruded)
            {
                return CalculateLengthFromExtrusion(firstExtruded, model, globalId);
            }
            else if (booleanResult.FirstOperand is IIfcBooleanResult nestedResult)
            {
                return CalculateLengthFromBooleanResult(nestedResult, model, globalId);
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"[几何细节异常] 构件: {globalId}, 类型: BooleanResult, 原因: {e.Message}");
        }

        return null;
    }

    private double ConvertToMillimeters(double rawValue, IModel model)
    {
        return (rawValue / model.ModelFactors.OneMetre) * 1000.0;
    }

    private void DetectSupportCondition(IIfcBuildingElement element, BimComponent component)
    {
        try
        {
            var allRels = element.ConnectedTo.Concat(element.ConnectedFrom);
            var neighbors = allRels
                .Select(rel => rel.RelatingElement == element ? rel.RelatedElement : rel.RelatingElement)
                .Where(neighbor => neighbor != null)
                .Select(neighbor => neighbor.GlobalId.ToString())
                .Distinct();
            int count = neighbors.Count();
            component.ConnectionCount = count;
            if (count == 1)
            {
                component.Support = SupportCondition.Cantilever;
            }
            else if (count >= 2)
            {
                component.Support = SupportCondition.SimplySupported;
            }
            else
            {
                component.Support = SupportCondition.SimplySupported;
            }

            
        }
        catch (Exception e)
        {
            Console.WriteLine($"[警告] 构件 {element.GlobalId} ({element.Name}) 连接关系解析失败。");
            Console.WriteLine($"原因: {e.Message}");

            component.Support = SupportCondition.SimplySupported;
            component.ConnectionCount = 0;
        }
    }

    private double? GetLengthFromQto(IIfcBuildingElement element)
    {
        if (element == null) return null;
        
        try
        {
            var qto = element.IsDefinedBy
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcElementQuantity>()
                .FirstOrDefault(q => 
                    q.Name.ToString().ToUpper().Contains("BEAMBASEQUANTITIES") || 
                    q.Name.ToString().ToUpper().Contains("COLUMNBASEQUANTITIES"));

            if (qto == null) return null;

            var lengthQuantity = qto.Quantities
                .OfType<IIfcQuantityLength>()
                .FirstOrDefault(q => q.Name.ToString().ToUpper() == "LENGTH");

            return lengthQuantity?.LengthValue;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[警告] 构件 {element?.GlobalId ?? "Unknown"} 的 Qto 提取失败: {e.Message}");
            return null;
        }
    }

    private double? GetLengthFromStandardPset(IIfcBuildingElement element)
    {
        if (element == null) return null;
        
        try
        {
            var pSet = element.IsDefinedBy
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>()
                .FirstOrDefault(p => 
                    p.Name.ToString().ToUpper().Contains("BEAMCOMMON") || 
                    p.Name.ToString().ToUpper().Contains("COLUMNCOMMON"));

            if (pSet == null) return null;

            var prop = pSet.HasProperties
                .OfType<IIfcPropertySingleValue>()
                .FirstOrDefault(p => 
                    p.Name.ToString().ToUpper() == "SPAN" || 
                    p.Name.ToString().ToUpper() == "LENGTH");

            var rawValue = prop?.NominalValue?.Value;
            return rawValue != null ? Convert.ToDouble(rawValue) : (double?)null;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[警告] 构件 {element?.GlobalId ?? "Unknown"} 的 Pset 提取失败: {e.Message}");
            return null;
        }
    }

    private double? GetLengthFromFallback(IIfcBuildingElement element)
    {
        if (element == null) return null;

        try
        {
            var candidateProps = element.IsDefinedBy
                .Where(r => r.RelatingPropertyDefinition != null) 
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>()
                .SelectMany(p => p.HasProperties)
                .OfType<IIfcPropertySingleValue>()
                .ToList(); 

            var foundValues = new Dictionary<string, double>();

            foreach (var prop in candidateProps)
            {
                string name = prop.Name.ToString().ToUpper();
    
                if ((name.Contains("LENGTH") || name.Contains("SPAN")) && !name.Contains("HEIGHT"))
                {
                    var rawValue = prop.NominalValue?.Value;
                    if (rawValue != null)
                    {
                        try
                        {
                            double val = Convert.ToDouble(rawValue);
                            if (val > 0) foundValues[name] = val;
                        }
                        catch { continue; }
                    }
                }
            }

            if (foundValues.Count == 0) return null;
        
            return foundValues.Keys.Any(k => k == "LENGTH") 
                ? foundValues["LENGTH"] 
                : foundValues.Values.Max();
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Fallback 严重异常] ID: {element?.GlobalId ?? "Unknown"}, 原因: {e.Message}");
            return null;
        }
    }

    private double? CalculateAxisLength(IIfcRepresentation axisRep, IModel model)
    {
        double totalDist = 0;
        bool found = false;

        foreach (var item in axisRep.Items)
        {
            if (item is IIfcPolyline polyline)
            {
                var pts = polyline.Points;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    totalDist += CalculateDistance(pts[i], pts[i + 1]);
                    found = true;
                }
            }
        }

        return found ? ConvertToMillimeters(totalDist, model) : (double?)null;
    }

    private double CalculateDistance(IIfcCartesianPoint p1, IIfcCartesianPoint p2)
    {
        var d1 = p1.Coordinates[0] - p2.Coordinates[0];
        var d2 = p1.Coordinates[1] - p2.Coordinates[1];
        var d3 = (p1.Coordinates.Count > 2 && p2.Coordinates.Count > 2) ? p1.Coordinates[2] - p2.Coordinates[2] : 0;
        return Math.Sqrt(d1 * d1 + d2 * d2 + d3 * d3);
    }
}

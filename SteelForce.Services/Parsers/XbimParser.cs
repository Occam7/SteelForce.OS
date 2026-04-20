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
            Name = "Unknown",
            IfcType = element.GetType().Name.Replace("Ifc", "")
        };

        try
        {
            if (element.Name != null)
            {
                component.Name = element.Name.ToString();
            }
        }
        catch
        {
            component.Name = "Unknown";
        }

        ExtractProperties(element, component, model);
        ExtractMaterial(element, component);

        double? lengthFromProperty = GetLengthFromProperties(element, model);
        double? lengthFromGeometry = null;

        if (lengthFromProperty.HasValue && lengthFromProperty.Value > 0)
        {
            component.Length = ConvertToMillimeters(lengthFromProperty.Value, model);
            component.LengthFromGeometry = false;
        }
        else
        {
            lengthFromGeometry = GetLengthFromGeometry(element, model);
            if (lengthFromGeometry.HasValue && lengthFromGeometry.Value > 0)
            {
                component.Length = lengthFromGeometry.Value;
                component.LengthFromGeometry = true;
            }
        }

        ResolveMissingParameters(component);
        return component;
    }

    private void ResolveMissingParameters(BimComponent component)
    {

        if (string.IsNullOrEmpty(component.SectionType))
        {
            if (_parameterResolver.TryInferSectionType(component, out var inferredSection))
            {
                component.SectionType = inferredSection;
            }
        }

        if (component.ElasticModulus <= 0)
        {
            if (_parameterResolver.TryResolveElasticModulusFromMaterialLibrary(component.Material ?? "", out var resolvedE))
            {
                component.ElasticModulus = resolvedE;
            }
            else
            {
                component.ElasticModulus = _parameterResolver.GetDefaultElasticModulus(component.Material ?? "");
            }
        }

        if (component.MomentOfInertia <= 0)
        {
            if (_parameterResolver.TryResolveInertiaFromSectionLibrary(component.SectionType ?? "", out var resolvedI))
            {
                component.MomentOfInertia = resolvedI;
            }
            else
            {
                component.MomentOfInertia = _parameterResolver.EstimateInertiaFromDimensions(component);
            }
        }

        if (component.DesignLoad <= 0)
        {
            component.DesignLoad = 1.0;
        }

        if (component.DeflectionLimitRatio <= 0)
        {
            component.DeflectionLimitRatio = 250.0;
        }
    }

    private void ExtractProperties(IIfcBuildingElement element, BimComponent component, IModel model)
    {
        try
        {
            var propertySets = element.IsDefinedBy
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>();

            foreach (var pset in propertySets)
            {
                foreach (var prop in pset.HasProperties.OfType<IIfcPropertySingleValue>())
                {
                    string propName = "";
                    try
                    {
                        if (prop.Name != null)
                        {
                            propName = prop.Name.ToString().ToUpperInvariant();
                        }
                    }
                    catch
                    {
                        continue;
                    }

                    var nominalValue = prop.NominalValue;
                    var propValue = prop.NominalValue?.Value;
                    if (propValue == null) continue;

                    var valueStr = propValue.ToString() ?? "";

                    if (propName.Contains("INERTIA") || propName.Contains("MOMENT"))
                    {
                        if (double.TryParse(valueStr, out var inertia))
                        {
                            component.MomentOfInertia = inertia;
                        }
                    }
                    else if (propName.Contains("ELASTIC") || propName.Contains("MODULUS"))
                    {
                        if (double.TryParse(valueStr, out var elasticModulus))
                        {
                            component.ElasticModulus = elasticModulus;
                        }
                    }
                    else if (propName.Contains("LOAD"))
                    {
                        if (double.TryParse(valueStr, out var load))
                        {
                            component.DesignLoad = load;
                        }
                    }
                    else if (propName.Contains("LIMIT") || propName.Contains("DEFLECTION"))
                    {
                        if (double.TryParse(valueStr, out var limit))
                        {
                            component.DeflectionLimitRatio = limit;
                        }
                    }
                    else if (propName.Contains("SECTION") || propName.Contains("PROFILE"))
                    {
                        component.SectionType = valueStr;
                    }
                    else if (propName.Contains("MATERIAL"))
                    {
                        component.Material = valueStr;
                    }
                }
            }
        }
        catch
        {
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
        catch
        {
        }
    }

    private double? GetLengthFromProperties(IIfcBuildingElement element, IModel model)
    {
        try
        {
            var properties = element.IsDefinedBy
                .Select(r => r.RelatingPropertyDefinition)
                .OfType<IIfcPropertySet>()
                .SelectMany(p => p.HasProperties)
                .OfType<IIfcPropertySingleValue>();

            foreach (var prop in properties)
            {
                string propName = "";
                try
                {
                    if (prop.Name != null)
                    {
                        propName = prop.Name.ToString().ToUpperInvariant();
                    }
                }
                catch
                {
                    continue;
                }

                var propValue = prop.NominalValue?.Value;
                if (propValue == null) continue;

                var valueStr = propValue.ToString() ?? "";

                if (propName.Contains("LENGTH") || propName.Contains("HEIGHT"))
                {
                    if (double.TryParse(valueStr, out var length))
                    {
                        return ConvertToMillimeters(length, model);
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private double? GetLengthFromGeometry(IIfcBuildingElement element, IModel model)
    {
        try
        {
            var representation = element.Representation;
            if (representation == null) return null;

            foreach (var repItem in representation.Representations.SelectMany(r => r.Items))
            {
                if (repItem is IIfcExtrudedAreaSolid extrudedSolid)
                {
                    var length = CalculateLengthFromExtrusion(extrudedSolid, model);
                    if (length.HasValue) return length;
                }
                else if (repItem is IIfcBooleanResult booleanResult)
                {
                    var length = CalculateLengthFromBooleanResult(booleanResult, model);
                    if (length.HasValue) return length;
                }
            }

            return 3000;
        }
        catch
        {
            return null;
        }
    }

    private double? CalculateLengthFromExtrusion(IIfcExtrudedAreaSolid extrudedSolid, IModel model)
    {
        try
        {
            var depth = extrudedSolid.Depth;
            if (depth > 0)
            {
                return ConvertToMillimeters(depth, model);
            }
        }
        catch
        {
        }

        return null;
    }

    private double? CalculateLengthFromBooleanResult(IIfcBooleanResult booleanResult, IModel model)
    {
        try
        {
            if (booleanResult.FirstOperand is IIfcExtrudedAreaSolid firstExtruded)
            {
                return CalculateLengthFromExtrusion(firstExtruded, model);
            }
            else if (booleanResult.FirstOperand is IIfcBooleanResult nestedResult)
            {
                return CalculateLengthFromBooleanResult(nestedResult, model);
            }
        }
        catch
        {
        }

        return null;
    }

    private double ConvertToMillimeters(double rawValue, IModel model)
    {
        return (rawValue / model.ModelFactors.OneMetre) * 1000.0;
    }
}

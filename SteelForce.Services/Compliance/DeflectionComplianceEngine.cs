namespace SteelForce.Services.Compliance;

using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

public class DeflectionComplianceEngine : IComplianceEngine
{
        /// <summary>
    /// 批量验证构件（使用 Parallel.ForEach 实现高性能）
    /// </summary>
    /// <param name="components">构件列表</param>
    /// <param name="maxDegreeOfParallelism">最大并行度（默认使用所有 CPU 核心）</param>
    /// <param name="progress">进度报告接口</param>
    /// <returns>校验结果列表</returns>
    public async Task<IReadOnlyList<ValidationResult>> ValidateBatchAsync(
        IEnumerable<BimComponent> components,
        int? maxDegreeOfParallelism = null,
        IProgress<ValidationProgress>? progress = null)
    {
        var componentList = components.ToList();
        var totalCount = componentList.Count;
        
        var stopwatch = Stopwatch.StartNew();
        var results = new ConcurrentBag<(int index, ValidationResult result)>();
        var processedCount = 0;

        // 配置并行选项
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount
        };

        try
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(
                    componentList.Select((c, i) => (index: i, component: c)),
                    parallelOptions,
                    item =>
                    {
                        try
                        {
                            var result = Validate(item.component);
                            results.Add((item.index, result));

                            // 报告进度
                            var currentCount = Interlocked.Increment(ref processedCount);
                            if (progress != null && currentCount % 100 == 0)
                            {
                                progress.Report(new ValidationProgress
                                {
                                    ProcessedCount = currentCount,
                                    TotalCount = totalCount,
                                    Percentage = (double)currentCount / totalCount * 100,
                                    PassedCount = results.Count(r => r.result.IsPassed),
                                    FailedCount = results.Count(r => !r.result.IsPassed)
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // _logger?.LogError(ex, "处理构件时发生错误");
                            results.Add((item.index, ValidationResult.CreateInvalidParameters(
                                item.component.Guid,
                                item.component.Name,
                                ex.Message)));
                        }
                    });
            });
        }
        catch (OperationCanceledException)
        {
            // _logger?.LogWarning("批量校验操作被取消");
            throw;
        }

        stopwatch.Stop();

        // 排序并返回结果
        var sortedResults = results
            .OrderBy(r => r.index)
            .Select(r => r.result)
            .ToList();

        var passedCount = sortedResults.Count(r => r.IsPassed);
        var failedCount = sortedResults.Count - passedCount;

        // // // _logger?.LogInformation(
        // //     "批量校验完成: 总计 {Total}, 通过 {Passed}, 失败 {Failed}, 耗时 {ElapsedMs}ms, 平均 {AvgMs:F2}ms/构件",
        //     totalCount,
        //     passedCount,
        //     failedCount,
        //     stopwatch.ElapsedMilliseconds,
        //     (double)stopwatch.ElapsedMilliseconds / totalCount);

        return sortedResults;
    }
        
    public ValidationResult Validate(BimComponent component)
    {
        try
        {
            var validationError = ValidateParameters(component);
            if (validationError != null)
            {
                return validationError;
            }

            var calculationResult = CalculationDeflection(component);
            return CreateValidationResult(component, calculationResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }
    
    private ValidationResult? ValidateParameters(BimComponent component)
    {
        var missingParams = new List<string>();

        if (component.Length <= 0)
            missingParams.Add("长度 L");
        if (component.MomentOfInertia <= 0)
            missingParams.Add("惯性矩 I");
        if (component.ElasticModulus <= 0)
            missingParams.Add("弹性模量 E");
        if (component.DeflectionLimitRatio <= 0)
            missingParams.Add("挠度限值");

        if (missingParams.Any())
        {
            return ValidationResult.CreateInvalidParameters(
                component.Guid, 
                component.Name,
                string.Join(", ", missingParams));
        }
        
        return null;
    }

    private DeflectionCalculationResult CalculationDeflection(BimComponent component)
    {
        double L = component.Length;
        double E = component.ElasticModulus;
        double I = component.MomentOfInertia;
        double Q = component.DesignLoad;

        double deflection;
        string formula;
        
        // 核心逻辑：根据物理姿态（SupportCondition）切换计算公式
        if (component.Support == SupportCondition.Cantilever)
        {
            // 悬臂梁挠度公式: v = (q * L⁴) / (8 * E * I)
            deflection = (Q * Math.Pow(L, 4)) / (8 * E * I);
            formula = "v = (q * L⁴) / (8 * E * I)";
        }
        else
        {
            // 简支梁挠度公式: v = (5 * q * L⁴) / (384 * E * I)
            deflection = (5.0 * Q * Math.Pow(L, 4)) / (384 * E * I);
            formula = "v = (5 * q * L⁴) / (384 * E * I)";
        }
        
        double allowedDeflection = component.GetAllowedDeflection();
        
        return new DeflectionCalculationResult
        {
            ActualDeflection = deflection,
            AllowedDeflection = allowedDeflection,
            UtilizationRatio = allowedDeflection > 0 ? deflection / allowedDeflection : 0,  
            IsPassed = deflection <= allowedDeflection,
            Formula = formula,
            Parameters = new DeflectionCalculationParameters
            {
                Length = L,
                ElasticModulus = E,
                MomentOfInertia = I,
                DesignLoad = Q,
                Formula = formula // 记录实际使用的公式，方便审计追溯
            }
        };
    }

    private ValidationResult CreateValidationResult(BimComponent component,
        DeflectionCalculationResult calculationResult)
    {
        if (calculationResult.IsPassed)
        {
            return ValidationResult.CreateSuccess(
                component.Guid, 
                component.Name,
                calculationResult.ActualDeflection,
                calculationResult.AllowedDeflection,
                calculationResult.Parameters);
        }
        else
        {
            // 修复点：将第二个参数由 ActualDeflection 改为 AllowedDeflection
            // 确保审计报告中能清晰显示“实际值”与“限值”的对比
            return ValidationResult.CreateFailure(
                component.Guid,
                component.Name,
                calculationResult.ActualDeflection,
                calculationResult.AllowedDeflection, 
                calculationResult.Parameters,
                "构件挠度计算值超过规范限值");
        }
    }
}

internal class DeflectionCalculationResult
{
    public double ActualDeflection {get; set; }	
    public double AllowedDeflection {get; set; }		
    public double UtilizationRatio {get; set; }	
    public bool IsPassed {get; set; }	
    public string Formula {get; set; }	
    public DeflectionCalculationParameters Parameters {get; set; }	
}



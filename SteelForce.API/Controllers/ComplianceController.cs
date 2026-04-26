using Microsoft.AspNetCore.Mvc;
using SteelForce.API.DTOs;
using SteelForce.API.Mappings;
using SteelForce.Core.Interfaces;
using SteelForce.Core.Models;

namespace SteelForce.API.Controllers;

/// <summary>
/// 挠度合规校验控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceEngine _complianceEngine;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(IComplianceEngine complianceEngine, ILogger<ComplianceController> logger)
    {
        _complianceEngine = complianceEngine;
        _logger = logger;
    }

    /// <summary>
    /// 校验单个构件的挠度合规性
    /// </summary>
    /// <param name="componentDto">BIM构件数据</param>
    /// <returns>校验结果</returns>
    [HttpPost("validate")]
    public ActionResult<ApiResponse<ValidationResultDto>> Validate([FromBody] BimComponentDto componentDto)
    {
        if (componentDto == null)
        {
            return BadRequest(ApiResponse<ValidationResultDto>.Error("请提供有效的构件数据"));
        }

        try
        {
            _logger.LogInformation("开始校验构件: {ComponentName}", componentDto.Name);
            var component = componentDto.ToModel();
            var result = _complianceEngine.Validate(component);
            _logger.LogInformation("构件校验完成: {ComponentName}, 结果: {IsPassed}", componentDto.Name, result.IsPassed);

            return Ok(ApiResponse<ValidationResultDto>.Ok(result.ToDto()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "校验构件时发生错误");
            return StatusCode(500, ApiResponse<ValidationResultDto>.Error($"校验失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 批量校验构件的挠度合规性
    /// </summary>
    /// <param name="request">批量校验请求，包含构件列表和并行度配置</param>
    /// <returns>校验结果列表</returns>
    [HttpPost("validate-batch")]
    public async Task<ActionResult<ApiResponse<List<ValidationResultDto>>>> ValidateBatch([FromBody] BatchValidationRequest request)
    {
        if (request == null || request.Components == null || request.Components.Count == 0)
        {
            return BadRequest(ApiResponse<List<ValidationResultDto>>.Error("请提供有效的构件列表"));
        }

        try
        {
            _logger.LogInformation("开始批量校验 {ComponentCount} 个构件", request.Components.Count);
            
            var components = request.Components.Select(c => c.ToModel()).ToList();
            
            var progress = new Progress<ValidationProgress>();
            var results = await _complianceEngine.ValidateBatchAsync(
                components,
                request.MaxDegreeOfParallelism,
                progress);
            
            var resultDtos = results.Select(r => r.ToDto()).ToList();
            
            var passedCount = resultDtos.Count(r => r.IsPassed);
            var failedCount = resultDtos.Count - passedCount;
            
            _logger.LogInformation(
                "批量校验完成: 总计 {Total}, 通过 {Passed}, 失败 {Failed}", 
                resultDtos.Count, passedCount, failedCount);

            return Ok(ApiResponse<List<ValidationResultDto>>.Ok(
                resultDtos,
                $"批量校验完成: 总计 {resultDtos.Count}, 通过 {passedCount}, 失败 {failedCount}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量校验时发生错误");
            return StatusCode(500, ApiResponse<List<ValidationResultDto>>.Error($"批量校验失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// 一键解析IFC文件并校验所有构件的挠度合规性
    /// </summary>
    /// <param name="file">IFC文件</param>
    /// <param name="maxDegreeOfParallelism">最大并行度（可选）</param>
    /// <returns>解析得到的构件列表和对应的校验结果</returns>
    [HttpPost("parse-and-validate")]
    public async Task<ActionResult<ApiResponse<ParseAndValidateResponseDto>>> ParseAndValidate(IFormFile file, [FromQuery] int? maxDegreeOfParallelism = null)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<ParseAndValidateResponseDto>.Error("请上传有效的IFC文件"));
        }

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ifc");
        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("开始解析并校验IFC文件: {FileName}", file.FileName);
            
            // 1. 调用解析器
            var ifcParser = HttpContext.RequestServices.GetRequiredService<IIfcParser>();
            var components = ifcParser.Parse(tempFilePath).ToList();
            
            _logger.LogInformation("IFC文件解析完成，找到 {ComponentCount} 个构件，开始校验", components.Count);
            
            // 2. 调用合规引擎进行批量计算
            var results = await _complianceEngine.ValidateBatchAsync(
                components,
                maxDegreeOfParallelism);
            
            // 3. 统计审计关键指标 (基于最新内核能力)
            var cantileverCount = components.Count(c => c.Support == SupportCondition.Cantilever);
            var libCount = components.Count(c => c.InertiaFromStandardLibrary || c.ElasticModulusFromMaterialLibrary);
            
            var componentDtos = components.Select(c => c.ToDto()).ToList();
            var resultDtos = results.Select(r => r.ToDto()).ToList();
            
            var passedCount = resultDtos.Count(r => r.IsPassed);
            var failedCount = resultDtos.Count - passedCount;
            
            var auditSummary = $"工业审计完成。自动识别出 {cantileverCount} 根悬臂构件并切换计算公式；" +
                              $"通过标准库自动修复了 {libCount} 根构件的数据缺失问题。";

            _logger.LogInformation(
                "解析并校验完成: 总计 {Total}, 通过 {Passed}, 悬臂 {Cantilever}, 自动修复 {Lib}", 
                resultDtos.Count, passedCount, cantileverCount, libCount);

            var response = new ParseAndValidateResponseDto
            {
                Components = componentDtos,
                ValidationResults = resultDtos,
                TotalCount = resultDtos.Count,
                PassedCount = passedCount,
                FailedCount = failedCount,
                CantileverCount = cantileverCount,
                LibrarySupplementCount = libCount,
                AuditSummary = auditSummary
            };

            return Ok(ApiResponse<ParseAndValidateResponseDto>.Ok(
                response,
                auditSummary));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "IFC文件未找到");
            return NotFound(ApiResponse<ParseAndValidateResponseDto>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析并校验IFC文件时发生错误");
            return StatusCode(500, ApiResponse<ParseAndValidateResponseDto>.Error($"操作失败: {ex.Message}"));
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
    }
}

/// <summary>
/// 解析并校验响应 DTO
/// </summary>
public class ParseAndValidateResponseDto
{
    public List<BimComponentDto> Components { get; set; } = new();
    public List<ValidationResultDto> ValidationResults { get; set; } = new();
    public int TotalCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>
    /// 自动识别出的悬臂梁数量
    /// </summary>
    public int CantileverCount { get; set; }

    /// <summary>
    /// 通过标准库补充参数的构件数量
    /// </summary>
    public int LibrarySupplementCount { get; set; }

    /// <summary>
    /// 本次解析的审计简报
    /// </summary>
    public string AuditSummary { get; set; } = string.Empty;
}
using Microsoft.AspNetCore.Mvc;
using SteelForce.API.DTOs;
using SteelForce.API.Mappings;
using SteelForce.Core.Interfaces;

namespace SteelForce.API.Controllers;

/// <summary>
/// IFC文件解析控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IfcParserController : ControllerBase
{
    private readonly IIfcParser _ifcParser;
    private readonly ILogger<IfcParserController> _logger;

    public IfcParserController(IIfcParser ifcParser, ILogger<IfcParserController> logger)
    {
        _ifcParser = ifcParser;
        _logger = logger;
    }

    /// <summary>
    /// 上传并解析IFC文件
    /// </summary>
    /// <param name="file">IFC文件</param>
    /// <returns>解析得到的BIM构件列表</returns>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<List<BimComponentDto>>>> UploadAndParse(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<List<BimComponentDto>>.Error("请上传有效的IFC文件"));
        }

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ifc");
        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("开始解析IFC文件: {FileName}", file.FileName);
            var components = _ifcParser.Parse(tempFilePath).ToList();
            _logger.LogInformation("IFC文件解析完成，找到 {ComponentCount} 个构件", components.Count);

            var componentDtos = components.Select(c => c.ToDto()).ToList();

            return Ok(ApiResponse<List<BimComponentDto>>.Ok(
                componentDtos,
                $"成功解析 {components.Count} 个构件"));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "IFC文件未找到");
            return NotFound(ApiResponse<List<BimComponentDto>>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析IFC文件时发生错误");
            return StatusCode(500, ApiResponse<List<BimComponentDto>>.Error($"解析失败: {ex.Message}"));
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
    }

    /// <summary>
    /// 从文件路径解析IFC文件
    /// </summary>
    /// <param name="filePath">IFC文件的完整路径</param>
    /// <returns>解析得到的BIM构件列表</returns>
    [HttpPost("parse")]
    public ActionResult<ApiResponse<List<BimComponentDto>>> ParseFromPath([FromBody] string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return BadRequest(ApiResponse<List<BimComponentDto>>.Error("请提供有效的文件路径"));
        }

        try
        {
            _logger.LogInformation("开始解析IFC文件: {FilePath}", filePath);
            var components = _ifcParser.Parse(filePath).ToList();
            _logger.LogInformation("IFC文件解析完成，找到 {ComponentCount} 个构件", components.Count);

            var componentDtos = components.Select(c => c.ToDto()).ToList();

            return Ok(ApiResponse<List<BimComponentDto>>.Ok(
                componentDtos,
                $"成功解析 {components.Count} 个构件"));
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "IFC文件未找到");
            return NotFound(ApiResponse<List<BimComponentDto>>.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析IFC文件时发生错误");
            return StatusCode(500, ApiResponse<List<BimComponentDto>>.Error($"解析失败: {ex.Message}"));
        }
    }
}

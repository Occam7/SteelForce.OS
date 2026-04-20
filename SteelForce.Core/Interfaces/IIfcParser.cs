namespace SteelForce.Core.Interfaces;

using SteelForce.Core.Models;

public interface IIfcParser
{
    /// <summary>
    /// 这是一个契约：
    /// 只要是解析器，就必须能接受一个路径，并返回一组构件。
    /// 至于怎么解析，接口不管。
    /// </summary>
    IEnumerable<BimComponent> Parse(string filePath);
}
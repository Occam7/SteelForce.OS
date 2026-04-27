# SteelForce.OS

基于 **.NET 8** 与 **Xbim** 库开发的 IFC 钢结构合规审计引擎。

## 核心功能

* **姿态识别 (Pose Detection)**：通过 `IfcRelConnectsStructuralMember` 拓扑关系自动判定**简支**或**悬臂**状态，动态切换挠度计算公式（系数 $5/384$ 或 $1/8$）。
* **长度提取 (Triple-Jump)**：按 **Qto (基准工程量) -> Pset (属性集) -> Geometry (几何反算)** 优先级提取构件真实长度，解决模型参数缺失问题。
* **数据驱动**：通过 `SteelLibrary.json` 维护 200+ 标准截面参数，支持在加载阶段自动完成单位归一化（$cm^4 \to mm^4$）。
* **异常隔离 (Fault Tolerance)**：解析异常时自动捕获并记录构件 `GlobalId`，确保局部数据缺陷不中断整体审计流程。

## 项目结构

* **SteelForce.API**: RESTful 接口服务，支持模型上传与批量审计。
* **SteelForce.Services**: 核心逻辑层，包含 IFC 解析与合规性校验引擎。
* **SteelForce.Infrastructure**: 外部截面库与材质库的加载与配置。
* **SteelForce.Core**: 领域模型（BimComponent）与接口定义。

## 运行说明

```bash
# 启动 API 服务
dotnet run --project SteelForce.API

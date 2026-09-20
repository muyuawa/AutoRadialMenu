# Changelog

本项目遵循语义化版本思路。

## 0.1.0 - 2026-09-20

首个 GitHub 发布版本。

### 功能

- 生成真正的 VRChat Radial Puppet 大轮盘。
- 支持自定义菜单名称、Float 参数名和图标。
- 对象模式支持批量拖入多个 GameObject。
- 对象模式支持可选“全关”档位。
- 材质模式支持批量拖入多个对象。
- 自动查找对象及子级中的 SkinnedMeshRenderer。
- 自动识别每个 SkinnedMeshRenderer 的全部材质。
- 每个材质拥有独立替换材质列表。
- 同一个轮盘同步驱动多个对象、多个 Renderer、多个材质。
- 空替换列表保持原材质。
- 替换列表长度不同时，较短列表保持最后一个已配置材质。
- Inspector 使用中文卡片式界面。
- 支持对象卡片和材质项排序。
- 支持多材质批量拖入。
- 支持手动 Modular Avatar Menu Installer 安装目标。
- 保留早期组件数据迁移逻辑。

### 验证

- Unity 2022.3 编译通过。
- VRChat SDK Avatars 3.10.4。
- Modular Avatar 1.18.1。
- NDMF 1.14.4。
- 已验证多个 Renderer、多材质槽生成独立材质动画曲线。

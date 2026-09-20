# Auto Radial Menu / 自动轮盘

一个面向 **VRChat Avatar 3.0** 的 Unity 编辑器插件。

它会在 NDMF 构建阶段自动生成真正的 **VRChat Radial Puppet 大轮盘**，不需要手工制作 FX Animator、Expression Parameters 和菜单控制逻辑。

当前支持两种模式：

- **开关对象**：转动大轮盘，在多个 GameObject 之间切换，可选“全关”档位。
- **替换材质**：一个大轮盘同步控制多个 `SkinnedMeshRenderer`，自动识别每个 Renderer 的全部材质，并让每个材质拥有独立的替换列表。

> 生成内容只存在于上传 / Play 构建克隆中，不会修改当前编辑场景。

## 环境

开发与验证环境：

- Unity **2022.3**
- VRChat SDK - Avatars **3.10.4**
- Modular Avatar **1.18.1**
- NDMF **1.14.4**

建议先通过 VCC 安装 VRChat SDK、Modular Avatar 和 NDMF，再安装本插件。

## 安装

安装前请先通过 VCC 确认项目已有 VRChat SDK、Modular Avatar 和 NDMF。

### 推荐：GitHub Release 的 unitypackage

打开 GitHub 的 **Releases** 页面，下载最新的：

```
AutoRadialMenu-vX.Y.Z.unitypackage
```

双击文件，或在 Unity 中使用：

`Assets > Import Package > Custom Package...`

导入全部内容即可。

### Unity Package Manager：Git URL

也可以在 Unity 打开：

`Window > Package Manager > + > Add package from git URL...`

输入：

```
https://github.com/muyuawa/AutoRadialMenu.git
```

### 手动安装

也可以下载仓库后，将仓库放到项目的 `Packages` 目录，例如：

```
Packages/com.muyu.auto-radial-menu
```

## 基本使用

1. 在 Avatar 下创建或选择一个 GameObject。
2. 添加组件：**自动轮盘 / 自动轮盘**。
3. 设置：
   - **菜单名称**
   - **参数名**（可以留空，留空时自动生成）
   - **图标**（可选）
   - **功能**
4. 根据需要选择 **开关对象** 或 **替换材质**。
5. 上传 Avatar 或通过 NDMF Play 流程测试。

如果同一 GameObject 上手动添加了 **Modular Avatar Menu Installer**，插件会使用它指定的目标菜单；没有时默认安装到 Avatar 根菜单。

---

## 开关对象

适合衣服、配件、尾巴、特效等互斥对象切换。

### 配置

- 可以一次拖入多个 GameObject。
- 开启 **包含全关区间** 后，轮盘最前面增加一个“全部关闭”的状态。
- 其余档位按照列表顺序，一次只开启对应对象。

例如：

```
全关 → 衣服 A → 衣服 B → 衣服 C
```

如果关闭“包含全关区间”：

```
衣服 A → 衣服 B → 衣服 C
```

---

## 替换材质

材质模式支持 **多个对象、多个 SkinnedMeshRenderer、每个 Renderer 多个材质**。

### 批量添加对象

可以在 Hierarchy 中一次选中多个对象，然后一起拖入 **批量添加** 区域。

插件会：

1. 找到拖入对象自身和子级中的 `SkinnedMeshRenderer`。
2. 自动去重。
3. 为每个 Renderer 建立一个材质对象卡片。
4. 自动识别该 Renderer 的全部 `sharedMaterials`。

例如一个 Renderer 有三个材质：

```
Tail
├─ 材质 1 · tail_base
├─ 材质 2 · tail_crystal
└─ 材质 3 · tail_emission
```

不需要手工处理 Unity 的“槽 0 / 槽 1 / 槽 2”。

### 每个材质独立设置替换列表

每个识别到的材质都有自己的替换列表。

例如：

```
Tail
├─ 材质 1 · tail_base
│  ├─ 档位 1：tail_red
│  └─ 档位 2：tail_blue
├─ 材质 2 · tail_crystal
│  ├─ 档位 1：crystal_red
│  └─ 档位 2：crystal_blue
└─ 材质 3 · tail_emission
   └─ 不填写
```

轮盘效果：

```
0 档：全部保持原材质
1 档：tail_red + crystal_red + emission 原材质
2 档：tail_blue + crystal_blue + emission 原材质
```

### 不填写替换材质

某个材质的替换列表为空时，它会 **始终保持原材质**，不会参与替换。

如果不同材质的替换数量不同，较短的列表在后续档位会保持它最后一个已配置材质。

例如：

```
材质 A：A1、A2
材质 B：B1
```

则：

```
0 档：A原 + B原
1 档：A1 + B1
2 档：A2 + B1
```

---

## 工作原理

插件使用一个 `0 ~ 1` 的 Float 参数作为 Radial Puppet 参数。

构建时通过 NDMF 自动生成：

- VRChat Radial Puppet 菜单项
- Expression Float Parameter
- FX Animator Controller
- 基于参数时间驱动的 AnimationClip

对象模式通过 `GameObject.m_IsActive` 曲线切换对象。

材质模式通过：

```
SkinnedMeshRenderer.m_Materials.Array.data[index]
```

的 Object Reference Animation Curve 同步切换多个材质。



## License

MIT License。详见 [LICENSE](LICENSE)。

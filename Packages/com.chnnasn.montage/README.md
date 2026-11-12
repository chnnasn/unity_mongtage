# Unity Montage

一个基于 Unity Playables 的轻量动画 Montage 插件，提供类似 UE Animation Montage 的 Slot、Section、Notify、打断和混合能力，同时保留现有 Animator Controller 作为基础 Locomotion。

> 当前发布版本为 `0.1.0`。工作区包含尚未发布的 UE Montage 核心工作流增强。

## 功能

- 每个角色一张长期存在的 `PlayableGraph`
- 保留 Animator Controller 作为 Base Pose
- FullBody、UpperBody、Additive 等自定义 Slot
- Slot 级 AvatarMask
- Montage 淡入、淡出、优先级和打断
- AnimationClip 拖放、移动、重排、复制、删除和边缘裁剪
- Source Clip In/Out、片段倒放、循环次数与播放速率
- Section 分段、拖动边界、连接编辑、循环、跳转与动态 Next Section
- Slot Group 互斥、跨组并行与组级停止
- 暂停、继续、定位、起播位置与完整播放生命周期回调
- 自动提前淡出、停留末帧与自定义混合曲线
- Montage 命名浮点曲线和运行时查询
- 多轨道命名 Notify、Notify State 与精确时间点分支
- Notify 回调中的跳段、停止和重入处理
- 可选 Root Motion Driver
- Montage 资源校验与可编辑时间轴
- 可拖动的预览/属性/时间轴分隔条、布局保存和属性面板隐藏
- 独立 Preview Scene 中的实时角色动画预览
- 带世界网格、右键环绕视角的 Preview 视图
- 可拖放编辑的 Audio 与 VFX 时间轴轨道
- 内置运行时 Audio/VFX Notify 资源
- Humanoid IK Retargeter、语义骨骼链映射与 Retarget Pose
- 双角色重定向预览、手脚 Two-Bone IK 与目标骨架 AnimationClip 烘焙
- 无第三方运行时依赖

## 安装

在 Unity Package Manager 中选择 **Add package from git URL**，然后填写：

```text
https://github.com/chnnasn/unity_mongtage.git?path=/Packages/com.chnnasn.montage
```

开发期间也可以选择 **Add package from disk**，指向仓库根目录的 `package.json`。

最低 Unity 版本为 2022.3。

## 快速开始

1. 给角色添加 `Animator` 和 `MontagePlayer`。
2. 通过 **Assets > Create > Montage > Montage Asset** 创建资源。
3. 添加一个 Slot Track，并设置 Slot 名称、AvatarMask 和 Animation Segment。
4. 从游戏代码播放：

```csharp
using ChnnAsn.Montage;
using UnityEngine;

public sealed class CharacterAttack : MonoBehaviour
{
    [SerializeField] private MontagePlayer montagePlayer;
    [SerializeField] private MontageAsset attackMontage;

    public void Attack()
    {
        var handle = montagePlayer.Play(
            attackMontage,
            MontagePlayOptions.Default
                .WithSection("Attack01")
                .WithPriority(100));

        handle.SetNextSection("Attack01", "Attack02");
    }
}
```

## 编辑器工具

- **Window > Animation > Unity Montage**：选择角色 Prefab，实时播放和查看 Montage。
- **Window > Animation > Unity Montage > IK Retargeter**：双角色预览、配置骨骼链并烘焙目标动画。
- **Tools > Unity Montage > Validate Project**：检查项目内所有 MontageAsset。

预览窗口支持播放、暂停、停止、循环、0.25x–2x 倍速、可配置 FPS 逐帧、时间拖动和 Section 范围播放。角色视图带世界网格，使用右键旋转、中键平移、滚轮缩放和 `F` 重新聚焦。AudioClip 与特效 Prefab 可以从 Project 直接拖入 Audio/VFX 轨道，事件块支持拖动改时间和右键删除；预览使用独立 Preview Scene，不会向当前场景添加对象，也不会触发 Montage Notify。

选中动画片段、Section、Notify 或曲线后，在右侧 Details 中修改参数。拖动预览区下方的横线调整高度，拖动预览与 Details 中间的竖线调整宽度。工具栏的 Details 可以收起属性面板，Reset Layout 恢复布局。Audio/VFX 的资源参数通过 Edit Notify Payload 打开。

IK Retargeter 使用 Unity Humanoid Avatar 完成基础动作重定向，使用插件内置的 Retarget Pose 与 Two-Bone IK 修正体型差异、手脚接触点和武器握持。烘焙后的 `.anim` 绑定目标骨架，可直接作为 Montage Segment 使用；详细步骤见 [IK Retargeter](Documentation~/ik-retargeter.md)。

## Slot 约定

同名 Slot 在同一个 `MontagePlayer` 上共享一套 AvatarMask 和 Additive 设置，第一次播放时创建。不同 Montage 对同名 Slot 应使用相同配置，否则运行时会警告并采用第一次创建的配置。

同一 Montage 中每个 Slot 只能有一个 Track。共享 Slot 或相同非空 Slot Group 的 Montage 相互竞争，较高优先级会拒绝较低优先级请求。不同 Group 且无共享 Slot 的 Montage 可以并行。旧资源的空 Group 延续按 Slot 仲裁的行为。

## Root Motion

默认不添加 `MontageRootMotionDriver`，因此插件不会接管项目已有的 Root Motion 流程。需要插件处理时再添加该组件：

- `ApplyToTransform`：直接应用到角色 Transform。
- `SendToReceiver`：发送给实现 `IMontageRootMotionReceiver` 的角色移动组件。

## 已知边界

- Montage 时间轴正向推进，单个 Segment 支持负 Play Rate 倒放。
- Section 一帧最多跳转 32 次，用于阻止错误配置造成无限循环。
- Slot 的创建顺序决定 Layer Mixer 的叠加顺序。
- UE 的 Child Montage、Sync Group、Notify 相对/比例时间绑定、网络 Root Motion 同步、Motion Warping 和网络预测仍待实现。
- 浮点曲线通过 API 提供数值，材质和 Morph Target 的绑定由调用方处理。
- 编辑器预览采样动画和 Section 连接，Audio/VFX 和游戏 Notify 在运行时执行。

更多说明见 [Documentation~/index.md](Documentation~/index.md)。仓库本身同时包含一个 Unity 2022.3 开发工程；通过 Git URL 安装时必须保留上面的 `?path=/Packages/com.chnnasn.montage` 参数。

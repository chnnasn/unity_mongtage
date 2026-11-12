# Unity Montage

一个基于 Unity Playables 的轻量动画 Montage 插件，提供类似 UE Animation Montage 的 Slot、Section、Notify、打断和混合能力，同时保留现有 Animator Controller 作为基础 Locomotion。

`package.json` 中的包版本为 `0.1.0`，最低 Unity 版本为 `2022.3`，当前验证版本为 `2022.3.48f1`。本文对应仓库当前源码，包含时间轴编辑、播放控制与 IK 重定向功能。

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

在 Unity Package Manager 中选择 **Add package from git URL**，输入以下地址。

```text
https://github.com/chnnasn/unity_mongtage.git?path=/Packages/com.chnnasn.montage
```

克隆本仓库后，也可以选择 **Add package from disk**，打开 `Packages/com.chnnasn.montage/package.json`。直接打开仓库根目录时，Unity 会将该目录下的插件作为 Embedded Package 加载。

开发工程中的 UnitySkills 用于编辑器工具接入，Montage 运行时使用 Unity 内置 API。

## 快速开始

1. 给角色添加 `Animator` 和 `MontagePlayer`。
2. 通过 **Assets > Create > Montage > Montage Asset** 创建资源并双击打开。也可以选中非 Legacy 的 AnimationClip，通过 **Assets > Create > Montage > From Selected Animation Clip** 创建带动画的资源。
3. 在编辑器中通过 **+ Slot** 添加轨道并拖入 AnimationClip。选中轨道可设置 Slot 名称、AvatarMask 和 Additive，选中片段可修改源动画范围和播放参数。
4. 给 Preview Character 赋值以查看角色预览，在游戏脚本中保存 MontagePlayer 与 MontageAsset 引用并播放。

```csharp
using ChnnAsn.Montage;
using UnityEngine;

public sealed class CharacterAttack : MonoBehaviour
{
    [SerializeField] private MontagePlayer montagePlayer;
    [SerializeField] private MontageAsset attackMontage;

    public void Attack()
    {
        montagePlayer.Play(attackMontage);
    }
}
```

`Play(montage)` 使用资源的 Default Play Rate。带参数的重载可以指定起播段、位置、速率、优先级和淡入时间。

## Section 与播放控制

通过 **+ Section** 在播放头位置分段，在 **Section Links** 中编辑后续段。选择当前段自身形成循环，选择 End 结束。以下代码假设资源中已创建 `Attack01` 和 `Attack02`。

```csharp
var handle = montagePlayer.Play(
    attackMontage,
    MontagePlayOptions.Default
        .WithSection("Attack01")
        .WithPlayRate(1.1f)
        .WithPriority(100)
        .WithBlendIn(0.1f));

handle.SetNextSection("Attack01", "Attack02");
```

| API | 用途 |
| --- | --- |
| `Pause()` / `Resume()` | 冻结和恢复时间、混合进度及状态事件 Tick |
| `SetPosition(seconds)` | 定位到指定时间，更新 Section 和状态事件 |
| `JumpToSection(name)` | 立即跳到指定 Section 的起点 |
| `SetNextSection(from, to)` | 为本次播放设置下一段，空目标结束该段 |
| `ClearNextSection(from)` | 恢复资源中配置的段连接 |
| `SetPlayRate(rate)` | 设置正向的 Montage 播放速率 |
| `Stop(seconds)` | 以指定时间淡出停止 |
| `GetCurveValue(name)` | 读取当前时间的命名浮点曲线 |
| `montagePlayer.StopGroup(group, seconds)` | 停止指定 Slot Group 的实例 |

使用 `MontagePlayOptions.Default.WithStartTime(seconds)` 可以从指定位置开始。`WithStartTime` 会清空 StartSection，随后调用 `WithSection` 时以 Section 起点为准。`MontagePlayOptions.Default` 的 PlayRate 为 `1`，非正的选项值会回退到资源速率。

定位会结束旧的状态事件并进入目标位置覆盖的状态事件。目标时间上的 Point Notify 会触发一次，跳过区间内的 Point Notify 会略过。定位与跳段适用于淡出开始前的实例。

## Notify 与生命周期

**+ Notify** 创建命名点事件，**+ State** 创建带持续时间的状态事件。Details 中的 Track 可将事件放到其他 Notify 轨道，已有 MontageNotifyAsset 可以拖入轨道作为自定义行为。

命名事件的 Asset 可以为空。显示或判断事件名称时使用 `context.Notify.Name`，该属性按顺序取事件名称、资源名称或默认的 `Notify`。

```csharp
private void OnEnable()
{
    montagePlayer.NotifyTriggered += OnNotifyTriggered;
}

private void OnDisable()
{
    if (montagePlayer != null)
        montagePlayer.NotifyTriggered -= OnNotifyTriggered;
}

private void OnNotifyTriggered(MontageNotifyContext context)
{
    Debug.Log($"Notify {context.Notify.Name} at {context.Time:0.00}s");
}
```

将以上监听方法放在持有 MontagePlayer 引用的角色脚本中。事件按时间边界同步派发，回调可以跳段、定位、停止播放或修改 Next Section。

| 事件 | 触发时机 |
| --- | --- |
| `Started` | 播放开始，早于起播位置的 Notify |
| `SectionChanged` | 当前 Section 变化，目标区域可对应空名称 |
| `NotifyTriggered` | 点事件触发 |
| `NotifyStateBegin` / `NotifyStateTick` / `NotifyStateEnd` | 状态窗口进入、持续和退出 |
| `BlendingOut` | 开始淡出，附带结束原因 |
| `Interrupted` | 被其他 Montage 打断 |
| `Ended` | 实例移除，附带 Completed、Interrupted、Stopped 或 PlayerDisabled 原因 |
| `Completed` | 自然结束，在 Ended 之后触发 |

Handle 在淡出期间保持有效，在 Ended 触发前失效。调用方可以通过 `IsValid`、`IsPaused`、`IsBlendingOut`、`Time` 和 `CurrentSection` 查询状态。完整语义见运行时文档。[Runtime API](Documentation~/runtime-api.md)

## 编辑器工具

| 入口 | 用途 |
| --- | --- |
| **Window > Animation > Unity Montage** | 编辑 Montage 并预览角色动画 |
| **Window > Animation > Unity Montage > IK Retargeter** | 双角色预览、骨骼链配置和动画烘焙 |
| **Tools > Unity Montage > Validate Project** | 检查项目内所有 MontageAsset |

预览窗口支持播放、暂停、停止、循环、0.25x–2x 倍速、可配置 FPS 逐帧、时间拖动和 Section 范围播放。角色视图带世界网格，使用右键旋转、中键平移、滚轮缩放和 `F` 重新聚焦。AudioClip 与特效 Prefab 可以从 Project 直接拖入 Audio/VFX 轨道，事件块支持拖动改时间和右键删除。预览使用独立 Preview Scene，游戏 Notify 和 Audio/VFX 在运行时执行。

选中动画片段、Section、Notify 或曲线后，在右侧 Details 中修改参数。拖动预览区下方的横线调整高度，拖动预览与 Details 中间的竖线调整宽度。工具栏的 Details 可以收起属性面板，Reset Layout 恢复布局。Audio/VFX 的资源参数通过 Edit Notify Payload 打开。

拖动动画片段主体可以移动片段，越过相邻片段中点后松开可重排。片段边缘用于裁剪，Snap 与 FPS 用于帧吸附，Zoom 与横向滚动条用于浏览长时间轴。启用 **Follow links during entire-montage preview** 可按 Section 连接顺序预览。详细操作见编辑器文档。[Montage Preview](Documentation~/montage-preview.md)

IK Retargeter 使用 Unity Humanoid Avatar 完成基础动作重定向，并通过 Retarget Pose 与 Two-Bone IK 修正体型差异、手脚接触点和武器握持。烘焙后的 `.anim` 绑定目标骨架，可直接作为 Montage Segment 使用。详细步骤见重定向文档。[IK Retargeter](Documentation~/ik-retargeter.md)

## 片段与混合参数

| 参数 | 行为 |
| --- | --- |
| Clip In / Clip Out | 源动画采样范围，Clip Out 为 `0` 时使用源动画结尾 |
| Duration | 为 `0` 时根据源范围、速率和 Loop Count 计算时长，正值指定显式时间轴长度 |
| Segment Play Rate | 源片段速率，负值使该片段倒放 |
| Loop Count | 自动时长计算时的循环次数 |
| Blend In / Blend Out Curve | 将归一化混合进度映射到权重变化 |
| Auto Blend Out | 启用后在最终段自动淡出，关闭后保持末帧直至停止或打断 |
| Blend Out Trigger Time | 负值按淡出时长和播放速率计算提前量，非负值表示距离段末的 Montage 秒数 |

默认 Blend Out Trigger Time 为 `-1`，淡出会在最终段结束前启动。设为 `0` 时，淡出从最终段结束时开始。通过 **+ Curve** 创建的命名浮点曲线使用 Montage 秒数作为时间轴，可由游戏逻辑采样后应用到伤害、IK 或材质等目标。

## Slot 约定

同名 Slot 在同一个 `MontagePlayer` 上共享一套 AvatarMask 和 Additive 设置，第一次播放时创建。不同 Montage 对同名 Slot 应使用相同配置，否则运行时会警告并采用第一次创建的配置。

同一 Montage 中每个 Slot 只能有一个 Track。共享 Slot 或相同非空 Slot Group 的 Montage 相互竞争，较高优先级会拒绝较低优先级请求。不同 Group 且无共享 Slot 的 Montage 可以并行。旧资源的空 Group 延续按 Slot 仲裁的行为。

## Root Motion

需要由插件处理 Root Motion 时，给角色添加 `MontageRootMotionDriver`，并选择应用方式。

- `ApplyToTransform` 直接应用到角色 Transform。
- `SendToReceiver` 发送给实现 `IMontageRootMotionReceiver` 的角色移动组件。

## 已知边界

- Montage 时间轴正向推进，单个 Segment 支持负 Play Rate 倒放。
- 沿 Section 连接每帧最多推进 32 次，用于阻止错误配置造成无限循环。
- Slot 的创建顺序决定 Layer Mixer 的叠加顺序。
- UE 的 Child Montage、Sync Group、Notify 相对/比例时间绑定、网络 Root Motion 同步、Motion Warping 和网络预测仍待实现。
- 浮点曲线通过 API 提供数值，材质和 Morph Target 的绑定由调用方处理。
- 编辑器预览采样动画和 Section 连接，Audio/VFX 和游戏 Notify 在运行时执行。
- 重定向以 Humanoid 为主要流程，Generic 动画烘焙要求兼容的 Transform 路径，任意 Generic 链重采样仍待实现。

## 测试

EditMode 测试程序集为 `ChnnAsn.Montage.Editor.Tests`。开发工程最近一次回归于 2026-11-12 在 Unity `2022.3.48f1` 中完成，48 项测试中 46 项通过、0 失败，2 项图形界面测试因无图形模式跳过。Demo 的命名事件、空资源回退、资源名称回退和空 Section 名称均有回归覆盖。

图形界面测试需要可绘制窗口的 Editor，拖拽测试还需要宿主能够投递鼠标事件。单独安装插件时，缺少开发工程 Demo 脚本会使相应集成测试跳过。

开发工程的运行步骤、测试命令和 Demo 入口见仓库首页。[开发工程 README](../../README.md)

完整文档和版本变更记录如下。[文档索引](Documentation~/index.md) · [Changelog](CHANGELOG.md)

# Unity Montage

一个基于 Unity Playables 的轻量动画 Montage 插件，提供类似 UE Animation Montage 的 Slot、Section、Notify、打断和混合能力，同时保留现有 Animator Controller 作为基础 Locomotion。

> 当前版本：`0.1.0`，属于可运行的 MVP。包名使用正确术语 `com.chnnasn.montage`；仓库名暂时保留原有拼写。

## 功能

- 每个角色一张长期存在的 `PlayableGraph`
- 保留 Animator Controller 作为 Base Pose
- FullBody、UpperBody、Additive 等自定义 Slot
- Slot 级 AvatarMask
- Montage 淡入、淡出、优先级和打断
- 多 AnimationClip Segment
- Section 跳转与动态 Next Section
- Point Notify 与 Notify State
- 可选 Root Motion Driver
- Montage 资源校验与时间轴概览窗口
- 独立 Preview Scene 中的实时角色动画预览
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
- **Tools > Unity Montage > Validate Project**：检查项目内所有 MontageAsset。

预览窗口支持播放、暂停、停止、循环、0.25x–2x 倍速、30 FPS 逐帧、时间拖动和 Section 范围播放。角色视图支持旋转、平移、缩放和重新聚焦；预览使用独立 Preview Scene，不会向当前场景添加对象，也不会触发 Montage Notify。

当前资源编辑仍使用 Inspector；时间轴用于预览和拖动播放头，尚不能直接拖动修改 Segment。

## Slot 约定

同名 Slot 在同一个 `MontagePlayer` 上共享一套 AvatarMask 和 Additive 设置，第一次播放时创建。不同 Montage 对同名 Slot 应使用相同配置，否则运行时会警告并采用第一次创建的配置。

同一 Montage 中每个 Slot 只能有一个 Track。共享 Slot 的新 Montage 会打断较低或相同优先级的旧 Montage；较高优先级 Montage 会拒绝较低优先级播放请求。不同 Slot 可以并行。

## Root Motion

默认不添加 `MontageRootMotionDriver`，因此插件不会接管项目已有的 Root Motion 流程。需要插件处理时再添加该组件：

- `ApplyToTransform`：直接应用到角色 Transform。
- `SendToReceiver`：发送给实现 `IMontageRootMotionReceiver` 的角色移动组件。

## 已知边界

- 当前只支持正向播放。
- Section 一帧最多跳转 32 次，用于阻止错误配置造成无限循环。
- Slot 的创建顺序决定 Layer Mixer 的叠加顺序。
- 尚未提供 Motion Warping、网络预测、Addressables 和 Animation Rigging 集成。

更多说明见 [Documentation~/index.md](Documentation~/index.md)。仓库本身同时包含一个 Unity 2022.3 开发工程；通过 Git URL 安装时必须保留上面的 `?path=/Packages/com.chnnasn.montage` 参数。

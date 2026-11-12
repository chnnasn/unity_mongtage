# Unity Montage Development Project

这是 `com.chnnasn.montage` 的 Unity 2022.3 开发与测试工程。

插件源码位于：

```text
Packages/com.chnnasn.montage
```

## 可运行 Demo

打开 `Assets/MontageDemo/Scenes/MontageDemo.unity` 后进入 Play Mode，即可体验
Section 串联、Notify、混合、跳段、停止和播放速率控制。Demo 也准备了
`DemoCharacter.prefab` 与 `DemoAttackMontage.asset`，可以直接放进
**Window > Animation > Unity Montage** 预览窗口中播放和拖动时间轴。

![Montage Demo](Assets/MontageDemo/Preview.png)

## 开发

使用 Unity `2022.3.48f1` 打开仓库根目录。插件作为 Embedded Package 被项目直接引用，修改包内源码后 Unity 会自动重新编译。

## 测试

在 Unity 中打开 **Window > General > Test Runner**，运行 EditMode 测试；也可以通过命令行执行：

```powershell
& "D:\UnityEngine\Editors\2022.3.48f1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . `
  -runTests -testPlatform EditMode `
  -testResults TestResults\editmode.xml `
  -logFile TestResults\editmode.log
```

包的安装、API 和使用说明见 [插件 README](Packages/com.chnnasn.montage/README.md)。

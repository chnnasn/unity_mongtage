# Unity Montage Development Project

这是 `com.chnnasn.montage` 的 Unity 2022.3 开发与测试工程。

插件源码位于：

```text
Packages/com.chnnasn.montage
```

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

# 批量提取音频 (BatchAudioExtract)

从视频文件**批量提取音频**的 Windows 桌面小工具（C# WinForms）。

## 功能

- 批量添加视频文件 / 整个文件夹 / 拖拽导入
- 输出格式：MP3 / WAV / FLAC / M4A / AAC / OGG
- ⚡ **极速模式**：直接复制音轨（`-c:a copy`），不重新编码、秒出
- 内置 ffmpeg，开箱即用，无需手动配置
- 支持中文/空格路径

## 安装

下载最新 Release 中的 `批量提取音频-Setup-1.1.exe`，双击安装（正式安装包，非便携版）。

## 使用

1. 添加要提取的视频文件或文件夹
2. 选择输出格式（或勾选极速模式）
3. 可选指定输出目录（留空=输出到视频同目录）
4. 点击「开始提取」

## 构建

- 源码：`src/BatchAudioExtract.cs`（.NET Framework WinForms，csc 编译）
- 安装包：`批量提取音频-setup.iss`（Inno Setup 6），产物在 `dist/`

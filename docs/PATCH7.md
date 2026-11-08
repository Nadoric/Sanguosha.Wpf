# Patch7：选将立绘缺失、卡顿及选择响应修复

## 根因

1. `SkinLibrary.Shared` 通过 pack URI 读取 `Assets/Official/sources.json`，但项目只把 `Skins/sources.json` 标记为 WPF Resource，根清单没有嵌入。源目录里有 PNG 不能弥补这个缺失。
2. 根清单的武将标识字段是 `id`，旧读取器却访问不存在的 `file`。即使补上嵌入，初始化仍会失败。
3. 上述异常发生在共享皮肤库初始化处，导致所有立绘绑定失败；选中时原来的全窗口属性重绑会反复访问这个失败的静态入口，放大异常和卡顿。
4. 旧列表为非虚拟化 WrapPanel，所有立绘在 UI 线程同步全尺寸解码，而且每张卡都有阴影。即使没有清单错误，也会增加首屏、筛选和滚动开销。
5. `Run.cmd` 原先无条件优先旧发布目录，源码编译成功也可能启动旧版本。此次截图所述的新编译版本问题并非仅由旧 EXE 造成。

## 修改

- 把两份清单都嵌入 WPF 资源；独立的可跨平台清单解析器读取真实 `id` 字段，保持 90 名武将、270 个外观选项及界将复用基础立绘的映射。
- 清单损坏或缺失时保留全体武将的经典形象映射，不再让静态初始化永久失败；偏好失效回退经典形象，不修改存档和已有皮肤偏好文件。
- 立绘按资源路径共享缓存；最多两个后台解码任务，`DecodePixelWidth=384`，OnLoad 后冻结 BitmapSource；UI 调度器只接收冻结图像、更新并冻结共享 ImageBrush。不再因选将而同步初始化卡牌大图集。后台资源读取使用线程安全的 ResourceManager.GetStream。
- 正常图片失败时尝试本武将经典形象；仍不可用时保留姓名、技能及占位提示，卡片仍能选择，不伪造官网素材。失败记录到 Trace，每个缓存入口只尝试一次，不重复触发绑定异常。
- 固定尺寸的虚拟化回收面板仅生成可见区域及上下各一行缓冲。移除未选中卡片的逐卡阴影，保留选中高亮。
- 筛选一次替换集合，复用武将视图；鼠标选择只通知依赖当前武将的属性，不再全窗口重绑。卡片有不透明底色，图片加载过程中整个卡面也能接收点击。
- 方向键、Home/End、PageUp/PageDown 改变选中并滚动到可见区域；Enter 入局，Tab 可移到其他控件。
- `Run.cmd` 在源码存在时始终构建运行源码；构建失败不会退回旧程序。只有显式 `--published` 或没有源码项目时才使用发布目录。发布脚本报告绝对路径及主程序集 DLL 的 SHA256，失败时明确警告可能残留旧程序。

## 已执行验证

Linux 交叉构建命令：

```sh
dotnet build Sanguosha.slnx -c Release -p:EnableWindowsTargeting=true --maxcpucount:1
dotnet run --project tests/Sanguosha.Verify -c Release
```

- 构建：0 warnings，0 errors。
- Verify：62,815 条断言通过；400 局固定种子全部完成，360 个武将/模式组合，最长 702 个动作。
- 新增检查：90 人全部映射可用，270 个外观对应 195 张有效 PNG；两份清单和全部 PNG 确实在编译产物 `.g.resources` 中，不仅是源目录存在。额外验证双工作线程能读取全部资源，以及同一立绘的并发流读取不会互相影响。
- Windows UI 验证已扩展：异步解码完成后检查冻结和尺寸、虚拟化生成数、键盘首尾导航、滚动至末项及筛选同步。原有皮肤验证和截图等待异步图像完成，避免把未加载状态当作坏图。
- Linux 不能实际启动 WPF；上述 Windows 专属 UI 验证与真人鼠标、键盘及流畅度测试未在本机执行，不冒充已实测。

## Windows 应用方法

1. 关闭所有旧版游戏。将补丁内容按相对路径覆盖到源码根目录（与 `Sanguosha.slnx` 同级）。无文件需要删除。
2. 运行 `Run.cmd`，或执行 `dotnet run --project src\Sanguosha.Desktop -c Release`。源码 Assets 不需要手动复制进 bin，清单和 PNG 都在程序资源里。
3. 如使用发布目录的 EXE，请先重新运行 `Publish.ps1`，然后使用其输出的确切路径；不要继续点击旧快捷方式。无需删除源码 Assets、存档或皮肤设置。可自行移走旧 `artifacts\app` 避免混淆，但不作为修复的必要步骤。
4. 验收：首次出现占位后应自动显示立绘；未加载时也能选择；筛选和滚轮正常，方向键/首尾键选择并滚动，Enter 入局；换皮肤、重新筛选、重新打开选将也应正常。
5. 可选运行 Windows 自动 UI 检查（需要 .NET 10 SDK/Windows Desktop Runtime）：

```bat
dotnet run --project src\Sanguosha.Desktop -c Release -- --verify-ui artifacts\verify-ui-patch7
dotnet run --project src\Sanguosha.Desktop -c Release -- --verify-skins artifacts\verify-skins-patch7
```

成功后输出 result.txt 与原生渲染截图；失败时输出 failure.txt。

## 范围与分发

不修改 Core 规则、AI、武将技能、存档格式或素材本体；新增素材 0 字节。`patch7.zip` 只含与 patch6 基线相比的变更，按原有目录直接覆盖。删除列表：无。

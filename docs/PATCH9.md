# Patch9：选将无法显示全部武将

## 已定位的根因

是先前自定义虚拟化网格的容器回收实现错误，不是缺少武将或模式主动隐藏界将。

WPF 的 `ItemContainerGenerator.GenerateNext(out isNewlyRealized)` 对从回收队列取出的旧容器也返回 `false`。此前实现只在该标志为 `true` 时，把容器加回 `InternalChildren` 并调用 `PrepareItemContainer`；滚动离开首屏、容器被移出视觉树并回收后，下一页拿到的复用容器未重新挂回视觉树、也未准备新内容。因此底层 ItemsSource/生成器可能已有武将，界面却出现空白、缺牌甚至视觉索引失配。

可核对 WPF [ItemContainerGenerator.cs 的 Generator.GenerateNext 实现](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/ItemContainerGenerator.cs)：从 `_recyclableContainers` 出队的分支明确设置 `isNewlyRealized = false`。

旧 UI 验证只检查 `ContainerFromIndex` 非空，这无法证明容器仍在视觉树中，没能覆盖这个回归。

## 修复

- 判断容器是否需要插入的依据改为**实际视觉树成员及位置**，不再把 `isNew` 当作是否属于视觉树的标志。回收容器重新附着后重新准备模板、内容及选中状态，已有且位置正确的容器继续复用。
- 同步处理集合重置/删除/替换/移动后的视觉索引；保持滚动范围、当前偏移和 ScrollViewer 绑定信息一致。
- 统一可移植的网格尺寸计算：完整行数向上取整，最后不足一行也计入 extent，偏移严格限制在合法范围，键盘定位保证目标行进入视口。首次测量之前调用定位也不除零。
- 如果被物理滚动的父控件用无限高度测量，返回**全部内容高度**，不再凭空把它当成三行高，避免父 ScrollViewer 错估范围。
- 选将列表显式零 Padding、像素滚动、常显竖向滚动条，并保留至少一张卡的逻辑高度。武将说明采用稳定的136 DIP高独立滚动区，既避免长说明挤掉卡牌，也避免切换武将时列表视口反复缩小、末行被挤出。
- 选将源直接来自 `Heroes.All` 的阵营筛选。全部90人，魏24、蜀24、吴23、群19；不因对战模式排除标准版/界限突破版。对局内防止同名异版同时参战的规则未改动。
- 保留异步缓存缩略图和虚拟化回收，不恢复 UI 线程全尺寸加载，也正常有限视口只创建可见/缓冲行；无限高度的兼容退路才报告全部内容。保留顶部/底部贴边及既有 Viewbox 缩放。

## 验证

```sh
dotnet build Sanguosha.slnx -c Release -p:EnableWindowsTargeting=true --maxcpucount:1
dotnet run --project tests/Sanguosha.Verify -c Release
```

- Release 交叉构建：0 warnings / 0 errors。
- Verify：70,244 条断言通过；400 局固定种子完成，360 个武将/模式组合，最长682个动作。
- 新增可在 Linux 执行的**同源布局/回收契约回归**：明确复现旧逻辑首屏成功、滚动后回收容器消失，再验证修复后的共享放置决策；模拟1,488次滚轮布局、5,254次回收容器复用，检查全部90/24/24/23/19条目逐一可到达、身份不串位。
- 覆盖分数 DIP 宽度、列数阈值、多种视口高度、底部和中段跳转、反向滚动、所有条目的键盘定位、无限高度父容器及空列表。分数 DIP 用例覆盖高 DPI 下常见尺寸换算边界。
- **Windows 原生 UI 验证已扩展，但未在 Linux 执行**：检查实际视觉树父子关系而不仅是生成器登记；通过 MouseWheel 路由事件、ScrollBar ThumbTrack 事件及键盘导航检查全部阵营、三种可用尺寸/列宽，确认全部条目被显示、末行不裁切、模式不隐藏条目。
- Linux 不能实际运行 WPF，因此不冒充已完成 Windows 真人滚动/拖动、跨屏 DPI 或原生截图验收。

## Windows 使用与验收

1. 关闭旧游戏，按目录覆盖到含 `Sanguosha.slnx` 的源码根目录。无需删除任何文件或素材。
2. 运行 `Run.cmd` 或 `dotnet run --project src\Sanguosha.Desktop -c Release`。使用发布版时先重跑 `Publish.ps1`，不要继续启动旧 EXE。
3. 在“全部”中用滚轮滚到底，拖动右侧滚动条，再按 Home/End、PageUp/PageDown；最后一行应完整可见，点击仍能选中。依次检查魏24、蜀24、吴23、群19，切回全部仍应90人。
4. 试最小窗口、最大化和不同显示缩放；技能较长的武将说明区可以单独滚动，不应挤掉卡牌列表。
5. 可选运行扩展的 Windows UI 验证：

```bat
dotnet run --project src\Sanguosha.Desktop -c Release -- --verify-ui artifacts\verify-ui-patch9
```

成功后输出 result.txt 与原生截图；失败写 failure.txt。原生测试仍需在 Windows 执行确认。

补丁只含相对 patch8 的变更，不改规则、AI、素材和存档。删除列表：无。

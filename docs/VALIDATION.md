# 验证记录

验证日期：2026-11-06。

当前版本：25将、四人/五人/八人身份局与2V2，官方PC风格围桌界面，本地人机对战；武将、卡牌、界面图集、音效与武将语音使用官方公开素材。

## 编译

```powershell
dotnet build Sanguosha.slnx --configuration Release --disable-build-servers --maxcpucount:1
```

Release构建通过：0警告、0错误。

## 规则验证

```powershell
dotnet tests/Sanguosha.Verify/bin/Release/net10.0/Sanguosha.Verify.dll
```

400局固定种子模拟全部结束，覆盖25将、四种模式与四种玩家身份，共53,034次断言，最长475个动作。每步检查108张牌总量，另验证距离、非法出牌、伤害、双闪、救援、胜负、技能交互与存档完整性，并检查非法操作不发出声音事件、转换牌的生效牌型以及加载不重播历史事件。原始输出位于 `artifacts/verification.txt`。

## 官方资源校验

```powershell
./tools/Verify-OfficialAssets.ps1
```

25张独立官方立绘、卡牌图集/坐标、120个MP3与12个界面资源文件，共159个源文件，文件大小与SHA-256全部通过。

## WPF界面

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-ui artifacts/ui
```

使用WPF离屏渲染，已解码25张官方立绘、15张官方牌面、26种红黑点数字形、4种花色。官方PC风格围桌布局在四种模式下检查座位边界和两两重叠；大厅、阵营筛选、选牌、技能转换、目标选择、响应、弃牌、结算、规则页及声音设置均通过；按钮处理逻辑驱动一局至结算，WPF绑定错误为0。

验证截图和结果：`artifacts/ui`。未将离屏验证表述为人工点击实体窗口的测试。

## 语音与播放链路

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-audio artifacts/audio-verification
```

120个MP3均被WPF媒体组件成功打开，包含音轨及正时长。25名武将技能/阵亡映射、队列上限、去重、阵亡优先、静音清理和偏好恢复均通过。另通过实际WPF `MediaPlayer`零输出音量播放两条台词，检查播放结束事件接续下一条，以及播放中立即静音取消。

这是静音解码与播放链路验证，不是人工试听。原始结果和逐文件时长见 `artifacts/audio-verification`。

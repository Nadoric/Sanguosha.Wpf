# 验证记录

> 历史记录。当前patch6核心范围、验证结果与版本边界请看 [EXPANSION_BATCH4.md](EXPANSION_BATCH4.md)。

验证日期：2026-11-07。

当前版本：25将、四人/五人/八人身份局与2V2，官方PC风格围桌界面，本地人机对战；武将、卡牌、界面图集、原有音效与武将语音使用官方公开素材，新增卡牌报音来源单独见 `AUDIO.md`。

## 编译

```powershell
dotnet build Sanguosha.slnx --configuration Release --disable-build-servers --maxcpucount:1
```

Release构建通过：0警告、0错误。

## 规则验证

```powershell
dotnet tests/Sanguosha.Verify/bin/Release/net10.0/Sanguosha.Verify.dll
```

400局固定种子模拟全部结束，覆盖25将、四种模式与四种玩家身份，共57,422次断言，最长1178个动作。每步检查108张牌总量，另验证距离、非法出牌、伤害、双闪、救援、胜负、技能交互与存档完整性，并检查非法操作不发出声音事件、转换牌的生效牌型以及加载不重播历史事件。特效事件还核对使用者/目标座位、实际实体牌、伤害来源及数值。

新增针对顺手牵羊和过河拆桥的六种区域结算，检查隐藏手牌、装备/判定牌的指定、不能重复确认、选牌时暂停推进和禁止存档、实际获得/弃置的实体牌、距离和奇才、陆逊连营及孙尚香枭姬。15种卡牌的效果覆盖杀/闪响应、桃回复、决斗、摸二、群体攻防/回复、装备入槽/替换/距离/次数/八卦判定，以及乐不思蜀红桃通过与其他花色跳过出牌。原始输出位于 `artifacts/verification.txt`。

## 官方资源校验

```powershell
./tools/Verify-OfficialAssets.ps1
```

25张经典立绘、50张额外官方皮肤、卡牌图集/坐标、120个MP3、12个界面资源文件和1个ICO，共210个源文件，文件大小与SHA-256全部通过。每名武将的两张新增皮肤与经典图像及彼此的文件哈希不同。

`tools/Verify-CardVoices.ps1`另核验32个新增卡牌报音源文件/转换产物（28个原始MP3、2个原始OGG、2个转换MP3），不混入官网资源清单。

## WPF界面

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-ui artifacts/ui
```

使用WPF离屏渲染，已解码25张官方立绘、15张官方牌面、26种红黑点数字形、4种花色。官方PC风格围桌布局在四种模式下检查座位边界和两两重叠；大厅、阵营筛选、选牌、技能转换、目标选择、响应、弃牌、结算、规则页及声音设置均通过；按钮处理逻辑驱动一局至结算，WPF绑定错误为0。

新增顺手牵羊的完整按钮流程：出牌、显示手牌/装备/判定三项、点击指定装备、手牌新增实体牌、提示包含牌名、弹窗关闭。截图为 `09-steal-choice.png`、`10-steal-result.png`。

验证截图和结果：`artifacts/ui`。未将离屏验证表述为人工点击实体窗口的测试。

## 语音与播放链路

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-audio artifacts/audio-verification
```

150个MP3均被WPF媒体组件成功打开，包含音轨及正时长。15种牌的男女报音与响应映射、25名武将技能/阵亡映射、独立队列上限、去重、阵亡优先、静音清理和偏好恢复均通过。另通过实际WPF `MediaPlayer`零输出音量播放两条武将台词及杀→闪报音，检查播放结束事件接续下一条，以及播放中立即静音取消。关闭武将语音后卡牌报音仍可用；关闭对局音效会取消卡牌报音。

这是静音解码与播放链路验证，不是人工试听。原始结果和逐文件时长见 `artifacts/audio-verification`。

## 界面细化与对局特效

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-effects artifacts/effects
```

原生WPF时间轴渲染牌面飞行、武将技能横幅、攻击轨迹、伤害飘字、闪的光圈和回复粒子。验证动画结束后元素与时钟移除、32个临时特效上限、关闭开关、暂停/规则页/切换对局时清理、不拦截鼠标。渲染前后存档内容一致，特效不改变规则状态。绑定错误为0。

截图为验证场景的实际WPF渲染，不是人工对局录像：`01-refined-board.png`、`02-skill-and-card.png`、`03-damage-dodge-heal.png`。四种模式和顺手牵羊按钮流程另由 `--verify-ui` 验证；官方图片与卡牌图集仍保持源文件内容。

## 装备栏与皮肤

```powershell
dotnet src/Sanguosha.Desktop/bin/Release/net10.0-windows/山河弈.dll --verify-skins artifacts/skins
```

原生WPF解码75款官方形象，覆盖25将各三款。验证预览不会提前应用、大厅与对局入口、头像刷新、其他角色换肤、恢复经典、选择保存/重新读取、损坏配置回退及错误归属皮肤拒绝；换肤前后游戏存档内容一致，绑定错误为0。

装备栏使用实际已装备的青龙偃月刀、八卦阵及的卢渲染，检查五行图标与文本的几何边界：至少间隔4像素，文字不越出所在行或栏宽。截图包括 `01-equipment-layout.png`、`02-skin-preview.png`、`03-skin-applied.png`、`04-lobby-skin.png`；属于原生渲染与按钮处理流程验证，不是人工点击测试。

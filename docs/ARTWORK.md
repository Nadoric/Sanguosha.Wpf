# 三国杀官方素材来源

当前武将立绘、游戏牌牌面、花色及点数均使用三国杀官网公开资源。素材直接嵌入WPF程序，运行时离线读取。

## 武将立绘

- 图鉴来源：[三国杀·一将成名官方武将图鉴](https://x.sanguosha.com/hero)。
- 对照来源：[三国杀OL官方武将列表](https://www.sanguosha.com/hero)。
- 25名标准武将各自使用独立PNG文件，名称与官方图鉴中的标准形象对应。
- 本机目录：`D:\WorkSpace\Codex\Sanguosha.Wpf\src\Sanguosha.Desktop\Assets\Official\Portraits`。
- 示例：[关羽图鉴](https://x.sanguosha.com/hero/2.html)、[关羽经典形象原图](https://web.sanguosha.com/10/pc/res/assets/runtime/general/big/static/100201.png)。

全部25名武将的官方图鉴地址、图片地址、文件路径、文件字节数和SHA-256记录在 [sources.json](../src/Sanguosha.Desktop/Assets/Official/sources.json)。下载核对日期：2026-11-05。

## 官方卡牌

- 来源：[官网公开客户端入口](https://web.sanguosha.com/10/pc/index.php)及其[资源版本清单](https://web.sanguosha.com/10/pc/version.json)。
- 原始图片：[card.png](https://web.sanguosha.com/10/pc/res/assets/cards/normal/card.png)。
- 配套切片坐标：[card.atlas](https://web.sanguosha.com/10/pc/res/assets/cards/normal/card.atlas)。
- 本机目录：`D:\WorkSpace\Codex\Sanguosha.Wpf\src\Sanguosha.Desktop\Assets\Official\Cards`。

官方图集包含完整牌面，以及独立的红/黑点数字形、四种花色。本版读取15种游戏牌的原始135×190牌面，再使用同一图集的点数与花色对应当前实体卡。WPF `CroppedBitmap`只在显示时按官方坐标读取切片，不修改下载的原始图集。

杀、闪、桃、决斗、无中生有、过河拆桥、顺手牵羊、南蛮入侵、万箭齐发、桃园结义、乐不思蜀、诸葛连弩、青龙偃月刀、八卦阵、的卢均有对应官方牌面。官方牌面上的装备信息沿用原图；游戏具体效果以本项目规则提示为准。

## 官方PC界面与音频

围桌界面采用官方客户端公开的 `seat`、`selfseat`、`seat_bottom` 图集读取身份标记、勾玉、阵营条、装备槽与按钮；牌局场景使用官方1600×900背景文件。源图不重绘，WPF按图集坐标读取。界面资源清单见 `src/Sanguosha.Desktop/Assets/Official/Interface/sources.json`。

120个音频源文件与映射见 `Audio/sources.json`、`Audio/catalog.json`；详见 [AUDIO.md](AUDIO.md)。

## 核验与重新下载

```powershell
powershell -ExecutionPolicy Bypass -File tools/Verify-OfficialAssets.ps1
powershell -ExecutionPolicy Bypass -File tools/Download-OfficialAssets.ps1
```

核验脚本合并立绘/卡牌、音频和界面三份清单，检查159个源文件的大小、SHA-256、文件路径和官方来源域名。重新下载脚本只读取已核对的来源清单，并在下载后核验。WPF界面验证另外解码全部25张立绘、15张牌面、26种点数和4种花色。

图片版权归原权利人；本项目为独立的非官方本地实现。

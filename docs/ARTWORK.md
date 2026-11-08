# 三国杀官方素材来源

当前武将立绘、游戏牌牌面、花色及点数均使用三国杀官网公开资源。素材直接嵌入WPF程序，运行时离线读取。

## 武将立绘

- 图鉴来源：[三国杀·一将成名官方武将图鉴](https://x.sanguosha.com/hero)。
- 对照来源：[三国杀OL官方武将列表](https://www.sanguosha.com/hero)。
- 原有25名标准武将各自使用独立PNG文件，名称与官方图鉴中的标准形象对应。
- 本机目录：`D:\WorkSpace\Codex\Sanguosha.Wpf\src\Sanguosha.Desktop\Assets\Official\Portraits`。
- 示例：[关羽图鉴](https://x.sanguosha.com/hero/2.html)、[关羽经典形象原图](https://web.sanguosha.com/10/pc/res/assets/runtime/general/big/static/100201.png)。

全部基础武将的官方图鉴地址、图片地址、文件路径、文件字节数和SHA-256记录在 [sources.json](../src/Sanguosha.Desktop/Assets/Official/sources.json)。初次下载核对日期：2026-11-05；本批增补：2026-11-07。

## 官方卡牌

- 来源：[官网公开客户端入口](https://web.sanguosha.com/10/pc/index.php)及其[资源版本清单](https://web.sanguosha.com/10/pc/version.json)。
- 原始图片：[card.png](https://web.sanguosha.com/10/pc/res/assets/cards/normal/card.png)。
- 配套切片坐标：[card.atlas](https://web.sanguosha.com/10/pc/res/assets/cards/normal/card.atlas)。
- 本机目录：`D:\WorkSpace\Codex\Sanguosha.Wpf\src\Sanguosha.Desktop\Assets\Official\Cards`。

官方图集包含完整牌面，以及独立的红/黑点数字形、四种花色。本版读取15种游戏牌的原始135×190牌面，再使用同一图集的点数与花色对应当前实体卡。WPF `CroppedBitmap`只在显示时按官方坐标读取切片，不修改下载的原始图集。

杀、闪、桃、决斗、无中生有、过河拆桥、顺手牵羊、南蛮入侵、万箭齐发、桃园结义、乐不思蜀、诸葛连弩、青龙偃月刀、八卦阵、的卢均有对应官方牌面。官方牌面上的装备信息沿用原图；游戏具体效果以本项目规则提示为准。

## 官方PC界面与音频

围桌界面采用官方客户端公开的 `seat`、`selfseat`、`seat_bottom` 图集读取身份标记、勾玉、阵营条、装备槽；牌局场景使用官方1600×900背景文件。源图不重绘，WPF按图集坐标读取。界面资源清单见 `src/Sanguosha.Desktop/Assets/Official/Interface/sources.json`。

289个官网音频（含270个人物技能/阵亡音频）源文件与映射见 `Audio/sources.json`、`Audio/catalog.json`；详见 [AUDIO.md](AUDIO.md)。

## 额外官方皮肤

当前为65名基础人物各下载两款静态皮肤，另保留默认形象，共195张不同图片；25名界将复用同人物的官方图片，为90项武将提供270个外观选项。皮肤名称和PNG地址取自对应的[官方武将图鉴](https://x.sanguosha.com/hero)页面，原文件直接来自 `web.sanguosha.com`，未重绘。130张额外皮肤原图、官网页面URL、文件大小及SHA-256记录在 `src/Sanguosha.Desktop/Assets/Official/Skins/sources.json`，随程序嵌入以供离线更换。

皮肤仅替换本项目的头像显示。没有同步下载或启用相应皮肤专属语音、动态立绘或扩展武将规则。每名武将的两款新增图像均与经典形象及另一款皮肤的文件哈希不同。

## 核验与重新下载

```powershell
powershell -ExecutionPolicy Bypass -File tools/Verify-OfficialAssets.ps1
powershell -ExecutionPolicy Bypass -File tools/Download-OfficialAssets.ps1
```

核验脚本合并立绘/卡牌、音频、界面、皮肤和归档官网图标五份清单，检查347个源文件的大小、SHA-256、文件路径和官方来源域名。重新下载脚本只读取已核对的来源清单，并在下载后核验。`--verify-skins`另外检查全部165个外观选项；WPF界面验证还解码15张牌面、26种点数和4种花色。

应用图标已替换为本项目原创「山河剑印」，由 `tools/Generate-AppIcon.py` 使用 Pillow 程序绘制，无外部图片或字体依赖。包含16、24、32、48、64、128、256像素的ICO，小尺寸使用简化山形印记；512像素PNG位于 `Assets/Branding/shanheyi-mark.png`。

之前下载的[官网ICO文件](https://web.sanguosha.com/img/favicon.ico)原样保留为 `Assets/Official/Icon/official-original.ico`，仅供来源归档和完整性核验，不再作为应用图标。`Assets/Official/Icon/sources.json`对应此归档原文件；素材重新下载工具不会覆盖原创 `app.ico`。

图片版权归原权利人；本项目为独立的非官方本地实现。

新增13项武将的准确技能版本与素材复用、缺项说明见 [扩展第一批](EXPANSION_BATCH1.md)。原文件不改绘，也不将普通图片伪称界将专属图。

## 扩展第二批素材核验

本批新增12张经典立绘、24张皮肤及45段官网MP3，共81个原始文件、14,332,441字节（约13.67MiB）；未改动既有官方原文件。所有12名新增基础人物均有经典＋两款皮肤，新增5名界将沿用同人物图片，不冒充界版专属画作。全部347项通过路径、HTTPS官方域名、字节数和SHA-256校验；36张新PNG经Pillow解码、45个新MP3经ffprobe检查音轨与正时长。

蔡文姬阵亡音、利驭专属台词未取得，静默/通用效果回退，不伪造官方素材。详细来源、缺口和权利说明见 [第二批报告](EXPANSION_BATCH2.md)。

## 第三批更新

新增17位基础人物的51张官方PNG（默认17＋备选34），当前63位基础人物、189张不同图片、84项武将共252个外观选项。12名新界将沿用同人物素材；乐进/甘夫人默认国战形象、潘凤默认阵前威豪，界面显示官网原名。所有新旧来源文件核验见EXPANSION_BATCH3.md。

## patch6增补

于吉与左慈新增2张默认立绘、4张皮肤、8段技能/阵亡MP3，全部取自官网人物页34/57并保留原始文件，新增2,146,996字节。四名界将复用同人物官网素材，不冒称专属界皮肤；义绝、清俭与无配音马术登记静默回退。详见 [本批说明](EXPANSION_BATCH4.md)。

# 对局音效与武将语音

> patch6当前合计：289个官网MP3＋30个既有卡牌报音＝319个本地MP3。新增于吉/左慈8段官网语音，90项武将均有映射；本轮11项技能语音覆盖8项，义绝/清俭/马术静默回退，详见 [EXPANSION_BATCH4.md](EXPANSION_BATCH4.md)。下文部分为历史获取记录。

224个本地MP3：194个官网文件（第二批新增34段技能台词＋11段阵亡音，另保留原有文件），另有30个经典卡牌报音，覆盖15种牌的男女声音。游戏运行时不请求网络音频。

技能台词与图鉴对照：[官方武将图鉴](https://x.sanguosha.com/hero)。每名武将读取经典形象页所列技能及语音地址，保留两条可用台词作为变体；实际播放只绑定本版已经实现的技能。阵亡资源按官方武将编号对应。来源URL、文件大小及SHA-256见 `src/Sanguosha.Desktop/Assets/Official/Audio/sources.json`。

## 新增卡牌报音来源

新增资源单独保存在 `Assets/CardVoices`，不标注为官网下载资源：杀、闪、决斗、无中生有、过河拆桥、顺手牵羊、南蛮入侵、万箭齐发、桃园结义、乐不思蜀及四种装备的28个MP3来自[无名项目](https://github.com/libccy/noname)，固定提交为 `e18a8256e01ab1357e4c7472349dceb3d0aa1a3a`。

桃的男女报音来自[太阳神三国杀](https://github.com/Mogara/QSanguosha-v2)的 `audio/card/{male,female}/peach.ogg`。保留两个原始OGG，通过FFmpeg/libmp3lame转换成WPF支持的MP3。原始文件URL、转换参数和输入/输出校验值见同目录 `sources.json`；附上两个参考项目的许可证文本。素材权利归原权利人，参考项目许可证不表示本项目取得官方素材授权。

## 播放行为

- 通过规则引擎的结构化结算事件触发；不解析中文战报，存档不记录和重播音频队列。
- 出牌和响应根据生效后的牌型选音效，武圣/龙胆等转换会同时触发对应技能台词。
- 卡牌报音使用独立声道，按使用者选择男女声，最多保留四个待播请求，顺序播放，避免杀与紧随其后的闪互相截断。
- 最多4个音效声道、1个武将语音声道；语音最多保留两个近期请求，超过4秒的待播请求丢弃，同一技能1.5秒内合并，阵亡语音优先清理旧台词。
- 部分锦囊使用官方通用切牌音效；桃使用官方回复效果音。
- “对局音效”控制效果音和卡牌报音；“武将语音”独立控制技能/阵亡台词。关闭武将语音后仍可听到牌名。“试听音效”播放杀的效果音和报音。偏好自动保存在 `%LOCALAPPDATA%/ShanHeYi/audio.json`。
- 语音或设备出现故障时在声音设置显示状态，规则结算继续运行。

## 检查

```powershell
./tools/Verify-OfficialAssets.ps1
./tools/Verify-CardVoices.ps1
./tools/Download-CardVoices.ps1
dotnet run --project src/Sanguosha.Desktop --configuration Release -- --verify-audio artifacts/audio-verification
```

Windows验证工具检查全部MP3的音轨和正时长，15种牌的男女报音与响应映射、55项武将技能/阵亡映射（9名界将复用原人物文件，缺失语音按清单回退）、重复台词合并、队列上限、阵亡优先、静音和偏好保存/恢复。工具另使用实际WPF `MediaPlayer`以零输出音量连续播放两条武将台词、杀→闪报音，验证 `MediaEnded`接续下一条和播放中的立即静音。

新增资源校验覆盖28个原始MP3、2个原始OGG、2个转换后的MP3。通常从Git恢复或下载完整发布包即可；删除转换产物后重新生成时，可用 `Download-CardVoices.ps1 -FFmpegPath <工具路径>`指定FFmpeg，输出仍须通过校验。转换工具仅用于准备资源，不是游戏运行依赖。

这属于静音解码和播放链路测试，未将其表述为人工试听。Windows执行后结果与各文件时长位于 `artifacts/audio-verification`。

音频按内容文件发布。复制程序时应复制完整发布目录或解压完整压缩包，保留 `Assets/Official/Audio` 和 `Assets/CardVoices` 子目录。

第二批新增45个官网MP3已在Linux使用ffprobe验证音轨和正时长（约0.99至8.80秒）；未实际执行新版WPF MediaPlayer验证。下列缺口写入目录/来源清单，缺音不阻断规则：

- 蔡文姬：官方 `charDead_58.mp3` 返回404，保留通用阵亡效果，不借用他人语音。
- 界吕布：官方基础人物页没有利驭专属台词，利驭静默回退；无双沿用原人物资源。
- 庞德：沿用第一批缺口。官网当前为鞬出版本，未把鞬出台词改名冒充经典猛进。
- 红颜/马术等被动技未列独立台词时不伪造；火杀/雷杀使用已有“杀”报音。
- 技能生成的兵粮寸断/铁索连环缺独立牌名报音，已在 `missingCardVoices` 显式登记，静默报牌名但保留技能音效。

历史第一批覆盖见 [扩展第一批](EXPANSION_BATCH1.md)，当前覆盖见 [扩展第二批](EXPANSION_BATCH2.md)。Windows上请重新执行 `--verify-audio`，检查解码、队列、静音及实际播放衔接。

## 第三批更新

新增87条官网MP3，当前281条官网音频＋原30条非官网卡牌报音＝311条。84项武将均有catalog条目，21项界将同人物语音复用映射实际展开供运行时读取。乐进/李典/公孙瓒/伊籍阵亡URL返回404，部分觉醒/界专属技能与袁术妄尊/同疾无匹配台词，登记缺口静默回退。新四类牌独立报牌音同样登记缺口；完整名单与来源见EXPANSION_BATCH3.md。本批Linux核验文件SHA、字节与新音轨解码；Windows真实MediaPlayer播放须本机复核。

# patch10：开战与击杀/阵亡特效

## 官方依据与素材边界
- 官方公告：[2023-09-27 十周年更新「PVP对局新增连破动画播报」](https://www.sanguosha.com/news/20230920_1618_5617)，旧官阶击杀动画被移除；本次参考十周年，而不是混用旧 OL 的「卧龙出山」等名称。
- [官方网页客户端](https://web.sanguosha.com/10/pc/index.php)、[公开资源版本目录](https://web.sanguosha.com/10/pc/version.json)。
- 击杀原文件：`https://web.sanguosha.com/10/pc/res/assets/animate/battle/killEffect/` 下 shoupo/lianpo/sanpo/sipo/wupo/liupo/qipo 的图集和所需 PNG；播报为 `res/assets/runtime/voice/effect/newkill_*.mp3`。
- 七档经官方字图逐一核对：首破·一骑当先 → 连破·世无双 → 三破·冠三军 → 四破·镇诸侯 → 五破·天下无敌 → 六破·统九州乾坤 → 七破·千古第一人。
- 开战：`res/assets/animate/battle/gamestart/SF_kaizhan_eff_putong.png/.sk`，原音 `res/assets/runtime/voice/officialRank/SF_kaizhan_eff_putong.mp3`。原始骨骼的公开纹理元数据用于精确提取「开战」书法、双刀与光晕。
- 阵亡印章复用既有官方 `Interface/seat.atlas` 的 Seat_Dead_0..4，分别为通用阵亡及主公/忠臣/反贼/内奸阵亡。2V2 使用通用印章，不误显示虚拟身份。

**原图、原音、印章均为官方素材；裁切仅保留原像素并还原图集旋转。动画轨迹、粒子、渐隐、弹出横幅为本项目的 WPF 重现，不是官方 Spine/Laya 骨骼逐帧移植，也不冒充官方客户端原生动画。** 素材版权归游卡及相关权利人，公开可读取不等于获得再分发/商用授权。

## 使用行为
- 选将入局后立即播放一次开战双刀合拢、金字显现与官方开战音效；触发早于首回合，但不人为阻塞规则推进。读档不重播。
- 阵亡时立绘转灰，落下官方身份阵亡印；攻击来源高亮，出现对应官方书法横幅。无来源/自身死亡显示无击杀来源版本，不增加击杀数。
- AI 与玩家走相同规则事件。个人本局累计击杀跨回合保留，另记本回合击杀数；额外回合/新回合仅重置回合计数。当前公开资源可验证名称与音画，未获取官方服务器计数实现；此处采用个人本局累计口径，不把神司马懿的「连破」技能当作特效规则。
- 最后一击不会被立即产生的胜负事件清除；横幅位于结果面板之上，但动画不接管鼠标，结果面板仍能操作。
- 连环伤害等多杀逐条排队，不堆叠横幅。阵亡语音 → 对应击杀播报按 FIFO 播放；下一名阵亡不会打断前一名。胜负音效等待该序列结束。普通台词/卡牌报音不会插入该序列。
- 「跳过开战/击杀特效」按钮或 Esc 可跳过当前视觉/待播横幅与专属报音，保留阵亡语音。弹窗/强制选牌的 Esc 优先级保持不变。
- 「特效：关」关闭开战/击杀动画及专属报音，阵亡灰度/身份印章作为状态信息保留；武将阵亡语音独立遵循武将语音开关。开战/击杀报音遵循音效开关与音量，全部静音停止所有声音。
- 灰度立绘沿用异步 384px 缩略图、两解码器、冻结缓存；动画总数上限 32、横幅队列上限 8、优先语音队列上限 16，完成/跳过/切换对局时清理时钟与元素。
- 新存档保存两类计数；旧存档兼容，旧档没有的历史击杀从零计，不猜测或解析战报补数。

## 素材与复现
新增官方原文件 24 个，原像素裁切 11 个，总计约 5.83 MiB。逐文件 URL/大小/SHA-256 分别记录于 `Assets/Official/KillEffects/sources.json` 与 `GameStart/sources.json`，原有官方下载/校验脚本已扩展。
正常覆盖补丁即可，无须重新下载全套素材。若自行重新制作裁切，安装 Pillow 后运行：

```powershell
python tools/Extract-KillEffectTitles.py
python tools/Extract-GameStartSprites.py
powershell -ExecutionPolicy Bypass -File tools/Verify-OfficialAssets.ps1
```

## 验证与安装
- Linux 交叉构建：`dotnet build Sanguosha.slnx -c Release -p:EnableWindowsTargeting=true`，0 警告、0 错误。
- `dotnet run --project tests/Sanguosha.Verify -c Release --no-build`：74,099 断言通过，400 个完整固定种子对局，90 武将 / 4 模式全覆盖。新增来源/自伤/无来源/AI/延迟来源/重复阵亡/最终击杀、跨回合、存读档和实际输出资源验证；400 对局的所有死亡事件均核对计数。
- Windows 原生自动验证入口已扩展：`--verify-effects artifacts/effects`、`--verify-audio artifacts/audio`，检查开战时机/跳过/禁用/读档、灰度/印章/最终一击、七人多杀队列与语音顺序。**Linux 无法运行 WPF，这两项原生验证未执行，不宣称已在 Windows 验收。**
- 关闭游戏后，将补丁直接覆盖项目根目录；无需删除任何源码文件。用 `dotnet run --project src/Sanguosha.Desktop -c Release` 或默认 Run.cmd 从源码运行；若使用旧 `artifacts/app` 发布版，需要重新 Publish，勿误开旧程序。

# patch6增量应用

适用于**已应用patch5**的 `D:\WorkSpace\Codex\Sanguosha.Wpf`。先关闭游戏/Visual Studio，并备份整个项目和存档。

1. 把patch6.zip解压到项目根目录，按同名相对路径覆盖README、docs、src与tests中的文件；不要解压到src子目录。
2. 文件清单见 [PATCH6_FILES.txt](PATCH6_FILES.txt)。**没有需要删除的文件。** 不覆盖用户数据；旧bin/obj可以自行清理后重建。压缩包不含bin、obj、SDK或参考源码。
3. Windows安装.NET 10 SDK后执行：

```powershell
dotnet build Sanguosha.slnx -c Release
dotnet run --project tests/Sanguosha.Verify -c Release
./tools/Verify-OfficialAssets.ps1
```

运行 `src\Sanguosha.Desktop\bin\Release\net10.0-windows\山河弈.exe`。核心名单为90项，界将25项，风火林山各8项。新规则版本与素材回退见 [EXPANSION_BATCH4.md](EXPANSION_BATCH4.md)。

存档升级V5，向前兼容V2/V3/V4；请保留旧存档备份，旧版程序不能读新V5。没有修改之前的墨漆鎏金主题、图标、Viewbox Stretch=Fill和窗口上下贴边设置。

Windows建议手动检查：于吉主动/响应/救援/无懈的声明与质疑弹窗；左慈开局/回合开始/结束选一技能；界关羽黑/红义绝及装备武圣分支；界马超给牌与不可闪；界陆逊藏牌/连营/回合末返还；界夏侯惇清俭牌类别与上限；化身势力/性别与私有牌区的显示；MP3正常播放。

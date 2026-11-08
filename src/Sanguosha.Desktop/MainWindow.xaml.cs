using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Sanguosha.Core;

namespace Sanguosha.Desktop
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer timer;
        private readonly GameAudio audio = new(
            Environment.GetCommandLineArgs().Any(argument =>
                argument is "--verify-ui" or "--verify-audio" or "--verify-effects" or "--verify-skins" or "--verify-dialogs"));
        private readonly List<int> targets = new();
        private GameEngine game = new();
        private Hero selectedHero = Heroes.All[0];
        private readonly Dictionary<string, HeroView> heroViews = Heroes.All.ToDictionary(hero => hero.Id, hero => new HeroView(hero));
        private GameMode selectedMode;
        private Role selectedRole;
        private bool paused;
        private bool resultShown;
        private bool initialized;
        private string? localStatus;
        private WindowState previousWindowState;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.65)
            };
            timer.Tick += (_, _) =>
            {
                if (!paused && Lobby.Visibility != Visibility.Visible &&
                    RulesOverlay.Visibility != Visibility.Visible && AudioOverlay.Visibility != Visibility.Visible &&
                    SkinsOverlay.Visibility != Visibility.Visible)
                {
                    game.Advance();
                }
            };
            BindGame(game);
            OfficialFrameLibrary.Warm("start"); OfficialFrameLibrary.Warm("kill1");
            audio.StatusChanged += RaiseAll;
            SetHeroFilter("全部");
            InitializeSkinSelector();
            RulesText.Text = BuildRules();
            initialized = true;
            timer.Start();
            RaiseAll();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<HeroView> HeroChoices { get; private set; } = new();
        public ObservableCollection<PlayerView> Opponents { get; } = new();
        public ObservableCollection<PlayerView> ResultPlayers { get; } = new();
        public ObservableCollection<CardView> HandCards { get; } = new();
        public ObservableCollection<CardView> TableCards { get; } = new();
        public ObservableCollection<GameLog> Logs { get; } = new();

        private bool HasGame => game.Phase != GamePhase.NotStarted;
        private bool IsPlay => HasGame && game.Phase == GamePhase.Play && game.Current.IsHuman;
        private int? TargetSeat => targets.Count > 0 ? targets[0] : null;
        public bool CanSave => HasGame && game.CanSave;
        public string ModeLabel => HasGame ? game.ModeName : "本地对战大厅";
        public string RoundLabel => HasGame ? $"第 {game.Round:D2} 轮   /   存活 {game.Players.Count(player => player.IsAlive)} 人" : "等待入局";
        public string DeckLabel => HasGame ? $"牌堆 {game.DeckCount:D3}     弃牌 {game.DiscardPile.Count:D3}" : "";
        public string TurnLabel => HasGame ? $"{game.Current.Hero.Name} · {game.Current.Seat + 1}号位" : "未开始";
        public string PhaseLabel => paused ? "对局已暂停" : game.Phase switch
        {
            GamePhase.Play => "出牌阶段",
            GamePhase.Discard => "弃牌阶段",
            GamePhase.Response => "等待你的响应",
            GamePhase.SelectCard => "选择目标区域的牌",
            GamePhase.GameOver => "对局结束",
            GamePhase.Resolving => "正在结算",
            _ => "选择武将后开始"
        };
        public int SeatWidth => HasGame && game.Players.Count > 4 ? 250 : 338;
        public int BoardWidth => 1060;
        public Visibility TableVisibility => HasGame && game.Players.Count == 8 ? Visibility.Collapsed : Visibility.Visible;
        public Visibility CompactTableVisibility => HasGame && game.Players.Count == 8 ? Visibility.Visible : Visibility.Collapsed;
        public IEnumerable<CardView> CompactTableCards => TableCards.TakeLast(2);
        public string TargetLabel => targets.Count > 0
            ? $"已选目标：{string.Join(" → ", targets.Select(seat => game.Players[seat].Hero.Name))}   ·   再次点击取消"
            : HasGame && !game.Human.IsAlive ? "你已阵亡，继续观看剩余角色对战。" : "点击武将选择目标 · 鼠标停留在卡牌或技能上查看说明";
        public Visibility HumanDeathVisibility => HasGame && !game.Human.IsAlive ? Visibility.Visible : Visibility.Collapsed;
        public System.Windows.Media.Imaging.BitmapSource HumanDeathStamp => HasGame
            ? OfficialInterface.DeathStamp(game.Human, game.Mode) : OfficialInterface.Get("seat", "Seat_Dead_0.png");
        public ImageBrush HumanPortrait => Art.Portrait(HasGame ? game.Human.Hero : selectedHero, HasGame && !game.Human.IsAlive);
        public string HumanName => HasGame ? game.Human.Hero.Name : selectedHero.Name;
        public string HumanIdentity => !HasGame ? "你" : game.Mode == GameMode.Team2V2 ? "青龙队 · 你" : $"{game.Human.RoleName} · 你";
        public string HumanHearts => HasGame
            ? $"{new string('●', Math.Max(0, game.Human.Health))}{new string('○', Math.Max(0, game.Human.MaxHealth - Math.Max(0, game.Human.Health)))}  {Math.Max(0, game.Human.Health)}/{game.Human.MaxHealth}"
            : "";
        public string HumanEquipment => HasGame
            ? $"范围 {game.Human.AttackRange} · " + (game.Human.Equipment.Count == 0 ? "暂无装备" :
                string.Join(" · ", game.Human.Equipment.Select(card => card.Name))) +
                (game.Human.Judgments.Count > 0 ? "\n判定区：" + string.Join("、", game.Human.Judgments.Select(card => game.Human.SupplyShortages.Contains(card.Id) || card.Kind == CardKind.SupplyShortage ? "兵粮寸断" : card.Kind == CardKind.Lightning ? "闪电" : "乐不思蜀")) : "") +
                (game.Human.TurnedOver ? "\n背面朝上" : "") + (game.Human.Chained ? " · 铁索连环" : "") +
                (game.Human.NamedCards.Any() ? "\n" + string.Join("；", game.Human.NamedZones.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key + "：" + string.Join("、", pair.Value.Select(card => card.Suit + card.RankText)))) : "") +
                (game.Human.SpecialCards.Count > 0 ? "\n不屈牌：" + string.Join("、", game.Human.SpecialCards.Select(card => card.RankText)) : "") +
                (game.Human.SkillsDisabled ? "\n断肠：武将技能已失去" : "")
            : "";
        public string HandLabel => HasGame ? $"你的手牌  {game.Human.Hand.Count:D2}  /  上限 {game.Human.HandLimit}" : "你的手牌";
        public string StatusLabel => localStatus ?? game.Status;
        public string SkillLabel => HasGame ? game.Human.EffectiveSkillText : selectedHero.Skill;
        public string SkillDescription => HasGame ? game.Human.Hero.Description + (game.Human.IncarnationHeroId != null ? "\n当前化身仅获得〔" + game.Human.IncarnationSkill + "〕：" + Heroes.Find(game.Human.IncarnationHeroId).Description : "") + (game.Human.Incarnations.Count > 0 ? "\n你的化身：" + string.Join("、", game.Human.Incarnations.Select(id => Heroes.Find(id).Name)) : "") : selectedHero.Description;
        public bool CanSkill => IsPlay && game.CanActivateSkill(game.Human);
        public Visibility ConversionVisibility => HasGame && game.Human.RuleId is "guanyu" or "zhaoyun" or "ganning" or "daqiao" or "yanliangwenchou" or "xuhuang"
            ? Visibility.Visible
            : Visibility.Collapsed;
        public string PrimaryLabel => game.Phase switch
        {
            GamePhase.Response => "打出响应  ↵",
            GamePhase.Discard => $"确认弃牌 ({HandCards.Count(card => card.IsSelected)})",
            _ => "使用卡牌  ↵"
        };
        public string SecondaryLabel => game.Phase == GamePhase.Response ? "放弃响应" : "结束回合";
        public bool CanPrimary => HasGame && game.Phase is GamePhase.Play or GamePhase.Response or GamePhase.Discard;
        public bool CanSecondary => IsPlay || game.Pending != null;
        public string ResultTitle => game.HumanWon ? "旗 开 得 胜" : "来 日 方 长";
        public string ResultLabel => $"{game.Result}  ·  共 {game.Round} 轮";
        public string AllRosterLabel => $"全部 {Heroes.All.Count}";
        public string WeiRosterLabel => $"魏  {Heroes.All.Count(hero => hero.Kingdom == "魏")}";
        public string ShuRosterLabel => $"蜀  {Heroes.All.Count(hero => hero.Kingdom == "蜀")}";
        public string WuRosterLabel => $"吴  {Heroes.All.Count(hero => hero.Kingdom == "吴")}";
        public string QunRosterLabel => $"群  {Heroes.All.Count(hero => hero.Kingdom == "群")}";
        public string SkinRosterLabel => $"官网立绘 · {Heroes.All.Count}名武将 · {SkinLibrary.Shared.All.Count}个外观选项 · 同名版本复用官网素材";
        public string SelectedHeroTitle => $"{selectedHero.Pack} · {selectedHero.Kingdom} · {selectedHero.Name}    /    {selectedHero.Skill}    /    体力 {selectedHero.Health}";
        public string SelectedHeroDescription => $"版本：{selectedHero.Ruleset}\n{selectedHero.Description}";
        public string SelectedHeroQuote => $"“{selectedHero.Quote}”";
        public string LobbyModeDescription => selectedMode switch
        {
            GameMode.Identity5 => "1主 · 1忠 · 2反 · 1内",
            GameMode.Identity8 => "1主 · 2忠 · 4反 · 1内",
            GameMode.Team2V2 => "明牌阵营 · 交错座次 · 与3号位组队",
            _ => "1主 · 1忠 · 1反 · 1内"
        };

        private void RaiseAll()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        private void Refresh()
        {
            localStatus = null;
            RefreshCardChoice();
            HashSet<int> selected = HandCards.Where(card => card.IsSelected).Select(card => card.Id).ToHashSet();
            HandCards.Clear();
            foreach (Card card in game.Human.Hand)
            {
                bool available = game.Pending == null || game.CanTransform(game.Human, card, game.Pending.Kind);
                HandCards.Add(new CardView(card, available) { IsSelected = selected.Contains(card.Id) });
            }

            targets.RemoveAll(seat => !game.Players[seat].IsAlive);
            UpdateSeats();
            TableCards.Clear();
            foreach (Card card in game.TableCards)
            {
                TableCards.Add(new CardView(card));
            }

            Logs.Clear();
            foreach (GameLog log in game.Logs.TakeLast(80))
            {
                Logs.Add(log);
            }

            LogScroll.ScrollToBottom();
            if (game.Phase == GamePhase.GameOver && !resultShown)
            {
                resultShown = true;
                ResultPlayers.Clear();
                foreach (Player player in game.Players)
                {
                    ResultPlayers.Add(new PlayerView(player, game, null));
                }

                ResultOverlay.Visibility = Visibility.Visible;
            }

            RaiseAll();
        }

        private void UpdateSeats()
        {
            Opponents.Clear();
            foreach (Player player in game.Players.Where(player => !player.IsHuman))
            {
                Opponents.Add(new PlayerView(player, game, targets.Contains(player.Seat) ? player.Seat : null));
            }
        }

        private void HandCard_Click(object sender, RoutedEventArgs e)
        {
            if (!CanPrimary || sender is not Button { Tag: CardView chosen })
            {
                return;
            }

            bool selected = chosen.IsSelected;
            if (game.Phase != GamePhase.Discard)
            {
                foreach (CardView card in HandCards)
                {
                    card.IsSelected = false;
                }
            }

            chosen.IsSelected = !selected;
            audio.PlayEffect("click");
            localStatus = chosen.IsSelected ? $"{chosen.Name} · {chosen.Card.Description}" : null;
            RaiseAll();
        }

        private void Target_Click(object sender, RoutedEventArgs e)
        {
            if (!IsPlay || sender is not Button { Tag: int seat } || !game.Players[seat].IsAlive)
            {
                return;
            }

            if (targets.Contains(seat))
            {
                targets.Remove(seat);
            }
            else
            {
                int limit = game.Human.Hero.Id is "diaochan" or "re_diaochan" or "lusu" ? 2 : 1;
                if (targets.Count >= limit)
                {
                    targets.Clear();
                }

                targets.Add(seat);
            }

            UpdateSeats();
            RaiseAll();
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            List<int> cards = HandCards.Where(card => card.IsSelected).Select(card => card.Id).ToList();
            bool success;
            if (game.Phase == GamePhase.Discard)
            {
                success = game.Discard(cards);
            }
            else if (cards.Count != 1)
            {
                localStatus = "请先选择一张手牌。";
                RaiseAll();
                return;
            }
            else if (game.Pending != null)
            {
                success = game.Respond(cards[0]);
            }
            else
            {
                success = game.Play(cards[0], TargetSeat, ConversionCheck.IsChecked == true);
            }

            if (success)
            {
                ClearSelection();
            }
        }

        private void Secondary_Click(object sender, RoutedEventArgs e)
        {
            if (game.Pending != null)
            {
                game.Respond(null);
            }
            else
            {
                game.EndTurn();
            }

            ClearSelection();
        }

        private void Skill_Click(object sender, RoutedEventArgs e)
        {
            int? card = HandCards.FirstOrDefault(item => item.IsSelected)?.Id;
            int? second = targets.Count > 1 ? targets[1] : null;
            if (game.UseSkill(card, TargetSeat, second))
            {
                ClearSelection();
            }
        }

        private void ClearSelection()
        {
            foreach (CardView card in HandCards)
            {
                card.IsSelected = false;
            }

            targets.Clear();
            ConversionCheck.IsChecked = false;
            localStatus = null;
            if (HasGame)
            {
                UpdateSeats();
            }

            RaiseAll();
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
        }

        private void Conversion_Changed(object sender, RoutedEventArgs e)
        {
            if (initialized)
            {
                RaiseAll();
            }
        }

        private void SetHeroFilter(string kingdom)
        {
            // One collection replacement, not 90 collection/layout notifications; reuse view models.
            HeroChoices = new ObservableCollection<HeroView>(HeroGalleryLayout.Filter(kingdom).Select(hero => heroViews[hero.Id]));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeroChoices)));
            HeroList.SelectedItem = HeroChoices.FirstOrDefault(choice => choice.Hero == selectedHero) ?? HeroChoices.FirstOrDefault();
            if (HeroList.SelectedItem != null) HeroList.ScrollIntoView(HeroList.SelectedItem);
        }

        private void Kingdom_Changed(object sender, RoutedEventArgs e)
        {
            if (initialized && sender is RadioButton { Tag: string kingdom })
            {
                SetHeroFilter(kingdom);
            }
        }

        private void Hero_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource == HeroList && HeroList.SelectedItem is HeroView choice && selectedHero != choice.Hero)
            {
                selectedHero = choice.Hero;
                // A gallery click must not refresh every board, skin and card binding.
                foreach (string name in new[] { nameof(SelectedHeroTitle), nameof(SelectedHeroDescription),
                    nameof(SelectedHeroQuote), nameof(HumanPortrait), nameof(HumanDeathVisibility), nameof(HumanDeathStamp), nameof(HumanName), nameof(HumanVerticalName),
                    nameof(HumanFaction), nameof(PreviewHeroLabel), nameof(SkillLabel), nameof(SkillDescription) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private void HeroList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key == Key.Enter && HeroList.SelectedItem is HeroView)
            {
                Start_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (NavigateHeroGallery(e.Key)) e.Handled = true;
        }

        private bool NavigateHeroGallery(Key key)
        {
            if (HeroChoices.Count == 0) return false;
            VirtualizingHeroPanel? panel = FindGalleryPanel(HeroList);
            int columns = panel?.Columns ?? 1;
            int current = Math.Max(0, HeroList.SelectedIndex);
            int next = key switch
            {
                Key.Left => current - 1, Key.Right => current + 1,
                Key.Up => current - columns, Key.Down => current + columns,
                Key.Home => 0, Key.End => HeroChoices.Count - 1,
                Key.PageUp => current - columns * (panel?.VisibleRows ?? 1),
                Key.PageDown => current + columns * (panel?.VisibleRows ?? 1),
                _ => -1
            };
            if (key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)) return false;
            HeroList.SelectedIndex = Math.Clamp(next, 0, HeroChoices.Count - 1);
            HeroList.ScrollIntoView(HeroList.SelectedItem);
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                (HeroList.ItemContainerGenerator.ContainerFromItem(HeroList.SelectedItem) as ListBoxItem)?.Focus();
            }));
            return true;
        }

        private static VirtualizingHeroPanel? FindGalleryPanel(DependencyObject root)
        {
            if (root is VirtualizingHeroPanel panel) return panel;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                VirtualizingHeroPanel? child = FindGalleryPanel(VisualTreeHelper.GetChild(root, index));
                if (child != null) return child;
            }
            return null;
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: string mode })
            {
                selectedMode = Enum.Parse<GameMode>(mode);
                if (RoleChoices != null)
                {
                    RoleChoices.Visibility = selectedMode == GameMode.Team2V2 ? Visibility.Collapsed : Visibility.Visible;
                }

                RaiseAll();
            }
        }

        private void Role_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: string role })
            {
                selectedRole = Enum.Parse<Role>(role);
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            BindGame(new GameEngine());
            targets.Clear();
            HandCards.Clear();
            resultShown = false;
            paused = false;
            PauseButton.Content = "暂停";
            ResultOverlay.Visibility = Visibility.Collapsed;
            Lobby.Visibility = Visibility.Collapsed;
            game.Start(selectedHero.Id, selectedRole, selectedMode);
        }

        private void NewGame_Click(object sender, RoutedEventArgs e)
        {
            audio.StopAll();
            ClearBattleEffects();
            // 大厅只暂停当前对局；用户点击“入局”时才会替换。
            Lobby.Visibility = Visibility.Visible;
            ResultOverlay.Visibility = Visibility.Collapsed;
        }

        private void BackToGame_Click(object sender, RoutedEventArgs e)
        {
            if (HasGame)
            {
                Lobby.Visibility = Visibility.Collapsed;
            }
        }

        private void Rules_Click(object sender, RoutedEventArgs e)
        {
            audio.StopAll();
            ClearBattleEffects();
            RulesOverlay.Visibility = Visibility.Visible;
        }

        private void CloseRules_Click(object sender, RoutedEventArgs e)
        {
            RulesOverlay.Visibility = Visibility.Collapsed;
        }

        private void ViewReport_Click(object sender, RoutedEventArgs e)
        {
            ResultOverlay.Visibility = Visibility.Collapsed;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!CanSave)
            {
                return;
            }

            SaveFileDialog dialog = new()
            {
                Title = "保存山河弈对局",
                Filter = "山河弈存档 (*.shy)|*.shy",
                FileName = $"山河弈_{DateTime.Now:yyyyMMdd_HHmm}.shy"
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, game.Save());
                localStatus = "对局已保存。";
                RaiseAll();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                ShowError(exception.Message);
            }
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "读取山河弈对局",
                Filter = "山河弈存档 (*.shy)|*.shy"
            };
            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                FileInfo file = new(dialog.FileName);
                if (file.Length > 2 * 1024 * 1024)
                {
                    throw new JsonException("存档文件过大。请选择有效的山河弈存档。");
                }

                GameEngine loaded = GameEngine.Load(File.ReadAllText(dialog.FileName));
                BindGame(loaded);
                resultShown = false;
                paused = false;
                PauseButton.Content = "暂停";
                ResultOverlay.Visibility = Visibility.Collapsed;
                Lobby.Visibility = Visibility.Collapsed;
                targets.Clear();
                HandCards.Clear();
                ConversionCheck.IsChecked = false;
                Refresh();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                ShowError($"无法读取存档：{exception.Message}");
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "山河弈", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Speed_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (initialized)
            {
                timer.Interval = TimeSpan.FromSeconds(e.NewValue);
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            paused = !paused;
            if (paused)
            {
                audio.StopAll();
                ClearBattleEffects();
            }
            PauseButton.Content = paused ? "继续" : "暂停";
            RaiseAll();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (SkinsOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Escape)
                {
                    CloseSkins_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }

                return;
            }

            // Let focused choice buttons receive native Enter/Space. Mandatory choices cannot
            // be silently cancelled / auto-resolved by the global gameplay shortcuts.
            if (CardChoiceOverlay.Visibility == Visibility.Visible)
            {
                if (e.Key != Key.F11)
                {
                    if (e.Key is Key.Escape or Key.F1 || e.IsRepeat && e.Key is Key.Enter or Key.Space)
                        e.Handled = true;
                    return;
                }
            }

            // This window handles PreviewKeyDown (tunneling), before the ListBox.
            // Route gallery keys here first, otherwise the global Enter guard eats lobby confirmation.
            if (Lobby.Visibility == Visibility.Visible && HeroList.IsKeyboardFocusWithin &&
                RulesOverlay.Visibility != Visibility.Visible && AudioOverlay.Visibility != Visibility.Visible)
            {
                HeroList_PreviewKeyDown(HeroList, e);
                if (e.Handled) return;
            }

            if ((Lobby.Visibility == Visibility.Visible || RulesOverlay.Visibility == Visibility.Visible ||
                AudioOverlay.Visibility == Visibility.Visible) && e.Key is Key.Enter or Key.Space)
                return; // Buttons/radio buttons in overlays own their activation keys.

            if (e.Key == Key.F1)
            {
                audio.StopAll();
                ClearBattleEffects();
                RulesOverlay.Visibility = RulesOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (e.Key == Key.F11)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = previousWindowState;
                }
                else
                {
                    previousWindowState = WindowState;
                    WindowState = WindowState.Maximized;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (AudioOverlay.Visibility == Visibility.Visible) CloseAudio_Click(this, new RoutedEventArgs());
                else if (RulesOverlay.Visibility == Visibility.Visible) CloseRules_Click(this, new RoutedEventArgs());
                else if (Lobby.Visibility == Visibility.Visible) BackToGame_Click(this, new RoutedEventArgs());
                else if (HasKillEffects || HasStartEffects) SkipKillEffects_Click(this, new RoutedEventArgs());
                else ClearSelection();
            }
            else if (Lobby.Visibility != Visibility.Visible && RulesOverlay.Visibility != Visibility.Visible &&
                AudioOverlay.Visibility != Visibility.Visible &&
                ResultOverlay.Visibility != Visibility.Visible && e.OriginalSource is not Slider)
            {
                if (e.Key == Key.Enter && CanPrimary)
                {
                    Primary_Click(this, new RoutedEventArgs());
                }
                else if (e.Key == Key.Space && CanSecondary)
                {
                    Secondary_Click(this, new RoutedEventArgs());
                }
            }

            // 防止空格/回车继续触发当前焦点按钮，导致一次按键执行两个动作。
            if (e.Key is Key.Enter or Key.Space or Key.Escape or Key.F1 or Key.F11)
            {
                e.Handled = true;
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            bool wasRunning = timer.IsEnabled;
            timer.Stop(); // A modal confirmation pumps the dispatcher: do not advance AI behind it.
            bool verification = Environment.GetCommandLineArgs().Any(argument =>
                argument is "--verify-ui" or "--verify-audio" or "--verify-effects" or "--verify-skins");
            if (HasGame && game.Phase != GamePhase.GameOver && !verification &&
                MessageBox.Show(this, "当前对局尚未结束，退出将丢失上次保存后的进度。\n确定退出游戏吗？\n如需存档，请先取消，在自己的出牌阶段保存。",
                    "退出山河弈", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                if (wasRunning) timer.Start();
                return;
            }
            ClearBattleEffects();
            audio.Dispose();
        }

        private static string BuildRules()
        {
            return "【对战模式】\n" +
                "四人身份：1主公、1忠臣、1反贼、1内奸。\n" +
                "五人身份：1主公、1忠臣、2反贼、1内奸。\n" +
                "八人身份：1主公、2忠臣、4反贼、1内奸。\n" +
                "身份局：主忠消灭全部反贼与内奸获胜；主公阵亡时反贼获胜；内奸须成为最后的生还者。主公额外1点体力并先行动，其余身份在阵亡后公开。\n\n" +
                "2V2：1、3号位为青龙队，2、4号位为朱雀队，阵营公开；消灭对方全队获胜，无主公体力加成，无击杀奖惩。玩家固定在1号位。\n\n" +
                "【基本流程】\n" +
                "所有角色初始四张牌。回合依次执行开始技能、延时锦囊判定、摸牌、出牌、弃牌、结束技能。通常每回合摸两张牌，保留手牌上限等于体力。\n" +
                "点击手牌，点击上方目标武将，再点“使用卡牌”。桃与装备无需目标。弃牌阶段可多选，需恰好弃至上限。响应时选择一张合法牌或放弃；响应牌可自动通过龙胆、武圣、倾国、急救转换。\n\n" +
                "【卡牌与距离】\n" +
                "新局119张适配牌堆（兼容108张旧档），包含无懈可击、火攻、酒、闪电、铁索连环、兵粮寸断及杀、闪、桃、决斗、无中生有、过河拆桥、顺手牵羊、南蛮入侵、万箭齐发、桃园结义、乐不思蜀及四种装备。弃牌堆耗尽后自动洗回。\n" +
                "距离沿存活角色座次取最短路径；的卢令他人计算与你的距离+1，马术令你计算与他人距离-1。杀通常范围1、每回合一次；青龙偃月刀范围3；连弩或咆哮解除次数限制。\n" +
                "八卦阵可选择判定，红色视为闪，鬼才可在判定生效前改判。乐不思蜀在下回合判定，非红桃跳过出牌阶段。锦囊无懈可击、闪电等尚未纳入首版。\n\n" +
                "【武将与技能】\n" +
                $"{Heroes.All.Count}名武将。已齐备标准核心、25名标准界将及神话风火林山；各武将的具体技能版本见选将说明（不含无名杀全部异版）。于吉点技能声明牌；左慈在开局与回合开始/结束弹窗选择化身中的单项技能，魂池仅本人可见。主动技先选择手牌与目标，再点击左下技能按钮；黄盖苦肉不需手牌；貂蝉离间依次选择两名不同角色。甘宁、关羽、赵云、大乔及发动双雄后的颜良文丑，出牌转化需勾选“技能转化”。典韦先选目标再按强袭，在弹窗选代价；烈弓、猛进、烈刃、再起与双雄的可选发动均有选择弹窗。\n" +
                "标准25将已补齐主要交互：仁德累计两张回复、制衡任意数量、结姻弃两手牌且目标须受伤男性、离间两男性；观星/突袭/刚烈/遗计均可自选。刘备未选手牌时点击技能可激将主动出杀。关羽/甘宁/大乔/徐晃可通过技能按钮转化装备。新局119张适配牌堆，含无懈反无懈、火攻、酒和闪电；仍不是官方完整军争牌堆或无名杀全部脚本。公共主公技能由技能按钮选择；田、觉醒、限定技和额外回合状态随存档保存。\n\n" +
                "【濒死、阵亡与AI】\n" +
                "体力降至0或以下时，从濒死者开始按座次询问桃救援，回复至正数后继续。阵亡者的所有牌进入弃牌堆。身份局击败反贼摸三张，主公误杀忠臣弃置全部手牌与装备。你阵亡后可观看AI完成对局。\n" +
                "AI依据自身身份进行阵营策略，会使用技能、装备、响应和救援；首版AI可知角色身份，属于本地规则对战。\n\n" +
                "【声音】\n" +
                "顶部“声音”可调整对局音效、武将语音音量，分别关闭或全部静音；设置自动保存在本机。技能发动、技能转换和阵亡会播放对应官方台词。音频离线播放，快速对局时会合并重复台词并限制排队。打开大厅、规则、声音设置或暂停对局时清除当前声音。\n\n" +
                "【保存与操作】\n" +
                "自己的出牌阶段可保存为.shy文件；保存牌堆、座次、身份、体力、装备、技能次数与战报。打开大厅或规则会暂停AI；点击“入局”才会替换当前对局。\n" +
                "Enter：使用/响应/确认弃牌；Space：结束/放弃；Esc：清除选择；F1：规则；F11：切换最大化。\n\n" +
                "【项目说明】\n" +
                "参考 nonameShijian/noname-shijian-android 的三国卡牌游戏方向，使用C#原生WPF独立实现。武将立绘与卡牌牌面来自三国杀官网公开资源，详见工程素材来源清单。本项目为非官方本地游戏。";
        }
    }
}

namespace Sanguosha.Core
{
    public enum GameCueKind
    {
        MatchStarted,
        TurnStarted,
        CardPlayed,
        CardResponded,
        SkillActivated,
        Damaged,
        Healed,
        NearDeath,
        Eliminated,
        MatchEnded,
        ArmorJudgment
    }

    public sealed record GameCue(
        GameCueKind Kind,
        string? HeroId = null,
        CardKind? Card = null,
        string? Skill = null,
        bool IsHuman = false,
        bool HumanWon = false);

    public sealed partial class GameEngine
    {
        // 独立的结算事件供声音等表现层订阅，不解析战报文本，也不写入存档。
        public event Action<GameCue>? CueRaised;

        private void EmitCue(
            GameCueKind kind,
            Player? player = null,
            CardKind? card = null,
            string? skill = null)
        {
            CueRaised?.Invoke(new GameCue(
                kind,
                player?.Hero.Id,
                card,
                skill,
                player?.IsHuman ?? false,
                HumanWon));
        }

        private void EmitSkill(Player player, string skill)
        {
            EmitCue(GameCueKind.SkillActivated, player, skill: skill);
        }

        private void EmitResponse(Player player, Card card, ResponseKind response)
        {
            CardKind effective = response switch
            {
                ResponseKind.Slash => CardKind.Slash,
                ResponseKind.Dodge => CardKind.Dodge,
                _ => CardKind.Peach
            };
            if (card.Kind != effective)
            {
                string skill = player.Hero.Id switch
                {
                    "guanyu" => "武圣",
                    "zhaoyun" => "龙胆",
                    "zhenji" => "倾国",
                    "huatuo" => "急救",
                    _ => player.Hero.Skill
                };
                EmitSkill(player, skill);
            }

            EmitCue(GameCueKind.CardResponded, player, effective);
        }
    }
}

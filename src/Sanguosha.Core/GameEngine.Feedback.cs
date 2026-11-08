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
        bool HumanWon = false,
        int? Seat = null,
        int? TargetSeat = null,
        Card? PhysicalCard = null,
        int Amount = 0,
        int? SourceSeat = null,
        DamageNature Nature = DamageNature.Normal,
        EliminationInfo? Elimination = null);

    public sealed partial class GameEngine
    {
        // 声音和动画共享结算事件；座位、实体牌和数值来自规则，不解析战报或写入存档。
        public event Action<GameCue>? CueRaised;

        private void EmitCue(
            GameCueKind kind,
            Player? player = null,
            CardKind? card = null,
            string? skill = null,
            Player? target = null,
            Card? physicalCard = null,
            int amount = 0,
            Player? source = null,
            DamageNature? nature = null,
            EliminationInfo? elimination = null)
        {
            CueRaised?.Invoke(new GameCue(
                kind,
                skill != null && player?.IncarnationSkill == skill ? player.SkillIdentity : player?.Hero.Id,
                card,
                skill,
                player?.IsHuman ?? false,
                HumanWon,
                player?.Seat,
                target?.Seat,
                physicalCard,
                amount,
                source?.Seat,
                nature ?? physicalCard?.Nature ?? DamageNature.Normal,
                elimination));
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
            if (response == ResponseKind.Peach && dyingPlayers.LastOrDefault() == player && (card.Kind == CardKind.Wine || ((player.RuleId == "dongzhuo" && player.HasAbility("酒池"))) && SuitFor(player, card) == "♠")) effective = CardKind.Wine;
            if (card.Kind != effective)
            {
                string skill = player.RuleId switch
                {
                    "guanyu" => "武圣",
                    "zhaoyun" => "龙胆",
                    "zhenji" or "re_zhenji" => "倾国",
                    "huatuo" => "急救",
                    "dongzhuo" => "酒池",
                    _ => player.Hero.Skill
                };
                EmitSkill(player, skill);
            }

            if (player.SkillIdentity == "re_zhaoyun" && player.HasAbility("涯角") && player != Current) reactions.Enqueue(done => Yajiao(player, new Card(0, effective, "", 0).Category, done));
            EmitCue(GameCueKind.CardResponded, player, effective, physicalCard: card);
        }
    }
}

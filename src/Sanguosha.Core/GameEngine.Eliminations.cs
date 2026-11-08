namespace Sanguosha.Core;

// Typed presentation metadata: kill tiers are personal match totals, not a consecutive-turn skill.
public sealed record EliminationInfo(int? KillerSeat, int VictimSeat, int MatchKills, int TurnKills)
{
    public int Tier => KillerSeat.HasValue ? Math.Clamp(MatchKills, 1, 7) : 0;
    public string TierLabel => Tier switch
    {
        1 => "首破", 2 => "连破", 3 => "三破", 4 => "四破", 5 => "五破", 6 => "六破", 7 => "七破", _ => "阵亡"
    };
    public string Title => Tier switch
    {
        1 => "一骑当先", 2 => "世无双", 3 => "冠三军", 4 => "镇诸侯", 5 => "天下无敌",
        6 => "统九州乾坤", 7 => "千古第一人", _ => "魂归山河"
    };
}

public sealed partial class GameEngine
{
    private readonly Dictionary<int, int> matchKills = new();
    private readonly Dictionary<int, int> turnKills = new();
    public int MatchKills(int seat) => matchKills.GetValueOrDefault(seat);
    public int TurnKills(int seat) => turnKills.GetValueOrDefault(seat);

    private EliminationInfo RecordElimination(Player? source, Player victim)
    {
        // Health loss/lightning without a source, and self-inflicted death, are not kills.
        int? killer = source != null && source != victim && Players.Contains(source) ? source.Seat : null;
        if (killer is int seat)
        {
            matchKills[seat] = MatchKills(seat) + 1;
            turnKills[seat] = TurnKills(seat) + 1;
        }
        return new(killer, victim.Seat, killer.HasValue ? MatchKills(killer.Value) : 0,
            killer.HasValue ? TurnKills(killer.Value) : 0);
    }
}

namespace Sanguosha.Desktop;

// Logical scene coordinates. Viewbox performs the only DPI/window-size transform.
public readonly record struct SceneSeat(double X, double Y, double Width = 172, double Height = 232);
public static class BattleSceneLayout
{
    public static SceneSeat Opponent(int count, int seat)
    {
        if (count is < 2 or > 8 || seat < 1 || seat >= count) throw new ArgumentOutOfRangeException(nameof(seat));
        (double X, double Y)[] positions = count switch
        {
            2 => new[] { (500d, 16d) },
            3 => new[] { (65d, 205d), (945d, 205d) },
            4 => new[] { (65d, 205d), (500d, 16d), (945d, 205d) },
            5 => new[] { (36d, 220d), (305d, 16d), (665d, 16d), (965d, 220d) },
            6 => new[] { (22d, 300d), (22d, 36d), (500d, 13d), (970d, 36d), (970d, 300d) },
            7 => new[] { (22d, 337d), (22d, 76d), (350d, 13d), (670d, 13d), (970d, 76d), (970d, 337d) },
            _ => new[] { (22d, 337d), (22d, 76d), (270d, 13d), (500d, 13d), (730d, 13d), (970d, 76d), (970d, 337d) }
        };
        var p = positions[seat - 1]; return new(p.X, p.Y);
    }
    public static SceneSeat Bounds(int count, int seat)
    {
        if (seat == 0 && count is >= 2 and <= 8) return new(1246, 646, 193, 253);
        var p = Opponent(count, seat); return p with { Y = p.Y + 44 };
    }
}

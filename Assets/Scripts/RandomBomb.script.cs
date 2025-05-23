using TheAdventure.Scripting;
using TheAdventure;
using System;

public class RandomBomb : IScript
{
    private DateTimeOffset _next;
    private const int RadiusNormal = 80;
    private const int RadiusHard = 40;
    private const int MinN = 2, MaxN = 5;
    private const int MinH = 1, MaxH = 3;

    public void Initialize()
    {
        _next = DateTimeOffset.UtcNow;
    }

    public void Execute(Engine e)
    {
        if (DateTimeOffset.UtcNow < _next) return;
        bool hard = e.HardMode;
        int min = hard ? MinH : MinN;
        int max = hard ? MaxH : MaxN;
        int rad = hard ? RadiusHard : RadiusNormal;
        _next = DateTimeOffset.UtcNow.AddSeconds(Random.Shared.Next(min, max));

        int px = e.PlayerX;
        int py = e.PlayerY;
        int bx = px + Random.Shared.Next(-rad, rad);
        int by = py + Random.Shared.Next(-rad, rad);
        e.AddBomb(bx, by, false);
    }
}

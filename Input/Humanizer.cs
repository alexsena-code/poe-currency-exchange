using System;
using System.Numerics;

namespace CurrencyExchange.Input;

/// <summary>
/// Humanização de input: delays GAUSSIANOS (Box-Muller) e jitter no ponto de clique (não bater sempre
/// no pixel central). Reduz o padrão robótico. Portado do projeto antigo (comprovado in-game).
/// </summary>
public static class Humanizer
{
    [ThreadStatic] private static Random _rng;
    private static Random Rng => _rng ??= new Random(Guid.NewGuid().GetHashCode());

    /// <summary>Delay gaussiano clampado em [min,max] (média no meio, ~4σ na largura).</summary>
    public static int Delay(int min, int max)
    {
        if (max <= min) return min;
        double mean = (min + max) / 2.0, std = (max - min) / 4.0;
        double u1 = 1.0 - Rng.NextDouble(), u2 = 1.0 - Rng.NextDouble();
        double g = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return Math.Clamp((int)Math.Round(mean + g * std), min, max);
    }

    /// <summary>Jitter simétrico em torno de um valor (±span).</summary>
    public static float Jitter(float center, float span) => center + (float)((Rng.NextDouble() * 2 - 1) * span);

    /// <summary>Ponto aleatório dentro de um rect, com margem interna (não colar na borda).</summary>
    public static Vector2 PointIn(SharpDX.RectangleF r, float inset = 0.30f)
    {
        float hw = Math.Max(0, r.Width * (0.5f - inset)), hh = Math.Max(0, r.Height * (0.5f - inset));
        return new Vector2(r.Center.X + Jitter(0, hw), r.Center.Y + Jitter(0, hh));
    }
}

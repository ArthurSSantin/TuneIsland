using System;

namespace TuneIsland.Core;

public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>
/// Extrai uma cor de destaque de pixels BGRA32 (ordem B, G, R, A).
/// Agrupa cores em baldes de 4 bits por canal e escolhe o balde mais "colorido" e frequente.
/// </summary>
public static class DominantColorExtractor
{
    public static readonly RgbColor Fallback = new(0x3A, 0xB0, 0xFF);

    private const int BucketCount = 16 * 16 * 16;
    private const int MinBrightness = 40;
    private const int MaxBrightness = 245;
    private const int MinChroma = 24;
    private const int MinVisibleOnDark = 150;

    public static RgbColor Extract(ReadOnlySpan<byte> bgra)
    {
        var weights = new int[BucketCount];
        var sumR = new int[BucketCount];
        var sumG = new int[BucketCount];
        var sumB = new int[BucketCount];

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            byte b = bgra[i];
            byte g = bgra[i + 1];
            byte r = bgra[i + 2];
            byte a = bgra[i + 3];

            if (a < 128)
            {
                continue;
            }

            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int chroma = max - min;

            // Descarta pixels muito escuros, muito claros ou acinzentados.
            if (max < MinBrightness || max > MaxBrightness || chroma < MinChroma)
            {
                continue;
            }

            // Pixels mais saturados pesam mais na escolha.
            int weight = 1 + chroma * 2 / 255;
            int key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);

            weights[key] += weight;
            sumR[key] += r * weight;
            sumG[key] += g * weight;
            sumB[key] += b * weight;
        }

        int best = -1;
        int bestWeight = 0;
        for (var key = 0; key < BucketCount; key++)
        {
            if (weights[key] > bestWeight)
            {
                bestWeight = weights[key];
                best = key;
            }
        }

        if (best < 0)
        {
            return Fallback;
        }

        var average = new RgbColor(
            (byte)(sumR[best] / bestWeight),
            (byte)(sumG[best] / bestWeight),
            (byte)(sumB[best] / bestWeight));

        return EnsureVisibleOnDark(average);
    }

    /// <summary>Clareia a cor até o canal máximo chegar a um mínimo legível sobre fundo preto.</summary>
    public static RgbColor EnsureVisibleOnDark(RgbColor color)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        if (max == 0 || max >= MinVisibleOnDark)
        {
            return color;
        }

        double factor = MinVisibleOnDark / (double)max;
        return new RgbColor(
            ScaleChannel(color.R, factor),
            ScaleChannel(color.G, factor),
            ScaleChannel(color.B, factor));
    }

    private static byte ScaleChannel(byte value, double factor) =>
        (byte)Math.Clamp(Math.Round(value * factor), 0, 255);
}

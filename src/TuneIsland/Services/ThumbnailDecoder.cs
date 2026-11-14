using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TuneIsland.Core;

namespace TuneIsland.Services;

public static class ThumbnailDecoder
{
    private const int ArtDecodeWidth = 256;
    private const int SampleSize = 32;

    /// <summary>Cria a imagem da capa (decodificada em tamanho reduzido e congelada).</summary>
    public static BitmapImage? CreateImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = ArtDecodeWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao decodificar a capa.");
            return null;
        }
    }

    /// <summary>Reduz a capa a uma amostra 32x32 e extrai a cor de destaque.</summary>
    public static RgbColor? ExtractAccent(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];

            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var scaled = new TransformedBitmap(
                converted,
                new ScaleTransform(
                    (double)SampleSize / converted.PixelWidth,
                    (double)SampleSize / converted.PixelHeight));

            var stride = scaled.PixelWidth * 4;
            var pixels = new byte[stride * scaled.PixelHeight];
            scaled.CopyPixels(pixels, stride, 0);

            return DominantColorExtractor.Extract(pixels);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao extrair a cor da capa.");
            return null;
        }
    }
}

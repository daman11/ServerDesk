using ServerDesk.Core;
using SkiaSharp;

namespace ServerDesk.App;
internal static class ProfileImageImport
{
    internal const int MaximumSourceBytes=4*1024*1024;
    internal static async Task<string> ImportAsync(Stream source,CancellationToken ct=default)
    {
        var bytes=await StreamLimits.ReadAsync(source,MaximumSourceBytes,ct);
        return await Task.Run(()=>Normalize(bytes),ct);
    }
    internal static string Normalize(byte[] bytes)
    {
        if(bytes.Length>MaximumSourceBytes)throw new InvalidDataException(L.T("Grafika przekracza limit 4 MiB."));
        using var data=SKData.CreateCopy(bytes); using var codec=SKCodec.Create(data);
        if(codec==null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))throw new InvalidDataException(L.T("Wybierz statyczną grafikę PNG, JPEG lub WebP."));
        var info=codec.Info;
        if(info.Width is <1 or >4096 || info.Height is <1 or >4096 || (long)info.Width*info.Height>16_000_000 || codec.FrameCount>1)throw new InvalidDataException(L.T("Grafika może mieć maksymalnie 4096 px na bok, 16 mln pikseli i jedną klatkę."));
        using var decoded=new SKBitmap(new SKImageInfo(info.Width,info.Height,SKColorType.Rgba8888,SKAlphaType.Premul));
        if(codec.GetPixels(decoded.Info,decoded.GetPixels())!=SKCodecResult.Success)throw new InvalidDataException(L.T("Nie można odczytać całej grafiki."));
        using var square=new SKBitmap(new SKImageInfo(128,128,SKColorType.Rgba8888,SKAlphaType.Premul));
        using(var canvas=new SKCanvas(square))
        {
            canvas.Clear(SKColors.Transparent); canvas.Translate(64,64);
            var rotated=codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var scale=Math.Min(128f/(rotated?info.Height:info.Width),128f/(rotated?info.Width:info.Height)); canvas.Scale(scale,scale);
            switch(codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: canvas.Scale(-1,1); break;
                case SKEncodedOrigin.BottomRight: canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Scale(1,-1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1,-1); break;
                case SKEncodedOrigin.RightTop: canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.RotateDegrees(90); canvas.Scale(-1,1); break;
                case SKEncodedOrigin.LeftBottom: canvas.RotateDegrees(-90); break;
            }
            using var image=SKImage.FromBitmap(decoded); canvas.DrawImage(image,new SKRect(-info.Width/2f,-info.Height/2f,info.Width/2f,info.Height/2f),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.None));
        }
        using var thumbnail=SKImage.FromBitmap(square); using var encoded=thumbnail.Encode(SKEncodedImageFormat.Png,100);
        var result=encoded.ToArray(); ProfileAppearance.ValidatePng(result); return Convert.ToBase64String(result);
    }
}

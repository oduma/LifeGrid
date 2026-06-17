using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace LifeGrid.Presentation;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("DMMono-Regular.ttf",         "DMMono-Regular");
                fonts.AddFont("DMMono-Medium.ttf",          "DMMono-Medium");
                fonts.AddFont("DMMono-Italic.ttf",          "DMMono-Italic");
                fonts.AddFont("ShareTechMono-Regular.ttf",  "ShareTechMono-Regular");
                fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbolsRounded");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

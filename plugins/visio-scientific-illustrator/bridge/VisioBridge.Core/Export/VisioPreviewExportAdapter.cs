using System.Runtime.InteropServices;
using VisioBridge.Core.Abstractions;
using Visio = Microsoft.Office.Interop.Visio;

namespace VisioBridge.Core.Export;

internal sealed class VisioPreviewExportAdapter : IPreviewExportAdapter
{
    private readonly Visio.InvisibleApp _application;
    private readonly Visio.Page _page;

    public VisioPreviewExportAdapter(Visio.InvisibleApp application, Visio.Page page)
    {
        _application = application;
        _page = page;
    }

    public RasterSettings ReadSettings()
    {
        var settings = _application.Settings;
        try
        {
            settings.GetRasterExportSize(
                out var sizeMode,
                out var width,
                out var height,
                out var sizeUnits);
            settings.GetRasterExportResolution(
                out var resolutionMode,
                out var resolutionWidth,
                out var resolutionHeight,
                out var resolutionUnits);
            return new RasterSettings(
                checked((int)Math.Round(width)),
                checked((int)Math.Round(height)),
                (int)resolutionMode,
                settings.RasterExportQuality,
                (int)sizeMode,
                (int)sizeUnits,
                resolutionWidth,
                resolutionHeight,
                (int)resolutionUnits,
                (int)settings.RasterExportColorFormat,
                settings.RasterExportBackgroundColor);
        }
        finally
        {
            Marshal.ReleaseComObject(settings);
        }
    }

    public void ApplyPreviewSettings(int widthPx)
    {
        var dimensions = ReadPageDimensions();
        var heightPx = Math.Max(
            1,
            checked((int)Math.Round(widthPx * dimensions.Height / dimensions.Width)));
        var settings = _application.Settings;
        try
        {
            settings.SetRasterExportSize(
                Visio.VisRasterExportSize.visRasterFitToCustomSize,
                widthPx,
                heightPx,
                Visio.VisRasterExportSizeUnits.visRasterPixel);
            settings.RasterExportQuality = 100;
        }
        finally
        {
            Marshal.ReleaseComObject(settings);
        }
    }

    public void ExportPage(string absolutePngPath) => _page.Export(absolutePngPath);

    public void RestoreSettings(RasterSettings saved)
    {
        var settings = _application.Settings;
        try
        {
            settings.SetRasterExportSize(
                (Visio.VisRasterExportSize)saved.SizeMode,
                Math.Max(saved.WidthPx, 1),
                Math.Max(saved.HeightPx, 1),
                (Visio.VisRasterExportSizeUnits)saved.SizeUnits);
            settings.SetRasterExportResolution(
                (Visio.VisRasterExportResolution)saved.Resolution,
                Math.Max(saved.ResolutionWidth, 1),
                Math.Max(saved.ResolutionHeight, 1),
                (Visio.VisRasterExportResolutionUnits)saved.ResolutionUnits);
            settings.RasterExportQuality = saved.Quality;
            settings.RasterExportColorFormat =
                (Visio.VisRasterExportColorFormat)saved.ColorFormat;
            settings.RasterExportBackgroundColor = saved.BackgroundColor;
        }
        finally
        {
            Marshal.ReleaseComObject(settings);
        }
    }

    private (double Width, double Height) ReadPageDimensions()
    {
        var pageSheet = _page.PageSheet;
        Visio.Cell? width = null;
        Visio.Cell? height = null;
        try
        {
            width = pageSheet.CellsU["PageWidth"];
            height = pageSheet.CellsU["PageHeight"];
            return (width.ResultIU, height.ResultIU);
        }
        finally
        {
            if (height is not null) Marshal.ReleaseComObject(height);
            if (width is not null) Marshal.ReleaseComObject(width);
            Marshal.ReleaseComObject(pageSheet);
        }
    }
}

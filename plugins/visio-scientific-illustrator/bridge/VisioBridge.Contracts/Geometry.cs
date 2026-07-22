namespace VisioBridge.Contracts;

public sealed record PixelPoint(double X, double Y);

public sealed record PixelBox(double X, double Y, double Width, double Height);

public sealed record VisioBox(
    double PinXInches,
    double PinYInches,
    double WidthInches,
    double HeightInches);

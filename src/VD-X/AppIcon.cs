namespace VDX;

static class AppIcon
{
    public static Icon Load(Size? size = null)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico")!;
        return size is Size s ? new Icon(stream, s) : new Icon(stream);
    }
}

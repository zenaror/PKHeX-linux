// Linux port (net10.0 target only): the shared sprite logic is written against the GDI+ type names.
// On the cross-platform target, those names resolve to the SkiaSharp bitmap type.
global using Bitmap = SkiaSharp.SKBitmap;
global using Image = SkiaSharp.SKBitmap;

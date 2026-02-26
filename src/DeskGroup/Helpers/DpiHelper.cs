using System;
using System.Windows;
using System.Windows.Media;

namespace DeskGroup.Helpers;

public static class DpiHelper
{
    public static double GetScaleFactor(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformToDevice.M11;
        return 1.0;
    }

    public static int DipToPixel(double dip, double scaleFactor)
    {
        return (int)Math.Round(dip * scaleFactor);
    }

    public static double PixelToDip(int pixel, double scaleFactor)
    {
        return pixel / scaleFactor;
    }

    public static int GetSystemIconSize(double scaleFactor)
    {
        return (int)Math.Round(48 * scaleFactor);
    }
}
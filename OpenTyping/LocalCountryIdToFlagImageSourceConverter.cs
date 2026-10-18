using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using FamFamFam.Flags.Wpf;

namespace OpenTyping
{
    public class LocalCountryIdToFlagImageSourceConverter : IValueConverter
    {
        private static readonly CountryIdToFlagImageSourceConverter OriginalConverter = new CountryIdToFlagImageSourceConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string code = value as string;
            if (string.Equals(code, "kaa", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Uri uriResource = new Uri("/Resources/Flags/kaa.png", UriKind.Relative);
                    StreamResourceInfo resourceStream = Application.GetResourceStream(uriResource);
                    if (resourceStream != null)
                    {
                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = resourceStream.Stream;
                        bitmapImage.EndInit();
                        return bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        System.IO.File.WriteAllText("debug_flag.txt", ex.ToString());
                    }
                    catch {}
                    System.Diagnostics.Debug.WriteLine($"Failed to load Karakalpak flag image: {ex}");
                    return null;
                }
            }
            return OriginalConverter.Convert(value, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return OriginalConverter.ConvertBack(value, targetType, parameter, culture);
        }
    }
}

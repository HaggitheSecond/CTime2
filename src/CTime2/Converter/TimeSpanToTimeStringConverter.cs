using System;
using Windows.UI.Xaml.Data;
using UwCore.Extensions;

namespace CTime2.Converter
{
    public class TimeSpanToTimeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TimeSpan == false)
                return value;

            TimeSpan t = (TimeSpan) value;
            return t.ToDateTime().ToString("t");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
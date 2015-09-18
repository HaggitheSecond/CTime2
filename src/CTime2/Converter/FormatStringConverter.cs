﻿using System;
using System.Globalization;
using Windows.UI.Xaml.Data;

namespace CTime2.Converter
{
    public class FormatStringConverter : IValueConverter
    {
        public string FormatString { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var format = this.GetDisplayFormat();
            return string.IsNullOrWhiteSpace(format) ? value : string.Format(new CultureInfo(language), format, value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        private string GetDisplayFormat()
        {
            if (string.IsNullOrWhiteSpace(this.FormatString))
                return string.Empty;

            if (this.FormatString.Contains("{"))
                return this.FormatString;

            return string.Format("{{0:{0}}}", this.FormatString);
        }
    }
}
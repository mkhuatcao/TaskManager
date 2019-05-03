using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TaskManager
{
    public class BytesToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Math.Round(((long)value / Math.Pow(10, 6)), 2).ToString() + " MB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class PriorityToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((int)value == 4)
                return "Idle";
            if ((int)value > 4 && (int)value < 8)
                return "Below normal";
            if ((int)value == 8)
                return "Normal";
            if ((int)value > 8 && (int)value < 13)
                return "Above normal";
            if ((int)value == 13)
                return "High";
            if ((int)value == 24)
                return "Real time";
            return "Access Denied";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class IdToResumeFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return ManagedProcessesWindow.ResumeFlags[(int)value];
            }
            catch (Exception)
            {

                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    public class IdToLogFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return ManagedProcessesWindow.LogFlags[(int)value];
            }
            catch (Exception)
            {

                return false;
            }
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}

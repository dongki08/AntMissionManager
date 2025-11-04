using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AntMissionManager.Utilities;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return booleanValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue)
        {
            return booleanValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Collapsed;
        }
        return true;
    }
}

public class BatteryLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int batteryLevel)
        {
            return batteryLevel switch
            {
                <= 20 => "#F44336", // Red
                <= 50 => "#FF9800", // Orange
                _ => "#4CAF50"      // Green
            };
        }
        return "#4CAF50";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NavigationStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int navigationState)
        {
            return navigationState switch
            {
                0 => "#FF9800", // Received - Orange
                1 => "#2196F3", // Accepted - Blue
                2 => "#F44336", // Rejected - Red
                3 => "#4CAF50", // Started - Green
                4 => "#9C27B0", // Completed - Purple
                5 => "#757575", // Cancelled - Gray
                _ => "#424242"  // Unknown - Dark Gray
            };
        }
        return "#424242";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BatteryLevelToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int batteryLevel)
        {
            // 최대 너비는 80, 배터리 레벨(0-100)에 비례하여 계산
            var width = (batteryLevel / 100.0) * 80.0;
            return Math.Max(5, width); // 최소 5로 설정하여 0%일 때도 약간 보이게
        }
        return 5.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
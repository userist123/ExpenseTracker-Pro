using System.Globalization;
using System.Windows.Data;
using ExpenseTrackerPro.Core.Models;

namespace ExpenseTrackerPro.Converters;

/// <summary>Shows a <see cref="TransactionType"/> as its Romanian label.</summary>
public sealed class TransactionTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TransactionType type ? type.ToDisplayName() : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

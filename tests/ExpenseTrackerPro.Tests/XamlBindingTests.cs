using System.Text.RegularExpressions;
using System.Windows.Input;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.ViewModels;

namespace ExpenseTrackerPro.Tests;

/// <summary>
/// The WPF window cannot be loaded on Linux, so this checks the most likely UI mistake instead: a binding path in
/// MainWindow.xaml that does not exist on the view model (or on the row types used in the item templates).
/// </summary>
public partial class XamlBindingTests
{
    private static readonly string Xaml = File.ReadAllText(FindFile("src", "ExpenseTrackerPro", "Views", "MainWindow.xaml"));

    [Fact]
    public void Every_binding_path_exists_on_the_view_model_or_a_row_type()
    {
        var known = PropertyNames(typeof(MainViewModel))
            .Concat(PropertyNames(typeof(TransactionItem)))
            .Concat(PropertyNames(typeof(CategoryExpense)))
            .ToHashSet();

        var paths = BindingPattern().Matches(Xaml).Select(m => m.Groups["path"].Value).Distinct().ToList();

        Assert.NotEmpty(paths);
        Assert.Empty(paths.Except(known));
    }

    [Fact]
    public void Every_command_binding_is_a_command_property_of_the_view_model()
    {
        var commands = typeof(MainViewModel).GetProperties()
            .Where(p => typeof(ICommand).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .ToHashSet();

        var used = CommandPattern().Matches(Xaml).Select(m => m.Groups["path"].Value).Distinct().ToList();

        Assert.NotEmpty(used);
        Assert.Empty(used.Except(commands));
    }

    [Fact]
    public void Every_view_model_command_is_reachable_from_the_window()
    {
        var used = CommandPattern().Matches(Xaml).Select(m => m.Groups["path"].Value).ToHashSet();
        var commands = typeof(MainViewModel).GetProperties()
            .Where(p => typeof(ICommand).IsAssignableFrom(p.PropertyType))
            .Select(p => p.Name)
            .ToList();

        // EditTransactionCommand is also invoked from the row double-click handler in the code-behind.
        Assert.Empty(commands.Except(used));
    }

    private static IEnumerable<string> PropertyNames(Type type)
    {
        return type.GetProperties().Select(p => p.Name);
    }

    private static string FindFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpenseTrackerPro.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. relativeParts]);
    }

    [GeneratedRegex(@"\{Binding\s+(?:Path=)?(?<path>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex BindingPattern();

    [GeneratedRegex(@"Command=""\{Binding\s+(?<path>[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex CommandPattern();
}

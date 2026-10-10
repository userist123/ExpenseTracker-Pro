using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using ExpenseTrackerPro.Core.Data;
using ExpenseTrackerPro.Core.Services;
using ExpenseTrackerPro.ViewModels;
using ExpenseTrackerPro.Views;

namespace ExpenseTrackerPro;

public partial class App : Application
{
    private const string AppCulture = "ro-RO";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureCulture();

        try
        {
            var window = CreateMainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Aplicația nu a putut porni, baza de date locală nu poate fi deschisă.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "ExpenseTracker Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <summary>Makes numbers, dates and WPF bindings use Romanian formatting regardless of the Windows region.</summary>
    private static void ConfigureCulture()
    {
        var culture = CultureInfo.GetCultureInfo(AppCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(AppCulture)));
    }

    private static MainWindow CreateMainWindow()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExpenseTrackerPro");
        Directory.CreateDirectory(folder);

        var factory = AppDbContextFactory.ForFile(Path.Combine(folder, "expense-tracker.db"));
        using (var db = factory.CreateDbContext())
        {
            DatabaseInitializer.Initialize(db);
        }

        var settings = new SettingsService(factory);
        var transactions = new TransactionService(factory, settings);
        var viewModel = new MainViewModel(transactions);

        return new MainWindow { DataContext = viewModel };
    }
}

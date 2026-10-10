using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpenseTrackerPro.Core.Models;
using ExpenseTrackerPro.ViewModels;

namespace ExpenseTrackerPro.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Double-clicking a row loads it into the form, like the "Editează" button.</summary>
    private void TransactionRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not DataGridRow { Item: TransactionItem item })
        {
            return;
        }

        viewModel.SelectedTransaction = item;
        viewModel.EditTransactionCommand.Execute(null);
    }
}

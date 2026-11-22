using System.Windows;
using Kopilka.App.Presentation;

namespace Kopilka.App;

/// <summary>
/// Окно справочника категорий. Вся логика — в CategoriesViewModel,
/// окно только показывает и закрывается.
/// </summary>
public partial class CategoriesWindow : Window
{
    public CategoriesWindow(CategoriesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

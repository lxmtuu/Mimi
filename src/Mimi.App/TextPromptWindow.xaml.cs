using System.Windows;
using Mimi.Localization;
using Mimi.Services;

namespace Mimi;

/// <summary>Small modal text-input dialog used for playlist names.</summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(
        string title,
        string prompt,
        string initialValue,
        LocalizationService? localizationService = null)
    {
        Loc = localizationService ?? new LocalizationService();
        InitializeComponent();
        DataContext = this;

        Title = title;
        PromptLabel.Text = prompt;
        InputTextBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowChromeHelper.ApplyDarkTitleBar(this);
    }

    public LocalizationService Loc { get; }

    public string Value => InputTextBox.Text.Trim();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0)
        {
            InputTextBox.Focus();
            return;
        }

        DialogResult = true;
    }
}

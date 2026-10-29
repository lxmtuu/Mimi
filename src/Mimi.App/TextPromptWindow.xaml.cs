using System.Windows;

namespace Mimi;

/// <summary>Small modal text-input dialog used for playlist names.</summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string prompt, string initialValue)
    {
        InitializeComponent();

        Title = title;
        PromptLabel.Text = prompt;
        InputTextBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

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

using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mimi.Behaviors;

namespace Mimi.Tests;

public sealed class ItemClickCommandBehaviorTests
{
    [Fact]
    public void CompletedPlainClickOnItem_ExecutesCommandWithThatItem()
    {
        RunOnSta(() =>
        {
            var command = new RecordingCommand();
            var listView = new ListView
            {
                ItemsSource = new[] { "Cyan", "Magenta" }
            };
            ItemClickCommandBehavior.SetCommand(listView, command);

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = listView
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var container = Assert.IsType<ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(1));

                container.RaiseEvent(NewMouseButtonEvent(
                    Mouse.PreviewMouseDownEvent));
                container.RaiseEvent(NewMouseButtonEvent(
                    Mouse.MouseUpEvent));

                Assert.Equal(new object?[] { "Magenta" }, command.Parameters);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ClickSurvivesContainerRefreshBetweenPressAndRelease()
    {
        RunOnSta(() =>
        {
            var cyan = "Cyan";
            var magenta = "Magenta";
            var command = new RecordingCommand();
            var listView = new ListView
            {
                ItemsSource = new[] { cyan, magenta }
            };
            ItemClickCommandBehavior.SetCommand(listView, command);

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = listView
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var pressContainer = Assert.IsType<ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(1));
                pressContainer.RaiseEvent(NewMouseButtonEvent(
                    Mouse.PreviewMouseDownEvent));

                // Startup collection/view refreshes may rebuild containers
                // between the two halves of the first click. The item under
                // pointer has not changed, so activation should survive.
                listView.ItemsSource = null;
                listView.ItemsSource = new[] { cyan, magenta };
                window.UpdateLayout();

                var releaseContainer = Assert.IsType<ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(1));
                releaseContainer.RaiseEvent(NewMouseButtonEvent(
                    Mouse.MouseUpEvent));

                Assert.Equal(new object?[] { magenta }, command.Parameters);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ClickOutsideAnyItem_DoesNotExecuteCommand()
    {
        RunOnSta(() =>
        {
            var command = new RecordingCommand();
            var listView = new ListView
            {
                ItemsSource = new[] { "Cyan" }
            };
            ItemClickCommandBehavior.SetCommand(listView, command);

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = listView
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                listView.RaiseEvent(NewMouseButtonEvent(
                    Mouse.PreviewMouseDownEvent));
                listView.RaiseEvent(NewMouseButtonEvent(
                    Mouse.MouseUpEvent));

                Assert.Empty(command.Parameters);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MouseUpWithoutPrecedingPressOnItem_DoesNotExecuteCommand()
    {
        RunOnSta(() =>
        {
            var command = new RecordingCommand();
            var listView = new ListView
            {
                ItemsSource = new[] { "Cyan" }
            };
            ItemClickCommandBehavior.SetCommand(listView, command);

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = listView
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var container = Assert.IsType<ListViewItem>(
                    listView.ItemContainerGenerator.ContainerFromIndex(0));

                container.RaiseEvent(NewMouseButtonEvent(
                    Mouse.MouseUpEvent));

                Assert.Empty(command.Parameters);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static MouseButtonEventArgs NewMouseButtonEvent(
        RoutedEvent routedEvent) =>
        new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = routedEvent
        };

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(
                $"STA/WPF item-click test failed: {failure}");
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public List<object?> Parameters { get; } = new();

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Parameters.Add(parameter);
    }
}

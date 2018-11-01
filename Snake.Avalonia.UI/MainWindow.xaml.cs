using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using SneakySnake;

namespace Snake.Avalonia.UI
{
    public class MainWindow : Window
    {
        private SnakeMailboxNetwork game = GameBuilder.buildSnakeGame(new PostOffice.MailboxNetwork(), 0);
        private const int CELL_SIZE = 20;
        private bool isInitialized = false;
        private Grid _fieldGrid;
        private TextBlock _scoreTextBlock;
        private TextBlock _attackTxt;
        private TextBlock _speedTxt;
        private TextBlock _exitTxt;
        private Dictionary<(int, int), Canvas> _canvasMap = new Dictionary<(int, int), Canvas>(20 * 50);
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            this.Activated += ((s, e) =>
            {
                if (!isInitialized)
                {
                    game.timerAgent.Post(TimerCommand.Start);
                    isInitialized = true;
                }
            });
            _fieldGrid = this.FindControl<Grid>("FieldGrid");
            _scoreTextBlock = this.FindControl<TextBlock>("ScoreTxt");
            _attackTxt = this.FindControl<TextBlock>("AttackTxt");
            _speedTxt = this.FindControl<TextBlock>("SpeedTxt");
            _exitTxt = this.FindControl<TextBlock>("ExitTxt");
            SetTextBlockStyle(_scoreTextBlock);
            SetTextBlockStyle(_exitTxt);
            SetTextBlockStyle(_speedTxt);
            SetTextBlockStyle(_attackTxt);
            InitGrid(game.gameAgent.GetState().Value);
            game.AddSubscriberInterop(RedrawGrid);
        }

        private void InitGrid(GameState gameState)
        {
            var field = ((GameFrame.Frame)gameState.gameFrame).Item;
            for (int i = 0; i < field.height; i++)
            {
                _fieldGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(CELL_SIZE) });
            }
            for (int i = 0; i < field.width; i++)
            {
                _fieldGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(CELL_SIZE) });
            }
            for (int i = 0; i < field.width; i++)
            {
                for (int j = field.height - 1; j >= 0; j--)
                {
                    var canvas = new Canvas();
                    canvas.Background = Brushes.Brown;
                    _fieldGrid.Children.Add(canvas);
                    Grid.SetColumn(canvas, i);
                    Grid.SetRow(canvas, j);
                    _canvasMap.Add((i, j), canvas);
                }
            }
        }

        private void RedrawGrid(GameState gameState)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _scoreTextBlock.Text = $"{gameState.snake.length} Points";
            });

            if (gameState.gameFrame.IsFrame)
            {
                var field = ((GameFrame.Frame)gameState.gameFrame).Item;
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var attackTicks = gameState.PerkAvailableAfter(SnakePerk.Attack);
                    var speedTicks = gameState.PerkAvailableAfter(SnakePerk.Speed);
                    var exitTicks = gameState.ExitOpensAfter();
                    _attackTxt.Text = attackTicks > 0
                        ? $"You can attack after {attackTicks} points"
                        : "You can attack!";
                    _speedTxt.Text = speedTicks > 0
                        ? $"You can speed up after {speedTicks} points"
                        : "You can speed up!";
                    _exitTxt.Text = exitTicks > 0
                        ? $"Exit opens after {exitTicks} points"
                        : "Exit is open!";
                    for (int i = 0; i < field.width; i++)
                    {
                        for (int j = 0; j < field.height; j++)
                        {
                            var y = field.height - j - 1;
                            var cell = field.cellMap[i, y].content;
                            var canvas = _canvasMap[(i, j)];
                            canvas.Background = CellContentToBrush(cell, gameState);
                        }
                    }
                }, DispatcherPriority.Send);
            }
            else if (gameState.gameFrame.IsEnd)
            {
                var end = (GameFrame.End)(gameState.gameFrame);
                var result = end.Item;
                string message = result.IsWin ? $"It's a WIN! You've got {result.points} points!" : $"You've lost. {result.Item}";
            }
        }

        private static IBrush CellContentToBrush(CellContent content, GameState gameState)
        {
            if (content.IsEater) { return Brushes.Red; }
            if (content.IsEmpty) { return Brushes.DarkSlateGray; }
            if (content.IsExit) { return Brushes.Blue; }
            if (content.IsFood) { return Brushes.Purple; }
            if (content.IsObstacle) { return Brushes.Orange; }
            if (content.IsSnakeCell)
            {
                if (gameState.snake.HasPerk(SnakePerk.Attack))
                {
                    return Brushes.DarkRed;
                }
                return gameState.snake.HasPerk(SnakePerk.Speed) ? Brushes.CadetBlue : Brushes.Green;
            }
            return Brushes.HotPink;
        }

        private void SetTextBlockStyle(TextBlock textBlock)
        {
            textBlock.FontSize = 20;
            textBlock.FontFamily = "Times New Roman";
            textBlock.FontWeight = FontWeight.Bold;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            ConsoleKey key = 0;
            switch (e.Key)
            {
                case Key.A:
                    key = ConsoleKey.A;
                    break;
                case Key.Q:
                    key = ConsoleKey.Q;
                    break;
                case Key.R:
                    key = ConsoleKey.R;
                    break;
                case Key.S:
                    key = ConsoleKey.S;
                    break;
                case Key.Left:
                    key = ConsoleKey.LeftArrow;
                    break;
                case Key.Up:
                    key = ConsoleKey.UpArrow;
                    break;
                case Key.Down:
                    key = ConsoleKey.DownArrow;
                    break;
                case Key.Right:
                    key = ConsoleKey.RightArrow;
                    break;
                case Key.Space:
                    key = ConsoleKey.Spacebar;
                    break;
                case Key.G:
                    key = ConsoleKey.G;
                    break;
            }
            var command = GameInterface.readUserCommand(key);
            if (command != null)
            {
                game = GameInterface.passCommandInterop(RedrawGrid, game, command.Value);
            }
            if (e.Key == Key.Q)
            {
                this.Close();
            }
            base.OnKeyDown(e);
        }
    }
}

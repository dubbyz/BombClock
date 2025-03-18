using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace BombClock
{
    public partial class MainWindow : Window
    {
        // Window click-through constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        private DispatcherTimer? _timer;
        private DateTime _startTime;
        private int _totalSeconds = 0;  
        private bool _beepStarted = false;
        private bool _explosionDisplayed = false;
        private DateTime _explosionStart;
        private Grid? _inputGrid;
        private Grid? _timerGrid;
        private Image? _bombImage;
        private TextBlock? _countdownText;
        private System.Media.SoundPlayer? _plantedSound;
        private System.Media.SoundPlayer? _beepSound;

        public MainWindow()
        {
            InitializeComponent();

            // Load sounds from Assets folder
            string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            _plantedSound = new System.Media.SoundPlayer(Path.Combine(assetsPath, "bomb_planted.wav"));
            _beepSound = new System.Media.SoundPlayer(Path.Combine(assetsPath, "beep_sound.wav"));

            CreateTimeInputUI();
        }

private void ApplyDigitalFont(Control control)
{
    try
    {
        control.FontFamily = new FontFamily("Digital-7");
    }
    catch
    {
        // If Digital-7 isn't installed, fall back to Consolas
        control.FontFamily = new FontFamily("Consolas");
    }
}

private void ApplyDigitalFont(TextBlock textBlock)
{
    try
    {
        textBlock.FontFamily = new FontFamily("Digital-7");
    }
    catch
    {
        // If Digital-7 isn't installed, fall back to Consolas
        textBlock.FontFamily = new FontFamily("Consolas");
    }
}

private void CreateTimeInputUI()
{
    // Set window properties
    this.WindowStyle = WindowStyle.None;
    this.ResizeMode = ResizeMode.NoResize;
    this.Background = new SolidColorBrush(Colors.Black);
    this.Height = 188;

    // Create main window grid
    Grid mainGrid = new Grid();
    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) }); // Title bar
    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Content

    // Create title bar grid
    Grid titleBar = new Grid();
    titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Title
    titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Spacer
    titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // X button

    // Make the title bar draggable
    titleBar.MouseLeftButtonDown += (s, e) => 
    {
        this.DragMove();
    };

    // Add this style to make the cursor show it's draggable
    titleBar.Cursor = Cursors.SizeAll;

    // Create and setup title text
    Border titleBorder = new Border();
    titleBorder.BorderBrush = new SolidColorBrush(Colors.Lime);
    titleBorder.BorderThickness = new Thickness(2);
    titleBorder.Background = new SolidColorBrush(Colors.Black);
    titleBorder.Height = 30;
    // Add these lines to make the border stretch
    Grid.SetColumnSpan(titleBorder, 2); // Span across title and spacer columns
    titleBorder.HorizontalAlignment = HorizontalAlignment.Stretch;

    TextBlock bombClockText = new TextBlock();
    bombClockText.Text = "BOMB CLOCK";
    bombClockText.FontSize = 18;
    bombClockText.Foreground = new SolidColorBrush(Colors.Lime);
    bombClockText.VerticalAlignment = VerticalAlignment.Center;
    bombClockText.HorizontalAlignment = HorizontalAlignment.Center; // Center the text horizontally
    bombClockText.Margin = new Thickness(0); // Remove margin
    titleBorder.Child = bombClockText;
    ApplyDigitalFont(bombClockText);

    Grid.SetColumn(titleBorder, 0);
    titleBar.Children.Add(titleBorder);

    // Create close button
    Button closeButton = new Button();
    closeButton.Content = "X";
    closeButton.FontWeight = FontWeights.Bold;
    closeButton.FontSize = 16;
    closeButton.Width = 46;
    closeButton.Height = 30;
    closeButton.Padding = new Thickness(0);
    closeButton.Margin = new Thickness(0);
    closeButton.Click += (sender, e) => this.Close();
    closeButton.HorizontalAlignment = HorizontalAlignment.Right;

    // Create and apply close button template
    ControlTemplate closeTemplate = new ControlTemplate(typeof(Button));
    FrameworkElementFactory closeBorder = new FrameworkElementFactory(typeof(Border));
    closeBorder.Name = "border";
    closeBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.Black));
    closeBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Colors.Lime));
    closeBorder.SetValue(Border.BorderThicknessProperty, new Thickness(2));

    FrameworkElementFactory closeContent = new FrameworkElementFactory(typeof(ContentPresenter));
    closeContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
    closeContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
    closeBorder.AppendChild(closeContent);
    closeTemplate.VisualTree = closeBorder;

    // Add hover trigger for red background
    Trigger hoverTrigger = new Trigger();
    hoverTrigger.Property = Button.IsMouseOverProperty;
    hoverTrigger.Value = true;
    hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Colors.Red), "border"));
    hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Colors.Red), "border"));
    closeTemplate.Triggers.Add(hoverTrigger);

    closeButton.Template = closeTemplate;
    closeButton.Foreground = new SolidColorBrush(Colors.Lime);

    Grid.SetColumn(closeButton, 2);
    titleBar.Children.Add(closeButton);

    // Add title bar to main grid
    Grid.SetRow(titleBar, 0);
    mainGrid.Children.Add(titleBar);

    // Create input grid
    _inputGrid = new Grid();
    _inputGrid.VerticalAlignment = VerticalAlignment.Center;
    _inputGrid.HorizontalAlignment = HorizontalAlignment.Center;
    _inputGrid.Margin = new Thickness(10, 10, 10, 10);

    // Create row definitions
    _inputGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
    _inputGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
    _inputGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
    
    // Create column definitions for hours, minutes, seconds
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
    _inputGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

    // Add input grid to main grid
    Grid.SetRow(_inputGrid, 1);
    mainGrid.Children.Add(_inputGrid);

    // Title
    TextBlock setTimerText = new TextBlock();
    setTimerText.Text = "Set Bomb Timer:";
    setTimerText.FontSize = 24;
    setTimerText.Foreground = new SolidColorBrush(Colors.Lime);
    setTimerText.Margin = new Thickness(0, 0, 0, 10);
    setTimerText.HorizontalAlignment = HorizontalAlignment.Center;
    Grid.SetRow(setTimerText, 0);
    Grid.SetColumnSpan(setTimerText, 6);
    _inputGrid.Children.Add(setTimerText);
    ApplyDigitalFont(setTimerText);

    // Hours input
    TextBox hoursInput = new TextBox();
    hoursInput.Width = 40;
    hoursInput.Height = 30;
    hoursInput.Text = "01";
    hoursInput.TextAlignment = TextAlignment.Center;
    hoursInput.FontSize = 24;
    hoursInput.Name = "HoursInput";
    hoursInput.Margin = new Thickness(5);
    hoursInput.PreviewTextInput += NumberValidationTextBox;
    hoursInput.MaxLength = 2;
    hoursInput.Background = new SolidColorBrush(Colors.Black);
    hoursInput.Foreground = new SolidColorBrush(Colors.Lime);
    hoursInput.BorderBrush = new SolidColorBrush(Colors.Lime);
    hoursInput.BorderThickness = new Thickness(2);
    Grid.SetRow(hoursInput, 1);
    Grid.SetColumn(hoursInput, 0);
    _inputGrid.Children.Add(hoursInput);
    ApplyDigitalFont(hoursInput);

    TextBlock hoursLabel = new TextBlock();
    hoursLabel.Text = "h";
    hoursLabel.Foreground = new SolidColorBrush(Colors.Lime);
    hoursLabel.VerticalAlignment = VerticalAlignment.Center;
    hoursLabel.Margin = new Thickness(0, 0, 10, 0);
    Grid.SetRow(hoursLabel, 1);
    Grid.SetColumn(hoursLabel, 1);
    _inputGrid.Children.Add(hoursLabel);
    ApplyDigitalFont(hoursLabel);

    // Minutes input
    TextBox minsInput = new TextBox();
    minsInput.Width = 40;
    minsInput.Height = 30;
    minsInput.Text = "47";
    minsInput.TextAlignment = TextAlignment.Center;
    minsInput.FontSize = 24;
    minsInput.Name = "MinsInput";
    minsInput.Margin = new Thickness(5);
    minsInput.PreviewTextInput += NumberValidationTextBox;
    minsInput.MaxLength = 2;
    minsInput.Background = new SolidColorBrush(Colors.Black);
    minsInput.Foreground = new SolidColorBrush(Colors.Lime);
    minsInput.BorderBrush = new SolidColorBrush(Colors.Lime);
    minsInput.BorderThickness = new Thickness(2);
    Grid.SetRow(minsInput, 1);
    Grid.SetColumn(minsInput, 2);
    _inputGrid.Children.Add(minsInput);
    ApplyDigitalFont(minsInput);

    TextBlock minsLabel = new TextBlock();
    minsLabel.Text = "m";
    minsLabel.Foreground = new SolidColorBrush(Colors.Lime);
    minsLabel.VerticalAlignment = VerticalAlignment.Center;
    minsLabel.Margin = new Thickness(0, 0, 10, 0);
    Grid.SetRow(minsLabel, 1);
    Grid.SetColumn(minsLabel, 3);
    _inputGrid.Children.Add(minsLabel);
    ApplyDigitalFont(minsLabel);

    // Seconds input
    TextBox secsInput = new TextBox();
    secsInput.Width = 40;
    secsInput.Height = 30;
    secsInput.Text = "47";
    secsInput.TextAlignment = TextAlignment.Center;
    secsInput.FontSize = 24;
    secsInput.Name = "SecsInput";
    secsInput.Margin = new Thickness(5);
    secsInput.PreviewTextInput += NumberValidationTextBox;
    secsInput.MaxLength = 2;
    secsInput.Background = new SolidColorBrush(Colors.Black);
    secsInput.Foreground = new SolidColorBrush(Colors.Lime);
    secsInput.BorderBrush = new SolidColorBrush(Colors.Lime);
    secsInput.BorderThickness = new Thickness(2);
    Grid.SetRow(secsInput, 1);
    Grid.SetColumn(secsInput, 4);
    _inputGrid.Children.Add(secsInput);
    ApplyDigitalFont(secsInput);

    TextBlock secsLabel = new TextBlock();
    secsLabel.Text = "s";
    secsLabel.Foreground = new SolidColorBrush(Colors.Lime);
    secsLabel.VerticalAlignment = VerticalAlignment.Center;
    Grid.SetRow(secsLabel, 1);
    Grid.SetColumn(secsLabel, 5);
    _inputGrid.Children.Add(secsLabel);
    ApplyDigitalFont(secsLabel);

    // Start button
    Button startButton = new Button();
    startButton.Content = "Start Clock";
    startButton.Margin = new Thickness(0, 15, 0, 0);
    startButton.Padding = new Thickness(20, 10, 20, 10);
    startButton.FontSize = 24;
    startButton.Click += StartButton_Click;

    // Create custom template for start button
    ControlTemplate startTemplate = new ControlTemplate(typeof(Button));
    FrameworkElementFactory startBorder = new FrameworkElementFactory(typeof(Border));
    startBorder.Name = "border";
    startBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.Black));
    startBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Colors.Lime));
    startBorder.SetValue(Border.BorderThicknessProperty, new Thickness(2));
    startBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

    FrameworkElementFactory startContent = new FrameworkElementFactory(typeof(ContentPresenter));
    startContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
    startContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
    startContent.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
    startBorder.AppendChild(startContent);
    startTemplate.VisualTree = startBorder;

    // Add hover trigger for start button
    Trigger startHoverTrigger = new Trigger();
    startHoverTrigger.Property = Button.IsMouseOverProperty;
    startHoverTrigger.Value = true;
    startHoverTrigger.Setters.Add(
        new Setter(
            Border.BorderBrushProperty, 
            new SolidColorBrush(Color.FromRgb(126, 180, 234)),
            "border"
        )
    );
    startTemplate.Triggers.Add(startHoverTrigger);

    startButton.Template = startTemplate;
    startButton.Foreground = new SolidColorBrush(Colors.Lime);
    startButton.Background = new SolidColorBrush(Colors.Black);
    startButton.BorderBrush = new SolidColorBrush(Colors.Lime);
    startButton.MinWidth = 150;
    startButton.MinHeight = 40;
    startButton.FocusVisualStyle = null;

    Grid.SetRow(startButton, 2);
    Grid.SetColumnSpan(startButton, 6);
    startButton.HorizontalAlignment = HorizontalAlignment.Center;
    _inputGrid.Children.Add(startButton);
    ApplyDigitalFont(startButton);

    // Set the window content
    this.Content = mainGrid;
}

private void CreateTimerUI()
{
    // Reset window background to transparent when showing the timer
    this.Background = Brushes.Transparent;
    
    // Create the timer UI from scratch
    _timerGrid = new Grid();
    
    // Create the bomb image
    _bombImage = new Image();
    _bombImage.Source = new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "bomb.png")));
    _bombImage.Stretch = Stretch.Uniform;
    _bombImage.HorizontalAlignment = HorizontalAlignment.Center;
    _bombImage.VerticalAlignment = VerticalAlignment.Center;
    _bombImage.Opacity = 0.7;  // Set bomb image to 70% opacity
    _timerGrid.Children.Add(_bombImage);
    _timerGrid.VerticalAlignment = VerticalAlignment.Top;
    _timerGrid.Margin = new Thickness(0, 0, 0, 0); // Place the bomb back at the top
    
    // Create the countdown text directly
    _countdownText = new TextBlock();
    _countdownText.Foreground = new SolidColorBrush(Colors.Lime);
    _countdownText.FontSize = 24;
    _countdownText.FontWeight = FontWeights.Bold;
    _countdownText.Margin = new Thickness(138, 35, 0, 0);
    _countdownText.HorizontalAlignment = HorizontalAlignment.Left;
    _countdownText.VerticalAlignment = VerticalAlignment.Top;
    
    // Flag to track if we're using the fallback font
    bool usingFallbackFont = false;
    
    // Try to use the Digital-7 font if installed, otherwise fall back to Consolas
    try
    {
        _countdownText.FontFamily = new FontFamily("Digital-7");
    }
    catch
    {
        // If Digital-7 isn't installed, fall back to Consolas
        _countdownText.FontFamily = new FontFamily("Consolas");
        usingFallbackFont = true;
    }
    
    // Add the text directly to the grid
    _timerGrid.Children.Add(_countdownText);
    
    // If we're using the fallback font, show a message
    if (usingFallbackFont)
    {
        // Create a message about the missing font
        TextBlock fontMessage = new TextBlock();
        fontMessage.Text = "Digital-7.ttf NOT INSTALLED";
        fontMessage.Foreground = new SolidColorBrush(Colors.Lime);
        fontMessage.FontSize = 10;
        fontMessage.FontWeight = FontWeights.Bold;
        fontMessage.HorizontalAlignment = HorizontalAlignment.Center;
        fontMessage.VerticalAlignment = VerticalAlignment.Bottom;
        fontMessage.Margin = new Thickness(0, 0, 0, 5); // 5px from bottom
        
        // Add the message to the grid
        _timerGrid.Children.Add(fontMessage);
    }
    
    // Set the window content to the timer grid
    this.Content = _timerGrid;
}

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Only allow numbers
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get references to the input textboxes
                TextBox hoursInput = null;
                TextBox minsInput = null;
                TextBox secsInput = null;
                
                foreach (UIElement element in _inputGrid.Children)
                {
                    if (element is TextBox textBox)
                    {
                        if (textBox.Name == "HoursInput") hoursInput = textBox;
                        else if (textBox.Name == "MinsInput") minsInput = textBox;
                        else if (textBox.Name == "SecsInput") secsInput = textBox;
                    }
                }

                // Parse values (default to 0 if parsing fails)
                int hours = 0, mins = 0, secs = 0;
                if (hoursInput != null) int.TryParse(hoursInput.Text, out hours);
                if (minsInput != null) int.TryParse(minsInput.Text, out mins);
                if (secsInput != null) int.TryParse(secsInput.Text, out secs);

                // Calculate total seconds
                _totalSeconds = (hours * 3600) + (mins * 60) + secs;

                // Ensure there's at least some time
                if (_totalSeconds <= 0)
                {
                    MessageBox.Show("Please enter a valid time greater than 0 seconds.", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create the timer UI
                CreateTimerUI();

                // Start the timer
                StartTimer();
                
                // Position the window and make it click-through
                PositionWindow();
                MakeWindowClickThrough();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PositionWindow()
        {
            // Move the window to the top-right of the primary screen
            double offsetRight = 10;
            double offsetTop = 10;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.ActualWidth - offsetRight;
            this.Top = offsetTop;
        }

        private void StartTimer()
        {
            _startTime = DateTime.Now;
            _plantedSound.Play();  // "bomb has been planted"

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100); // ~10x/sec
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _startTime).TotalSeconds;
            int secondsLeft = _totalSeconds - (int)elapsed;

            if (!_explosionDisplayed)
            {
                if (secondsLeft <= 0)
                {
                    _explosionDisplayed = true;
                    _bombImage.Source = new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "explosion.png")));
                    _countdownText.Text = "";
                    _explosionStart = DateTime.Now;
                }
                else
                {
                    if (!_beepStarted && secondsLeft <= 47)
                    {
                        _beepStarted = true;
                        _beepSound.Play();
                    }
                    
                    int h = secondsLeft / 3600;
                    int m = (secondsLeft % 3600) / 60;
                    int s = secondsLeft % 60;
                    _countdownText.Text = $"{h:D2}:{m:D2}:{s:D2}";
                }
            }
            else
            {
                var explosionTime = (DateTime.Now - _explosionStart).TotalMilliseconds;
                if (explosionTime >= 4700)
                {
                    this.Close();
                }
            }
        }

        // -------------------------------------------------------------
        // CLICK-THROUGH LOGIC: Make the window ignore mouse clicks.
        // -------------------------------------------------------------
        private void MakeWindowClickThrough()
        {
            // Get the native window handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Read existing extended style
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // OR in WS_EX_TRANSPARENT to ignore clicks
            exStyle |= WS_EX_TRANSPARENT;

            // Write updated style
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

    }
}
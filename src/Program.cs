using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Slideshow;

internal static class Program
{
    /// <summary>
    /// Token source.
    /// </summary>
    private static CancellationTokenSource TokenSource { get; } = new();

    /// <summary>
    /// All files in the slideshow.
    /// </summary>
    private static List<string> Files { get; } = new();
    
    /// <summary>
    /// Whether to get files recursively in all image paths.
    /// </summary>
    private static bool GetFileRecursively { get; set; }
    
    /// <summary>
    /// List of all image filename patterns to scan for.
    /// </summary>
    private static List<string> ImageFilePatterns { get; } = new();

    /// <summary>
    /// Interval between slides, in milliseconds.
    /// </summary>
    private static long Interval { get; set; } = 5000;

    /// <summary>
    /// All open forms.
    /// </summary>
    private static List<Form> OpenWindows { get; } = new();

    /// <summary>
    /// Randomizer, for next slide.
    /// </summary>
    private static Random Randomizer { get; } = new();

    /// <summary>
    /// Screen to display on.
    /// </summary>
    private static Screen DisplayScreen { get; set; } = Screen.PrimaryScreen!;

    /// <summary>
    /// Program version.
    /// </summary>
    private static Version Version { get; } = new(0, 1, 15);

    /// <summary>
    /// Init all the things...
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        
        if (args.Length == 0 ||
            args.Any(n => n is "-h" or "--h" or "-help" or "--help" or "/?"))
        {
            Application.Run(CreateAboutProgramWindow());
            return;
        }

        if (!ParseCmdArgs(args, out var errorWindow))
        {
            Application.Run(errorWindow);
            return;
        }

        if (ImageFilePatterns.Count == 0)
        {
            ImageFilePatterns.Add("*");
        }

        foreach (var pattern in ImageFilePatterns)
        {
            var files = GetFiles(pattern, SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                if (!Files.Contains(file))
                {
                    Files.Add(file);
                }
            }
        }

        if (Files.Count == 0)
        {
            Application.Run(CreateErrorParameterWindow(string.Empty, "No files where found."));
            return;
        }

        Cursor.Hide();

        var backgroundWindow = new Form
        {
            BackColor = System.Drawing.Color.Black,
            FormBorderStyle = FormBorderStyle.None,
            Location = DisplayScreen.Bounds.Location,
            Size = DisplayScreen.Bounds.Size,
            StartPosition = FormStartPosition.Manual,
            WindowState = FormWindowState.Maximized
        };

        backgroundWindow.KeyDown += HandleKeyDown;
        backgroundWindow.Shown += (_, _) =>
        {
            try
            {
                LoadNewImage();
                TransitionImages();
            }
            catch
            {
                //
            }

            var dynamicInterval = Interval == 0;

            Interval = 5000;
            var interval = Interval;
            
            while (!TokenSource.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    LoadNewImage();
                }
                catch
                {
                    //
                }

                var elapsed = stopwatch.ElapsedMilliseconds;

                if (dynamicInterval &&
                    elapsed + Interval > interval)
                {
                    interval = elapsed + Interval;
                }
                
                while (!TokenSource.IsCancellationRequested)
                {
                    if (stopwatch.ElapsedMilliseconds > interval)
                    {
                        break;
                    }
                    
                    Thread.Sleep(100);
                    Application.DoEvents();
                }
                
                TransitionImages();
            }
        };
        
        OpenWindows.Add(backgroundWindow);
        Application.Run(backgroundWindow);
    }

    /// <summary>
    /// Calculate the display size.
    /// </summary>
    /// <param name="originalHeight">Original height.</param>
    /// <param name="originalWidth">Original width.</param>
    /// <param name="maxHeight">Max height.</param>
    /// <param name="maxWidth">Max width.</param>
    /// <returns>Display height and width.</returns>
    private static (int displayHeight, int displayWidth) CalculateDisplaySize(
        int originalHeight,
        int originalWidth,
        int maxHeight,
        int maxWidth)
    {
        var height = originalHeight;
        var width = originalWidth;

        while (true)
        {
            double factor;
            double res;

            if (height > maxHeight)
            {
                factor = (double)height / maxHeight;
                res = width / factor;

                height = maxHeight;
                width = (int)res;
            }
            else if (width > maxWidth)
            {
                factor = (double)width / maxWidth;
                res = height / factor;

                height = (int)res;
                width = maxWidth;
            }
            else
            {
                break;
            }
        }

        return new(height, width);
    }

    /// <summary>
    /// Close all windows.
    /// </summary>
    private static void CloseApplication()
    {
        if (OpenWindows.Count == 0)
        {
            return;
        }

        while (true)
        {
            var form = OpenWindows.LastOrDefault();

            if (form is null)
            {
                break;
            }

            OpenWindows.RemoveAt(OpenWindows.Count - 1);

            AnimateFormOpacity(form, false);

            form.Dispose();
        }

        OpenWindows.Clear();
    }

    /// <summary>
    /// Convert a SharpImage into a System.Drawing image.
    /// </summary>
    /// <param name="image">SharpImage.</param>
    /// <param name="imageFormat">Format to convert with.</param>
    /// <returns>System.Drawing image.</returns>
    private static System.Drawing.Image ConvertToSystemDrawingImage(
        SixLabors.ImageSharp.Image image,
        SixLabors.ImageSharp.Formats.IImageFormat? imageFormat = null)
    {
        using var stream = new MemoryStream();
		
        image.Save(stream, imageFormat ?? SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);
		
        var output = System.Drawing.Image.FromStream(stream);

        return output;
    }

    /// <summary>
    /// Create a window that shows program info, usage, and options.
    /// </summary>
    /// <returns>Window.</returns>
    private static Form CreateAboutProgramWindow()
    {
        const int height = 450;
        const int width = 800;
        const int fontSize = 9;

        var left = (DisplayScreen.Bounds.Width - DisplayScreen.Bounds.Left - width) / 2;
        var top = (DisplayScreen.Bounds.Height - DisplayScreen.Bounds.Top - height) / 2;
        
        var window = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Location = new System.Drawing.Point(left, top),
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new System.Drawing.Size(width, height),
            StartPosition = FormStartPosition.Manual,
            Text = "About Slideshow"
        };

        window.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                window.Close();
            }
        };

        var preText = new StringBuilder();
        var postText = new StringBuilder();

        preText.AppendLine($"Slideshow v{Version}");
        preText.AppendLine();
        preText.AppendLine("Usage:");
        
        postText.AppendLine("You can add multiple paths to images.");
        postText.AppendLine();
        postText.AppendLine("Options:");

        var preTextLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(30, 30),
            Text = preText.ToString()
        };

        var execLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericMonospace, fontSize),
            Location = new System.Drawing.Point(50, 80),
            Text = "slideshow <path-to-images> <options>"
        };
        
        var postTextLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(30, 110),
            Text = postText.ToString()
        };

        var options = new Dictionary<string, string>
        {
            {"--recursive", "Get files recursively for each folder path specified."},
            {"--files <pattern>", "Set which file patterns to look for. Defaults to *.*"},
            {"--interval <milliseconds>", "Set milliseconds between each slide. Defaults to 5000."},
            {"--screen <index>", "Set index of screen to use. See list below for values. Defaults to primary screen."}
        };

        var screens = new StringBuilder();

        for (var index = 0; index < Screen.AllScreens.Length; index++)
        {
            screens.AppendLine($"Screen #{index} - {Screen.AllScreens[index].Bounds} {(Screen.AllScreens[index].Primary ? " - (primary)" : string.Empty)}");
        }
        
        var optionsLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericMonospace, fontSize),
            Location = new System.Drawing.Point(50, 160),
            Text = string.Join(Environment.NewLine, options.Select(n => n.Key))
        };
        
        var descriptionsLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(260, 160),
            Text = string.Join(Environment.NewLine, options.Select(n => n.Value))
        };

        var screensLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(260, 235),
            Text = screens.ToString()
        };
        
        var button = new Button
        {
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point((width - 100) / 2, 285 + (Screen.AllScreens.Length * 25)),
            Size = new System.Drawing.Size(100, 30),
            Text = "Ok",
            DialogResult = DialogResult.Abort
        };

        button.Click += (_, _) =>
        {
            window.Close();
        };

        window.AcceptButton = button;
        window.CancelButton = button;

        window.Controls.Add(preTextLabel);
        window.Controls.Add(execLabel);
        window.Controls.Add(postTextLabel);
        window.Controls.Add(optionsLabel);
        window.Controls.Add(descriptionsLabel);
        window.Controls.Add(screensLabel);
        window.Controls.Add(button);

        return window;
    }

    /// <summary>
    /// Create a window with an appropriate error message.
    /// </summary>
    /// <param name="parameter">Parameter which failed.</param>
    /// <param name="errorMessage">Error message.</param>
    /// <returns>Window.</returns>
    private static Form CreateErrorParameterWindow(string parameter, string errorMessage)
    {
        const int height = 260;
        const int width = 300;
        const int fontSize = 9;

        var left = (DisplayScreen.Bounds.Width - DisplayScreen.Bounds.Left - width) / 2;
        var top = (DisplayScreen.Bounds.Height - DisplayScreen.Bounds.Top - height) / 2;
        
        var window = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Location = new System.Drawing.Point(left, top),
            MaximizeBox = false,
            MinimizeBox = false,
            Size = new System.Drawing.Size(width, height),
            StartPosition = FormStartPosition.Manual,
            Text = parameter == string.Empty ? "Error" : "Invalid Parameter"
        };
        
        window.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                window.Close();
            }
        };

        var preTextLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(30, 30),
            Text = parameter == string.Empty ? "Error" : "Parameter"
        };

        var parameterLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericMonospace, fontSize),
            Location = new System.Drawing.Point(30, 60),
            Text = parameter
        };

        var errorMessageLabel = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point(30, 90),
            Text = errorMessage
        };

        var button = new Button
        {
            Font = new Font(FontFamily.GenericSansSerif, fontSize),
            Location = new System.Drawing.Point((width - 100) / 2, 160),
            Size = new System.Drawing.Size(100, 30),
            Text = "Ok",
            DialogResult = DialogResult.Abort
        };

        button.Click += (_, _) =>
        {
            window.Close();
        };

        window.AcceptButton = button;
        window.CancelButton = button;

        window.Controls.Add(preTextLabel);
        window.Controls.Add(parameterLabel);
        window.Controls.Add(errorMessageLabel);
        window.Controls.Add(button);
        
        return window;
    }

    /// <summary>
    /// Create a new slideshow window with the given file path.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>Window.</returns>
    private static Form? CreateNewSlideshowWindow(string path)
    {
        var windowHeight = DisplayScreen.Bounds.Height;
        var windowWidth = DisplayScreen.Bounds.Width;

        var blurredImage = SixLabors.ImageSharp.Image.Load(path);
        var displayImage = SixLabors.ImageSharp.Image.Load(path);

        if (TokenSource.IsCancellationRequested)
        {
            return null;
        }

        var (displayHeight, displayWidth) = CalculateDisplaySize(
            displayImage.Height,
            displayImage.Width,
            windowHeight,
            windowWidth);

        blurredImage.Mutate(n => n
            .Resize(displayImage.Width / 100, displayImage.Height / 100)
            .Resize(windowWidth, 0));

        var blurredPictureBox = new PictureBox
        {
            BackgroundImage = ConvertToSystemDrawingImage(blurredImage),
            BackgroundImageLayout = ImageLayout.Center,
            Location = new System.Drawing.Point(0, 0),
            Size = new System.Drawing.Size(windowWidth, windowHeight)
        };

        if (TokenSource.IsCancellationRequested)
        {
            return null;
        }

        displayImage.Mutate(n => n
            .Resize(displayWidth, displayHeight));

        var displayPictureBox = new PictureBox
        {
            BackgroundImage = ConvertToSystemDrawingImage(displayImage),
            BackgroundImageLayout = ImageLayout.Center,
            Location = new System.Drawing.Point((windowWidth - displayWidth) / 2, (windowHeight - displayHeight) / 2),
            Size = new System.Drawing.Size(displayWidth, displayHeight)
        };

        if (TokenSource.IsCancellationRequested)
        {
            return null;
        }

        var window = new Form
        {
            BackColor = System.Drawing.Color.Black,
            FormBorderStyle = FormBorderStyle.None,
            Location = DisplayScreen.Bounds.Location,
            Opacity = 0,
            Size = DisplayScreen.Bounds.Size,
            StartPosition = FormStartPosition.Manual,
            Text = path,
            WindowState = FormWindowState.Maximized
        };

        displayPictureBox.KeyDown += HandleKeyDown;
        window.KeyDown += HandleKeyDown;

        window.Controls.Add(displayPictureBox);
        window.Controls.Add(blurredPictureBox);

        window.Show();

        return window;
    }

    /// <summary>
    /// Animate fade out, or in, of a window.
    /// </summary>
    /// <param name="form">Window.</param>
    /// <param name="positive">Positive, or negative, addition.</param>
    /// <param name="milliseconds">Total milliseconds of animation.</param>
    /// <param name="interval">Milliseconds between each animation frame.</param>
    private static void AnimateFormOpacity(
        Form form,
        bool positive = true,
        int milliseconds = 250,
        double interval = 25)
    {
        var wait = (int)(milliseconds / interval);
        var factor = interval / milliseconds;

        double opacity = positive ? 0 : 1;

        while (!TokenSource.IsCancellationRequested)
        {
            form.Opacity = opacity;
            Thread.Sleep(wait);
            Application.DoEvents();

            if (positive)
            {
                opacity += factor;
            }
            else
            {
                opacity -= factor;
            }

            if (opacity is > 1 or < 0)
            {
                break;
            }
        }

        form.Opacity = positive ? 1 : 0;
    }

    /// <summary>
    /// Handle keyboard events.
    /// </summary>
    private static void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        TokenSource.Cancel();
        CloseApplication();
    }

    /// <summary>
    /// Load and prepare the next slideshow image window.
    /// </summary>
    private static void LoadNewImage()
    {
        var next = CreateNewSlideshowWindow(Files[Randomizer.Next(Files.Count)]);

        if (next is not null &&
            !TokenSource.IsCancellationRequested)
        {
            OpenWindows.Add(next);
        }
    }
    
    /// <summary>
    /// Parse command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="errorWindow">Error window, if any parameters fails parsing.</param>
    /// <returns>Success.</returns>
    private static bool ParseCmdArgs(IReadOnlyList<string> args, [NotNullWhen(returnValue: false)] out Form? errorWindow)
    {
        errorWindow = null;

        var skip = false;

        for (var i = 0; i < args.Count; i++)
        {
            if (skip)
            {
                skip = false;
                continue;
            }

            switch (args[i])
            {
                // Get files recursively for each folder path specified.
                case "--recursive":
                    GetFileRecursively = true;
                    break;
                
                // Set which file patterns to look for.
                case "--files":
                    if (i == args.Count - 1)
                    {
                        errorWindow = CreateErrorParameterWindow(args[i], "Must be followed by a valid folder path.");
                        return false;
                    }

                    ImageFilePatterns.Add(args[i + 1]);
                    skip = true;
                    
                    break;
                
                // Set milliseconds between each slide.
                case "--interval":
                    if (i == args.Count - 1)
                    {
                        errorWindow = CreateErrorParameterWindow(args[i], "Must be followed by a valid number of milliseconds.");
                        return false;
                    }

                    if (!long.TryParse(args[i + 1], out var milliseconds) ||
                        milliseconds < 0)
                    {
                        errorWindow = CreateErrorParameterWindow(args[i + 1], "Is not a valid number of milliseconds and positive.");
                        return false;
                    }

                    Interval = milliseconds;
                    skip = true;
                    
                    break;
                
                // Set index of screen to use.
                case "--screen":
                    if (i == args.Count - 1)
                    {
                        errorWindow = CreateErrorParameterWindow(args[i], "Must be followed by a valid index for a screen.");
                        return false;
                    }
                    
                    if (!int.TryParse(args[i + 1], out var index) ||
                        index < 0 || 
                        index >= Screen.AllScreens.Length)
                    {
                        errorWindow = CreateErrorParameterWindow(args[i + 1], "Is not a valid screen index.");
                        return false;
                    }

                    DisplayScreen = Screen.AllScreens[index];
                    skip = true;

                    break;
                
                // Treat it as a path/pattern.
                default:
                    ImageFilePatterns.Add(args[i]);
                    break;
            }
        }

        return errorWindow is null;
    }

    /// <summary>
    /// Transition the previous and next slideshow window.
    /// </summary>
    private static void TransitionImages()
    {
        Form? prev = null;
        Form? next = null;
        
        switch (OpenWindows.Count)
        {
            case 2:
                next = OpenWindows[1];
                break;
            
            case 3:
                prev = OpenWindows[1];
                next = OpenWindows[2];
                break;
        }

        if (prev is not null)
        {
            AnimateFormOpacity(prev, false);
            
            prev.Dispose();

            if (OpenWindows.Count > 0)
            {
                OpenWindows.RemoveAt(1);    
            }
        }

        if (next is not null)
        {
            AnimateFormOpacity(next);
        }
    }
    
    /// <summary>
    /// Returns the names of files (including their paths) that match the specified search pattern in the specified directory.
    /// </summary>
    /// <param name="pathAndPattern">Path and/or search pattern.</param>
    /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include all subdirectories or only the current directory.</param>
    /// <returns>An array of file names, including their paths.</returns>
    public static string[] GetFiles(string pathAndPattern, SearchOption searchOption)
    {
        var (path, pattern) = GetPathAndPattern(pathAndPattern);

        return Directory.GetFiles(path, pattern, searchOption);
    }
    
    /// <summary>
    /// Separates the path and search pattern from the single input string.
    /// </summary>
    /// <param name="pathAndPattern">Path and/or search pattern.</param>
    /// <returns>Path and pattern, separated.</returns>
    private static Tuple<string, string> GetPathAndPattern(string pathAndPattern)
    {
        string path;
        string pattern;
        
        if (Directory.Exists(pathAndPattern))
        {
            path = pathAndPattern;
            pattern = "*";
        }
        else if (File.Exists(pathAndPattern))
        {
            path = Path.GetDirectoryName(pathAndPattern) ?? string.Empty;
            pattern = Path.GetFileName(pathAndPattern);
        }
        else
        {
            var index = pathAndPattern.LastIndexOf(Path.DirectorySeparatorChar);
            
            if (index == -1)
            {
                path = string.Empty;
                pattern = pathAndPattern;
            }
            else
            {
                path = pathAndPattern[..index];
                pattern = pathAndPattern[(index + 1)..];
            }
        }
        
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "*";
        }

        return new(path, pattern);
    }
}
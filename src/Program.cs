using Timer = System.Windows.Forms.Timer;

namespace Slideshow
{
    internal static class Program
    {
        #region Properties

        /// <summary>
        /// Which screen to use.
        /// </summary>
        private static Screen PrimaryScreen { get; set; } = Screen.PrimaryScreen;

        /// <summary>
        /// Main window.
        /// </summary>
        private static Form Window { get; set; } = null!;

        /// <summary>
        /// Window height.
        /// </summary>
        private static int WindowHeight { get; set; }

        /// <summary>
        /// Window width.
        /// </summary>
        private static int WindowWidth { get; set; }

        /// <summary>
        /// Image display.
        /// </summary>
        private static PictureBox Display { get; set; } = null!;

        /// <summary>
        /// Slideshow timer.
        /// </summary>
        private static Timer Interval { get; set; } = null!;

        /// <summary>
        /// Interval, in milliseconds.
        /// </summary>
        private static int IntervalMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Slideshow image path.
        /// </summary>
        private static string ImagePath { get; set; } = null!;

        /// <summary>
        /// Current file index.
        /// </summary>
        private static int ImageIndex { get; set; } = -1;

        /// <summary>
        /// All found files.
        /// </summary>
        private static List<string> Images { get; set; } = new();

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">App arguments.</param>
        [STAThread]
        private static void Main(
            string[] args)
        {
            ApplicationConfiguration.Initialize();

            // Configure the app based on command-line arguments.
            if (!Configure(args))
            {
                ShowAppOptions();
                return;
            }

            // Hide cursor.
            Cursor.Hide();

            // Setup the main window.
            SetupWindow();

            // Setup the image control to display images with.
            SetupImageControl();

            // Setup the slideshow timer.
            SetupTimer();

            // Run the slideshow.
            Application.Run(Window);
        }

        #region Events

        /// <summary>
        /// Process key-presses.
        /// </summary>
        /// <param name="sender">Originating control.</param>
        /// <param name="arg">Keys pressed.</param>
        private static void OnKeyDown(
            object? sender, 
            KeyEventArgs arg)
        {
            switch (arg.KeyCode)
            {
                // Close the application.
                case Keys.Escape:
                    Application.Exit();
                    break;

                // Pause/resume the slideshow.
                case Keys.Space:
                    Interval.Enabled = !Interval.Enabled;
                    break;

                // Go to previous image.
                case Keys.Left:
                    GoToPreviousImage();
                    break;

                // Go to next image.
                case Keys.Right:
                    GoToNextImage();
                    break;
            }
        }

        /// <summary>
        /// Process timer interval.
        /// </summary>
        private static void OnTimerTick(
            object? sender, 
            EventArgs e)
        {
            ImageIndex++;

            if (ImageIndex == Images.Count)
            {
                ImageIndex = 0;
            }

            var path = Images[ImageIndex];

            try
            {
                var image = Image.FromFile(path);

                var width = image.Width;
                var height = image.Height;

                if (width > WindowWidth ||
                    height > WindowHeight)
                {
                    var rf1 = (double)WindowHeight / (double)height;
                    var rf2 = (double)WindowWidth / (double)width;
                    
                    double rf;

                    if (rf1 < rf2)
                    {
                        rf = (double)height / (double)WindowHeight;

                        height = WindowHeight;
                        width = (int)((double)width / rf);
                    }
                    else
                    {
                        rf = (double)width / (double)WindowWidth;

                        width = WindowWidth;
                        height = (int)((double)height / rf);
                    }
                }

                var top = (WindowHeight - height) /2;
                var left = (WindowWidth - width) /2;

                Display.Hide();

                Display.Left = left;
                Display.Top = top;
                Display.Width = width;
                Display.Height = height;

                Display.Image = image;
                Display.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// When the window is shown.
        /// </summary>
        private static void OnWindowShown(
            object? sender, 
            EventArgs e)
        {
            // Set window height and width.
            WindowHeight = Window.Height;
            WindowWidth = Window.Width;

            // Init the first image.
            OnTimerTick(null, new());
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Go to previous image.
        /// </summary>
        private static void GoToPreviousImage()
        {
            ImageIndex--;

            if (ImageIndex == -1)
            {
                ImageIndex = Images.Count - 1;
            }

            ImageIndex--;

            if (ImageIndex == -1)
            {
                ImageIndex = Images.Count - 1;
            }

            OnTimerTick(null, new());

            Interval.Enabled = false;
            Interval.Enabled = true;
        }

        /// <summary>
        /// Go to next image.
        /// </summary>
        private static void GoToNextImage()
        {
            OnTimerTick(null, new());

            Interval.Enabled = false;
            Interval.Enabled = true;
        }

        #endregion

        #region Setup

        /// <summary>
        /// Configure the app based on command-line arguments.
        /// </summary>
        /// <param name="args">App arguments.</param>
        /// <returns>Success.</returns>
        private static bool Configure(
            string[] args)
        {
            if (args == null ||
                args.Length == 0 ||
                args[0] == "-h")
            {
                return false;
            }

            // ImagePath.
            try
            {
                if (!Directory.Exists(args[0]))
                {
                    throw new Exception(
                        $"Specified folder does not exist: {args[0]}");
                }

                ImagePath = args[0];

                if (!FindAllImages(args.Any(n => n == "-s")))
                {
                    throw new Exception(
                        $"No images found in {ImagePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return true;
            }

            // Cycle for more params.
            try
            {
                var skip = false;

                for (var i = 1; i < args.Length; i++)
                {
                    if (skip)
                    {
                        skip = false;
                        continue;
                    }

                    switch (args[i])
                    {
                        // Randomize for each new image.
                        case "-r":
                            var rnd = new Random();
                            
                            Images = Images
                                .OrderBy(_ => rnd.Next())
                                .ToList();

                            break;

                        // Interval, in milliseconds.
                        case "-i":
                            if (i == args.Length - 1)
                            {
                                throw new Exception(
                                    "Argument -i must be followed by a number of milliseconds.");
                            }

                            if (!int.TryParse(args[i + 1], out var ms))
                            {
                                throw new Exception(
                                    $"Unable to parse argument {args[i + 1]} to a valid number.");
                            }

                            IntervalMilliseconds = ms;

                            skip = true;
                            break;

                        // Set screen to use, by index.
                        case "-n":
                            if (i == args.Length - 1)
                            {
                                throw new Exception(
                                    "Argument -n must be followed by an index.");
                            }

                            if (!int.TryParse(args[i + 1], out var index))
                            {
                                throw new Exception(
                                    $"Unable to parse argument {args[i + 1]} to a valid index.");
                            }

                            if (index < 0 ||
                                index >= Screen.AllScreens.Length)
                            {
                                throw new Exception(
                                    $"Screen index out of bounds.");
                            }

                            PrimaryScreen = Screen.AllScreens[index];

                            skip = true;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }

            // Done.
            return true;
        }

        /// <summary>
        /// Find all the images in the given path.
        /// </summary>
        /// <param name="includeSubfolders">Include subfolders.</param>
        /// <returns>Success.</returns>
        private static bool FindAllImages(
            bool includeSubfolders = false)
        {
            var exts = new[]
            {
                "jpg",
                "jpeg",
                "png",
                "gif"
            };

            var searchOption = includeSubfolders
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (var ext in exts)
            {
                try
                {
                    Images.AddRange(
                        Directory.GetFiles(
                            ImagePath,
                            $"*.{ext}",
                            searchOption));
                }
                catch
                {
                    //
                }
            }

            Images = Images
                .OrderBy(n => n)
                .ToList();

            return Images.Count > 0;
        }

        /// <summary>
        /// Setup the image control to display images with.
        /// </summary>
        private static void SetupImageControl()
        {
            Display = new()
            {
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            Window.Controls.Add(Display);
        }

        /// <summary>
        /// Setup the slideshow timer.
        /// </summary>
        private static void SetupTimer()
        {
            Interval = new Timer
            {
                Enabled = true,
                Interval = IntervalMilliseconds
            };

            Interval.Tick += OnTimerTick;
        }

        /// <summary>
        /// Setup the main window.
        /// </summary>
        private static void SetupWindow()
        {
            // Setup form.
            Window = new Form
            {
                BackColor = Color.Black,
                FormBorderStyle = FormBorderStyle.None,
                Left = PrimaryScreen.WorkingArea.Left,
                StartPosition = FormStartPosition.Manual,
                Top = PrimaryScreen.WorkingArea.Top,
                WindowState = FormWindowState.Maximized
            };

            // Attach events.
            Window.KeyDown += OnKeyDown;
            Window.Shown += OnWindowShown;
        }

        /// <summary>
        /// Show the app options to the appropriate output.
        /// </summary>
        private static void ShowAppOptions()
        {
            var list = new[]
            {
                $"{Application.ProductName} v{Application.ProductVersion}",
                string.Empty,
                "Usage:",
                " slideshow <path-to-images> [options]",
                string.Empty,
                "Options:",
                " -r",
                "  Randomize for each new image.",
                " -i <ms>",
                "  Interval, in milliseconds. Defaults to 5000 = 5 seconds.",
                " -s",
                "  Include subfolders.",
                " -n <index>",
                "  Set the index of the screen to use. Defaults to primary screen.",
                string.Empty,
                "Keys:",
                " ESC - Exit slideshow.",
                " Space - Pause/resume slideshow.",
                " Left - Go to previous image.",
                " Right - Go to next image."
            };

            var text = string.Join(Environment.NewLine, list);

            MessageBox.Show(
                text,
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion
    }
}
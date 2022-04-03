using Timer = System.Windows.Forms.Timer;

namespace Slideshow
{
    internal static class Program
    {
        #region Properties

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
        private static List<string> Images { get; } = new();

        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">App arguments.</param>
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // Configure the app based on command-line arguments.
            if (!Configure(args))
            {
                ShowAppOptions();
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
        private static void OnKeyDown(object? sender, KeyEventArgs arg)
        {
            switch (arg.KeyCode)
            {
                // Close the application.
                case Keys.Escape:
                    Application.Exit();
                    break;
            }
        }

        /// <summary>
        /// Process timer interval.
        /// </summary>
        private static void OnTimerTick(object? sender, EventArgs e)
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
        private static void OnWindowShown(object? sender, EventArgs e)
        {
            // Set window height and width.
            WindowHeight = Window.Height;
            WindowWidth = Window.Width;

            // Init the first image.
            OnTimerTick(null, new());
        }

        #endregion

        #region Setup

        /// <summary>
        /// Configure the app based on command-line arguments.
        /// </summary>
        /// <param name="args">App arguments.</param>
        /// <returns>Success.</returns>
        private static bool Configure(string[] args)
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
                    throw new Exception($"Specified folder does not exist: {args[0]}");
                }

                ImagePath = args[0];

                if (!FindAllImages())
                {
                    throw new Exception($"No images found in {ImagePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return true;
            }

            // Done.
            return true;
        }

        /// <summary>
        /// Find all the images in the given path.
        /// </summary>
        /// <returns>Success.</returns>
        private static bool FindAllImages()
        {
            var exts = new[]
            {
                "jpg",
                "jpeg",
                "png",
                "gif"
            };

            foreach (var ext in exts)
            {
                try
                {
                    Images.AddRange(
                        Directory.GetFiles(
                            ImagePath,
                            $"*.{ext}",
                            SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    //
                }
            }

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
                Interval = 5000
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
            var text =
                $"{Application.ProductName} v{Application.ProductVersion}{Environment.NewLine}" +
                $"{Environment.NewLine}" +
                "Usage: slideshow <path-to-images>";

            MessageBox.Show(
                text,
                "About",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion
    }
}
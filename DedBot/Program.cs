using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Tesseract;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;


namespace DedBot
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Bot bot = new Bot();
            Application.Run(new Form1());

        }
    }

    class Bot
    {
        TwitchClient client;

        public Bot()
        {
            var oauth = Environment.GetEnvironmentVariable("twitchOauth");
            ConnectionCredentials credentials = new ConnectionCredentials("Justly", oauth);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, "Justly");
            client.Connect();
            InitTimer();
        }

        private Timer isDeadtimer, hasDiedTimer;
        public void InitTimer()
        {
            isDeadtimer = new Timer();
            isDeadtimer.Tick += new EventHandler(timer1_Tick);
            isDeadtimer.Interval = 1000;
            isDeadtimer.Start();

            hasDiedTimer = new Timer();
            hasDiedTimer.Tick += new EventHandler(timer2_Tick);
            hasDiedTimer.Interval = 5000;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Byte[] bob;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(new Point(641, 480), new Point(641, 480),  new Size(500, 100));
                }

                using (var memoryStream = new MemoryStream())
                {
                    for (Int32 y = 0; y < bitmap.Height; y++) { 
                        for (Int32 x = 0; x < bitmap.Width; x++)
                        {
                            Color PixelColor = bitmap.GetPixel(x, y);
                            if (PixelColor.R > 30)
                            {
                                bitmap.SetPixel(x, y, Color.White);
                            }
                        }
                    }
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);

                    bob = memoryStream.ToArray();
                }
            }
             
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                using (var img = Pix.LoadFromMemory(bob))
                {
                    using (var page = engine.Process(img))
                    {
                        var text = page.GetText().ToLower();
                        if (text.Contains("you died") || text.Contains("youdied"))
                        {
                            isDeadtimer.Stop();
                            hasDiedTimer.Start();
                            client.SendMessage("Justly", "he ded");

                        }
                    }
                }
            }

            /* Find certain pixels in the "YOU DIED" message
            Point cursor = new Point();
            GetCursorPos(ref cursor);

            var YColour = GetColorAt(new Point(2701, 541));
            var OColour = GetColorAt(new Point(2777, 534));
            var DColour = GetColorAt(new Point(2936, 535));
            var leftColour = GetColorAt(new Point(2477, 535));
            var rightColour = GetColorAt(new Point(3124, 537));

            var YHue = YColour.GetHue();
            var OHue = OColour.GetHue();
            var DHue = DColour.GetHue();
            var leftBrightness = leftColour.GetBrightness();
            var rightBrightness = rightColour.GetBrightness();

            
            if ((YHue > 320 || YHue < 15) && (OHue > 320 || OHue < 15)  && (DHue > 320 || DHue < 15) && leftBrightness < 0.2 && rightBrightness < 0.2)
            {
                client.SendMessage("CaveManRoob", "he ded");
            }
            */

        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            hasDiedTimer.Stop();
            isDeadtimer.Start();

        }


        /*
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point lpPoint);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);


        Bitmap screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        public Color GetColorAt(Point location)
        {
            using (Graphics gdest = Graphics.FromImage(screenPixel))
            {
                using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hSrcDC = gsrc.GetHdc();
                    IntPtr hDC = gdest.GetHdc();
                    int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
                    gdest.ReleaseHdc();
                    gsrc.ReleaseHdc();
                }
            }

            return screenPixel.GetPixel(0, 0);
        }
        */
    }
}


using System;
using System.Drawing;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using Tesseract;

namespace DedBot
{

    public partial class Form1 : Form
    {
        Bot bot;
        public Form1()
        {
            bot = new Bot();
           // InitializeComponent();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            var thread = new Thread(bot.twitchChatClient.onDeath);
            thread.Start();
        }
    }

    public class Bot
    {
        string channel = "justly";
        public TwitchChatClient twitchChatClient;
        OpenAIClient openAIClient;
        TwitchPubSubClient pubSubClient;
        WerewolfController werewolfController;

        private System.Windows.Forms.Timer isDeadtimer, hasDiedTimer;

        public Bot()
        {
            openAIClient = new OpenAIClient();
            twitchChatClient = new TwitchChatClient(channel, openAIClient);
            werewolfController = new WerewolfController(channel);
            pubSubClient = new TwitchPubSubClient(channel, openAIClient, twitchChatClient, werewolfController);

            resetAI();
            Thread thread1 = new Thread(InitTimers);
            thread1.Start();
            
        }

        public void InitTimers()
        {
            isDeadtimer = new System.Windows.Forms.Timer();
            isDeadtimer.Tick += new EventHandler(IsDeadTimer_Tick);
            isDeadtimer.Interval = 500;
            isDeadtimer.Start();

            hasDiedTimer = new System.Windows.Forms.Timer();
            hasDiedTimer.Tick += new EventHandler(HasDiedTimer_Tick);
            hasDiedTimer.Interval = 5000;
            hasDiedTimer.Start();
        }

        private void HasDiedTimer_Tick(object sender, EventArgs e)
        {
            isDeadtimer.Start();
            hasDiedTimer.Stop();
        }

        private void IsDeadTimer_Tick(object sender, EventArgs e)
        {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Byte[] byteArray;
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // g.CopyFromScreen(new Point(641, 480), new Point(641, 480), new Size(500, 100));
                    g.CopyFromScreen(new Point(600, 200), new Point(600, 200), new Size(1000, 500));
                }

                using (var memoryStream = new MemoryStream())
                {
                    for (Int32 y = 0; y < bitmap.Height; y++)
                    {
                        for (Int32 x = 0; x < bitmap.Width; x++)
                        {
                            Color PixelColor = bitmap.GetPixel(x, y);
                            if (PixelColor.R > 35)
                            {
                                bitmap.SetPixel(x, y, Color.White);
                            }
                        }
                    }
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);

                    byteArray = memoryStream.ToArray();


                    using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                    {
                        using (var img = Pix.LoadFromMemory(byteArray))
                        {
                            using (var page = engine.Process(img))
                            {
                                var text = page.GetText().ToLower();

                                if (text.Contains("you") || text.Contains("died"))
                                {
                                    bitmap.Save("bill.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);

                                    var thread = new Thread(twitchChatClient.onDeath);
                                    thread.Start();
                                    hasDiedTimer.Start();
                                    isDeadtimer.Stop();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void resetAI()
        {
            openAIClient.Reset();

            twitchChatClient.prompt = File.ReadAllText("prompt.txt");
        }
    }
}

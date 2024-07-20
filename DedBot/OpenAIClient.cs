using System;
using OpenAI_API;
using OpenAI_API.Chat;
using System.Threading.Tasks;

namespace DedBot
{
    public class OpenAIClient
    {
        private OpenAIAPI api;
        private Conversation mongoConversation;
        private Conversation conversation;

        public OpenAIClient()
        {
            System.IO.File.WriteAllText("ai-image.png", "blank");
            var openAiKey = Environment.GetEnvironmentVariable("openAiKey");
            this.api = new OpenAIAPI(new APIAuthentication(openAiKey));
        }

        public async void Reset()
        {
            conversation = api.Chat.CreateConversation();
            mongoConversation = api.Chat.CreateConversation();
            var mongoPrompt = Environment.GetEnvironmentVariable("mongoPrompt");
            mongoConversation.AppendUserInput(mongoPrompt);
            await mongoConversation.GetResponseFromChatbotAsync();
        }

        public async Task<string> SendAndRecieveMessage(string message, bool isMongo)
        {
            
            try
            {
                var convo = isMongo ? mongoConversation : conversation;
                if (convo == null) { return "bot not initialised"; }
                convo.AppendUserInput(message);
                string response = await convo.GetResponseFromChatbotAsync();
                response = response.Replace('"', '\0');
                if (response.Length > 500)
                {
                    response = response.Substring(0, 500);
                }
                return response;
            }
            catch
            {
                return "Something went wrong, probably too many requests";
            }

        }

        public async void TTS(string user, string prompt)
        {
            OpenAI_API.Audio.TextToSpeechRequest bob = new OpenAI_API.Audio.TextToSpeechRequest() { Input = prompt, Voice = "alloy" };
            await this.api.TextToSpeech.SaveSpeechToFileAsync(prompt, "tts.mp3");
            string dir = System.IO.Directory.GetCurrentDirectory();
            WMPLib.WindowsMediaPlayer wplayer = new WMPLib.WindowsMediaPlayer();
            wplayer.URL = ($"{dir}\\tts");
            wplayer.controls.play();
        }

        public async Task DrawImage(string user, string prompt)
        {
            OpenAI_API.Images.ImageGenerationRequest request = new OpenAI_API.Images.ImageGenerationRequest { NumOfImages = 1, Prompt = prompt, Model = "dall-e-3", Quality = "hd" };
            var img = await this.api.ImageGenerations.CreateImageAsync(request);
            System.Net.WebClient client = new System.Net.WebClient();
            await client.DownloadFileTaskAsync(new Uri(img.ToString()), "downloading.png");
            string dir = System.IO.Directory.GetCurrentDirectory();
            System.IO.File.Copy("downloading.png", $"{dir}\\images\\{user}_{prompt}.png", true);
            System.IO.File.Move("downloading.png", "ai-image.png", true);
            System.Threading.Thread.Sleep(30000);
            System.IO.File.WriteAllText("ai-image.png", "blank");
        }
    }
}

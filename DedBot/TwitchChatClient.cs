using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace DedBot
{
    
    public class TwitchChatClient
    {
        private readonly string channel;

        private readonly OpenAIClient OpenAIClient;

        public string prompt;

        private readonly TwitchClient twitchClient;
        private bool currentlyCreatingimage = false;
        private readonly List<string> users;
        
        public TwitchChatClient(string channel, OpenAIClient openAIClient)
        {
            prompt = File.ReadAllText("prompt.txt");
            this.channel = channel;
            this.OpenAIClient = openAIClient;
            this.users = new List<string>();
            
            var oauth = Environment.GetEnvironmentVariable("altTwitchOauth");
            ConnectionCredentials credentials = new ConnectionCredentials("YouKnowWhatActually", oauth);
            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);

            this.twitchClient = new TwitchClient();
            twitchClient = new TwitchClient(customClient);
            twitchClient.Initialize(credentials, channel);
            twitchClient.OnMessageReceived += Client_OnMessageReceived;
            twitchClient.OnNewSubscriber += Client_OnNewSubscriber;
            twitchClient.OnReSubscriber += Client_OnReSubscriber;
            twitchClient.OnGiftedSubscription += Client_OnGiftedSubscription;
            twitchClient.OnWhisperReceived += Client_OnWhisperReceived;
            twitchClient.Connect();
        }

        public void SendMessage(string message)
        {
            twitchClient.SendMessage(channel, message);
        }

        public void OnDeath()
        {
            var now = DateTime.Now;
            var deathTimes = new List<string>(File.ReadAllLinesAsync("deaths.txt").Result)
            {
                now.ToString()
            };
            File.WriteAllLines("deaths.txt", deathTimes);
            var message = OpenAIClient.SendAndRecieveMessage(prompt, true).Result;
            if (message.Length > 500)
            {
                twitchClient.SendMessage(channel, "Response too long");
            }
            else
            {
                twitchClient.SendMessage(channel, message);
            }
        }

        public async void CreateImage(string user, string prompt)
        {
            if (currentlyCreatingimage)
                {
                    twitchClient.SendMessage(channel, "Already working on an image");
                } else
                {
                    currentlyCreatingimage = true;
                    twitchClient.SendMessage(channel, "Working on it...");
                    await this.OpenAIClient.DrawImage(user, prompt);
                    currentlyCreatingimage = false;
                }
        }

        public void CreateTTS(string user, string prompt)
        {

                twitchClient.SendMessage(channel, "Working on it...");
                this.OpenAIClient.TTS(user, prompt);
        }

        public void CreateTimeout()
        {
            if (users.Count == 0)
            {
                twitchClient.SendMessage(channel, $"There's no one in chat to time out.");

                return;
            }
            Random rnd = new Random();
            int index = rnd.Next(0, users.Count -1);
           // client.TimeoutUser(channel, users[index], new TimeSpan(0, 1, 0));
            twitchClient.SendMessage(channel, $"Timing out {users[index]} for 1 minute, unlucky.");
        }

        public void Client_OnWhisperReceived(object s, OnWhisperReceivedArgs e)
        {
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {

            if (e.ChatMessage.Message.ToLower().StartsWith("!dedbot")  && (e.ChatMessage.DisplayName.Equals("Justly") || e.ChatMessage.UserType == UserType.Moderator))
            {
                var message = e.ChatMessage.Message.Split();
                var command = message[1].ToLower();
                var now = DateTime.Now.ToShortDateString();
                var todayDeaths = File.ReadLines(@"deaths.txt").Where(l => l.Contains(now)).ToList();

                switch (command)
                {
                    case "hello":
                        twitchClient.SendMessage(channel, "hello @" + e.ChatMessage.DisplayName);
                        break;
                    case "deaths":
                        var lineCount = File.ReadLines(@"deaths.txt").ToList().Count();
                        twitchClient.SendMessage(channel, "Roobie has died " + lineCount + " times");
                        break;
                    case "today":
                        twitchClient.SendMessage(channel, "Roobie has died " + todayDeaths.Count() + " times today");
                        break;
                    case "adddeath":
                        OnDeath();
                        break;
                    case "average":
                        if (message.Count() == 2)
                        {
                            var average = MathHelper.getAverage(null);
                            twitchClient.SendMessage(channel, "average time between todays deaths: " + average.Hours + "h " + average.Minutes + "m " + average.Seconds + "s");
                        }
                        else if (message.Count() > 2)
                        {
                            var amount = int.Parse(message[2].ToLower());
                            var average = MathHelper.getAverage(amount);
                            twitchClient.SendMessage(channel, "average time between the last " + amount + " deaths: " + average.Hours + "h " + average.Minutes + "m " + average.Seconds + "s");
                        }
                        break;
                    case "prompt":
                        var test = e.ChatMessage.Message.Split('"');
                        var param = test[1];
                        prompt = param;
                        break;
                    case "currentprompt":
                        twitchClient.SendMessage(channel, "The current prompt is: " + prompt);
                        break;
                    case "reset":
                        OpenAIClient.Reset();
                        prompt = File.ReadAllText("prompt.txt");
                        break;
                    case "message":
                        var request = e.ChatMessage.Message.Split('"');
                        var response = OpenAIClient.SendAndRecieveMessage(request[1], false).Result;
                        twitchClient.SendMessage(channel, response);
                        break;


                }

            }
            if (e.ChatMessage.Message.ToLower().Contains("peeposcissors"))
            {
                twitchClient.SendMessage(channel, "peepoScissors");
            }
        }

        private async void Client_OnReSubscriber(object sender, OnReSubscriberArgs e)
        {
            var response = await OpenAIClient.SendAndRecieveMessage("generate a short personalised thank you message for a person named " + e.ReSubscriber.DisplayName + "signing off with " + channel, false);
            SendMessage(response);
        }

        private async void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            var response = await OpenAIClient.SendAndRecieveMessage("generate a short personalised thank you message for a person named " + e.Subscriber.DisplayName + "signing off with " + channel, false);
            SendMessage(response);
        }

        private async void Client_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            var response = await OpenAIClient.SendAndRecieveMessage("generate a short personalised thank you message for a person named " + e.GiftedSubscription.DisplayName + "signing off with " + channel, false);
            SendMessage(response);
        }

    }
}

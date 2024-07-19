using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using System.Threading;

namespace DedBot
{
     public enum Role
    {
        Villager, Werewolf, Doctor, Seer
    }
    public class WerewolfPlayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Role Role { get; set; }
        public int Votes { get; set; }
        public bool hasVoted { get; set; }


        public WerewolfPlayer(string user) { this.Name = user; }
        
    }

    public class TwitchChatClient
    {
        TwitchClient client;
        OpenAIClient OpenAIClient;
        string channel;
        public string prompt;
        public bool currentlyCreatingimage = false;
        public List<string> users;
        Random rnd;

        // werewolf
        public bool lookingForWerewolfplayers = false, lookingForPins = false, waitingForPrey = false, waitingForDoctor = false, waitingForSeer = false, waitingForVotes = false;
        public List<WerewolfPlayer> werewolfplayers;
        public string playerToKill, playerToSave, playerToCheck, seerSuccessPin;
        public TwitchChatClient(string channel, OpenAIClient openAIClient)
        {
            rnd = new Random();
            werewolfplayers = new List<WerewolfPlayer>();   
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
            client = new TwitchClient(customClient);
            client.Initialize(credentials, channel);
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnReSubscriber += Client_OnReSubscriber;
            client.OnGiftedSubscription += Client_OnGiftedSubscription;
            client.OnWhisperReceived += Client_OnWhisperReceived;
            client.Connect();
        }

        public void SendMessage(string message)
        {
            client.SendMessage(channel, message);
        }

        public void onDeath()
        {
            var now = DateTime.Now;
            var deathTimes = new List<string>(File.ReadAllLinesAsync("deaths.txt").Result);
            deathTimes.Add(now.ToString());
            File.WriteAllLines("deaths.txt", deathTimes);
            var message = OpenAIClient.SendAndRecieveMessage(prompt, true).Result;
            if (message.Length > 500)
            {
                client.SendMessage(channel, "Response too long");
            }
            else
            {
                client.SendMessage(channel, message);
            }
        }

        public async void createImage(string user, string prompt)
        {
            if (currentlyCreatingimage)
                {
                    client.SendMessage(channel, "Already working on an image");
                } else
                {
                    currentlyCreatingimage = true;
                    client.SendMessage(channel, "Working on it...");
                    await this.OpenAIClient.DrawImage(user, prompt);
                    currentlyCreatingimage = false;
                }
        }

        public async void createTTS(string user, string prompt)
        {

                client.SendMessage(channel, "Working on it...");
                this.OpenAIClient.TTS(user, prompt);
        }

        public async void createTimeout()
        {
            if (users.Count == 0)
            {
                client.SendMessage(channel, $"There's no one in chat to time out.");

                return;
            }
            Random rnd = new Random();
            int index = rnd.Next(0, users.Count -1);
           // client.TimeoutUser(channel, users[index], new TimeSpan(0, 1, 0));
            client.SendMessage(channel, $"Timing out {users[index]} for 1 minute, unlucky.");
        }

        public void startWerewolf(string user)
        {
            SendMessage(user + " has started a game of werewolf, type !join to play.");
            this.lookingForWerewolfplayers = true;
            werewolfplayers.Add(new WerewolfPlayer(user.ToLower()));
            Thread.Sleep(60000);
            this.lookingForWerewolfplayers = false;

            SendMessage("Starting a game of werewolf. The players are " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            int werewolfIndex = rnd.Next(werewolfplayers.Count - 1);
            int docterIndex = rnd.Next(werewolfplayers.Count - 1);
            int seerIndex = rnd.Next(werewolfplayers.Count - 1);

            while (docterIndex == werewolfIndex)
            {
                docterIndex = rnd.Next(werewolfplayers.Count - 1);
            }

            while (seerIndex == werewolfIndex || seerIndex == docterIndex)
            {
                seerIndex = rnd.Next(werewolfplayers.Count - 1);
            }

            werewolfplayers[werewolfIndex].Role = Role.Werewolf;
            werewolfplayers[docterIndex].Role = Role.Doctor;
            werewolfplayers[seerIndex].Role = Role.Seer;

            SendMessage("please whisper a random 4 digit code to the bot by typing \"/w youknowwhatactually [phrase]\", the bot will then write a message in the chat telling you your code and your role");
            this.lookingForPins = true;
            Thread.Sleep(60000);
            this.lookingForPins = false;
            SendMessage("The game is now starting");

            var werewolves = werewolfplayers.FindAll(x => x.Role == Role.Werewolf);
            var villagers = werewolfplayers.FindAll(x => x.Role != Role.Werewolf);

            while (villagers.Count > werewolves.Count && werewolves.Count > 0) {
                // night time
                SendMessage("Werewolf please tell me who you want to kill and doctor who you want to save by typing /w youknowwhatactually [player]. The current players are: " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
                waitingForPrey = true;
                waitingForDoctor = true;
                Thread.Sleep(30000);
                waitingForPrey = false;
                waitingForDoctor = false;

                SendMessage("Seer tell me who you want to check by typing /w youknowwhatactually [player] [4 digit success pin]. If you correctly check the werewolf, I will send the success pin in chat, otherwise it will be a random pin. The current players are: " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));

                waitingForSeer = true;
                Thread.Sleep(30000);
                waitingForSeer = false;

                // day time
                if (playerToKill != null)
                {

                    if (playerToKill == playerToSave)
                    {
                        SendMessage("The doctor saved a villager!");
                        return;
                    }
                    var deadPlayer = werewolfplayers.Find(x => x.Name.ToLower() == playerToKill);
                    var successful = werewolfplayers.Remove(deadPlayer);
                    if (successful)
                    {
                        SendMessage(playerToKill + " has been killed!");
                    }
                    else
                    {
                        SendMessage("Something went wrong");
                    }
                    playerToKill = null;
                }
                else
                {
                    SendMessage("No one has been killed tonight");
                }
                Thread.Sleep(3000);

                var checkedPlayer = werewolfplayers.Find(x => x.Name == playerToCheck);
                switch(checkedPlayer.Role)
                {
                    case Role.Werewolf:
                        SendMessage("Seer, " + seerSuccessPin);
                        break;
                    default:
                        SendMessage("Seer, " + rnd.Next(9999));
                        break;

                }

                werewolves = werewolfplayers.FindAll(x => x.Role == Role.Werewolf);
                villagers = werewolfplayers.FindAll(x => x.Role != Role.Werewolf);
                if (villagers.Count <= werewolves.Count)
                {
                    break;
                }

                //vote to kill

                Thread.Sleep(5000);
                SendMessage("You now have 3 minutes to vote someone off. To vote someone off type /w youknowwhatactually [player]. if I receive more than 3 votes for a player they will be hanged.  The current players are:" + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
                waitingForVotes = true;
                Thread.Sleep(60000);
                SendMessage("You now have 2 minutes to vote someone off");
                Thread.Sleep(60000);
                SendMessage("You now have 1 minute to vote someone off");
                Thread.Sleep(60000);

                waitingForVotes = false;

                var maxvotes = 0;
                foreach (var player in werewolfplayers)
                {
                    player.hasVoted = false;
                    if (player.Votes > maxvotes)
                    {
                        maxvotes = player.Votes;
                    }
                }

                if (maxvotes > werewolfplayers.Count / 3)
                {
                    var player = werewolfplayers.Find(x => x.Votes == maxvotes);
                    SendMessage(player.Name + " has been voted out.");
                    werewolfplayers.Remove(player);
                }
                else
                {
                    SendMessage("No one received enough votes to be voted off");
                }

                foreach (var player in werewolfplayers)
                {
                    player.Votes = 0;
                }

                werewolves = werewolfplayers.FindAll(x => x.Role == Role.Werewolf);
                villagers = werewolfplayers.FindAll(x => x.Role != Role.Werewolf);
                if (villagers.Count <= werewolves.Count)
                {
                    break;
                }
                Thread.Sleep(5000);
            }

            if (werewolves.Count == 0) {
                SendMessage("Villagers win");
            }
            else if (villagers.Count <= werewolves.Count) {
                SendMessage("Werewolves win! The werewolves were " + string.Join(", ", werewolves.Select(x => x.Name).ToList()));
            }
        }

        private async void Client_OnWhisperReceived(object s, OnWhisperReceivedArgs e)
        {
            WerewolfPlayer sender = werewolfplayers.Find(x => x.Name == e.WhisperMessage.Username);
            List<string> playerNames = werewolfplayers.Select(x => x.Name.ToLower()).ToList();
            
            if (sender == null)
            {
                SendMessage(e.WhisperMessage.Username + " you are not in the game");
                return;
            }

            // voting someone off
            if (waitingForVotes && sender != null)
            {
                if (!sender.hasVoted)
                {
                    WerewolfPlayer target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                    if (target == null)
                    {
                        SendMessage(sender.Id + ": A player with that name does not exist, try again");
                    }
                    else
                    {
                        target.Votes++;
                        sender.hasVoted = true;
                    }
                }
                else
                {
                    SendMessage(sender.Id + " you have already voted, stop cheating");

                }


            }
            // werewolf killing someone
            else if (sender.Role == Role.Werewolf && waitingForPrey) {
                // if user exists

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                if (target == null)
                {
                    SendMessage(sender.Id + "A player with that name does not exist, try again");

                } else if (target.Role == Role.Werewolf) {
                    SendMessage(sender.Id + "That player is the werewolf, try again");
                }
                else
                {
                    SendMessage("the werewolf has chosen its prey");
                    waitingForPrey = false;
                    playerToKill = e.WhisperMessage.Message.ToLower();
                }
                
            }
            // doctor saving someone
            else if (sender.Role == Role.Doctor && waitingForDoctor)
            {
                // if user exists

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                if (target == null)
                {
                    SendMessage(sender.Id + "A player with that name does not exist, try again");

                }
                else
                {
                    SendMessage("the doctor has chosen their patient");
                    waitingForDoctor = false;
                    playerToSave = e.WhisperMessage.Message.ToLower();
                }

            }
            // seer checking someone
            else if (sender.Role == Role.Seer && waitingForSeer)
            {
                // if user exists
                var pin = e.WhisperMessage.Message.Split(' ');
                seerSuccessPin = pin[1];

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                if (target == null)
                {
                    SendMessage(sender.Id + "A player with that name does not exist, try again");

                }
                else
                {
                    waitingForSeer = false;
                    playerToCheck = e.WhisperMessage.Message.ToLower();
                }

            }
            // getting roles
            else if (playerNames.Contains(e.WhisperMessage.Username) && lookingForPins){
                int result;

                WerewolfPlayer existing = werewolfplayers.Find(x => x.Id == e.WhisperMessage.Message.ToLower());

                if (existing != null) {
                    SendMessage(e.WhisperMessage.Message + " Pin already in use");
                    return;
                }
 

                var valid = Int32.TryParse(e.WhisperMessage.Message, out result);
                if (e.WhisperMessage.Message.Length != 4 || !valid)
                {
                    SendMessage("Pin must be 4 digits");
                    return;
                }
                WerewolfPlayer user = werewolfplayers.Find(x => x.Name == e.WhisperMessage.Username.ToLower());
                user.Id = e.WhisperMessage.Message;
                SendMessage(e.WhisperMessage.Message + " " + user.Role);
            }
        }

        private async void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {

            if (!users.Contains(e.ChatMessage.Username))
            {
                users.Add(e.ChatMessage.Username);
            }
            if (lookingForWerewolfplayers && e.ChatMessage.Message.ToLower().StartsWith("!join"))
            {
              //  if (!werewolfplayers.Select(x => x.Name).Contains(e.ChatMessage.Username.ToLower()))
              //  {
                    werewolfplayers.Add(new WerewolfPlayer(e.ChatMessage.Username.ToLower()));
              //  } else
              //  {
                    SendMessage(e.ChatMessage.Username + " you are already in the game.");

              //  }
            }


            if (e.ChatMessage.Message.ToLower().StartsWith("!dedbot")  && (e.ChatMessage.DisplayName.Equals("Justly") || e.ChatMessage.UserType == UserType.Moderator))
            {
                var message = e.ChatMessage.Message.Split();
                var command = message[1].ToLower();
                var now = DateTime.Now.ToShortDateString();
                var todayDeaths = File.ReadLines(@"deaths.txt").Where(l => l.Contains(now)).ToList();

                switch (command)
                {
                    case "hello":
                        client.SendMessage(channel, "hello @" + e.ChatMessage.DisplayName);
                        break;
                    case "deaths":
                        var lineCount = File.ReadLines(@"deaths.txt").ToList().Count();
                        client.SendMessage(channel, "Roobie has died " + lineCount + " times");
                        break;
                    case "today":
                        client.SendMessage(channel, "Roobie has died " + todayDeaths.Count() + " times today");
                        break;
                    case "adddeath":
                        onDeath();
                        break;
                    case "average":
                        if (message.Count() == 2)
                        {
                            var average = MathHelper.getAverage(null);
                            client.SendMessage(channel, "average time between todays deaths: " + average.Hours + "h " + average.Minutes + "m " + average.Seconds + "s");
                        }
                        else if (message.Count() > 2)
                        {
                            var amount = int.Parse(message[2].ToLower());
                            var average = MathHelper.getAverage(amount);
                            client.SendMessage(channel, "average time between the last " + amount + " deaths: " + average.Hours + "h " + average.Minutes + "m " + average.Seconds + "s");
                        }
                        break;
                    case "prompt":
                        var test = e.ChatMessage.Message.Split('"');
                        var param = test[1];
                        prompt = param;
                        break;
                    case "currentprompt":
                        client.SendMessage(channel, "The current prompt is: " + prompt);
                        break;
                    case "reset":
                        OpenAIClient.Reset();
                        prompt = File.ReadAllText("prompt.txt");
                        break;
                    case "message":
                        var request = e.ChatMessage.Message.Split('"');
                        var response = OpenAIClient.SendAndRecieveMessage(request[1], false).Result;
                        client.SendMessage(channel, response);
                        break;


                }

            }
            if (e.ChatMessage.Message.ToLower().Contains("peeposcissors"))
            {
                client.SendMessage(channel, "peepoScissors");
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

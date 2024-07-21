using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

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
        public bool HasVoted { get; set; }

        public WerewolfPlayer(string user) { this.Name = user; }
        
    }

    public class WerewolfController
    {
        readonly string channel;
        
        public bool gameInProgress = false;

        private readonly TwitchClient twitchClient;
        private readonly Random rnd;
        private bool lookingForWerewolfplayers = false, lookingForPins = false, waitingForPrey = false, waitingForDoctor = false, waitingForSeer = false, waitingForVotes = false;
        private readonly List<WerewolfPlayer> werewolfplayers;
        private string playerToKill, playerToSave;

        public WerewolfController(string channel)
        {
            this.channel = channel;
            rnd = new Random();
            werewolfplayers = new List<WerewolfPlayer>();

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
            twitchClient.OnMessageReceived += OnMessageReceived;
            twitchClient.OnWhisperReceived += OnWhisperReceived;
            twitchClient.Connect();
        }

        public async void StartWerewolf(string user)
        {
            gameInProgress = true;
            twitchClient.SendMessage(channel, user + " has started a game of werewolf, type !join to play.");

            bool enoughPlayers = GetPlayers(user);
            if (!enoughPlayers)
            {
                twitchClient.SendMessage(channel, "Not enough players have entered the game.");
                return;
            }

            CreateRoles();
            await CreatePins();

            twitchClient.SendMessage(channel, "The game is now starting");
            Thread.Sleep(3000);

            while (!GameIsOver())
            {
                // night time
                GetWerewolfTarget();
                GetDoctorTarget();
                GetSeerTarget();

                // day time
                KillVictim();

                if (GameIsOver())
                {
                    break;
                }

                WaitForVotes();
                CalculateVotes();

                if (GameIsOver())
                {
                    break;
                }

                Thread.Sleep(5000);
            }

            gameInProgress = false;
        }

        private bool GetPlayers(string user)
        {
            this.lookingForWerewolfplayers = true;
            werewolfplayers.Add(new WerewolfPlayer(user.ToLower()));
            Thread.Sleep(60000);
            this.lookingForWerewolfplayers = false;
            twitchClient.SendMessage(channel, "Starting a game of werewolf. The players are " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            return werewolfplayers.Count > 4;
        }

        private void CreateRoles()
        {
            // 1-5, 1 werewolf
            // 6 people, 2 werewolves
            // every 4, add another werewolf

            List<int> werewolfIndexes = new List<int>();
            int werewolfCount = werewolfplayers.Count / 5;

            while (werewolfplayers.FindAll(x => x.Role == Role.Werewolf).Count < werewolfCount)
            {
                int index = rnd.Next(werewolfplayers.Count - 1);
                werewolfplayers[index].Role = Role.Werewolf;
                werewolfIndexes.Add(index);
            }

            int doctorIndex = rnd.Next(werewolfplayers.Count - 1);
            int seerIndex = rnd.Next(werewolfplayers.Count - 1);

            while (werewolfIndexes.Contains(doctorIndex))
            {
                doctorIndex = rnd.Next(werewolfplayers.Count - 1);
            }

            while (werewolfIndexes.Contains(seerIndex) || seerIndex == doctorIndex)
            {
                seerIndex = rnd.Next(werewolfplayers.Count - 1);
            }

            werewolfplayers[doctorIndex].Role = Role.Doctor;
            werewolfplayers[seerIndex].Role = Role.Seer;

        }

        private async Task CreatePins()
        {
            twitchClient.SendMessage(channel, "please whisper a random 4 digit code to the bot by typing \"/w youknowwhatactually [phrase]\", the bot will then write a message in the chat telling you your code and your role");
            lookingForPins = true;
            var pins = werewolfplayers.Select(x =>x.Id).ToList();
            while(pins.Contains(null))
            {
                await Task.Delay(500);
                pins = werewolfplayers.Select(x => x.Id).ToList();
            }
            lookingForPins = false;
        }

        private void GetWerewolfTarget()
        {
            twitchClient.SendMessage(channel, "Werewolf please tell me who you want to kill by typing /w youknowwhatactually [player]. The current players are: " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            waitingForPrey = true;
            Stopwatch timer = new Stopwatch();

            timer.Start();
            while (waitingForPrey)
            {
                if (timer.Elapsed.TotalSeconds > 30)
                {
                    waitingForPrey = false;
                }
            }
            timer.Stop();
        }

        private void GetDoctorTarget()
        {
            if (werewolfplayers.Select(x => x.Role == Role.Doctor) == null)
            {
                return;
            }
            twitchClient.SendMessage(channel, "Doctor who you want to save by typing /w youknowwhatactually [player]. The current players are: " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            waitingForDoctor = true;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (waitingForDoctor)
            {
                if (timer.Elapsed.TotalSeconds < 30)
                {
                    waitingForDoctor = false;
                }
            }
            timer.Stop();
        }

        private void GetSeerTarget()
        {
            if (werewolfplayers.Select(x => x.Role == Role.Seer) == null)
            {
                return;
            }
            twitchClient.SendMessage(channel, "Seer tell me who you want to check by typing /w youknowwhatactually [player] [2 digit success pin]. If you correctly check the werewolf, I will send the success pin in chat, otherwise it will be a random pin. The current players are: " + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            waitingForSeer = true;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (waitingForSeer)
            {
                if (timer.Elapsed.TotalSeconds < 30)
                {
                    waitingForSeer = false;
                }
            }
            timer.Stop();
        }

        private void KillVictim()
        {
            if (playerToKill != null)
            {
                if (playerToKill == playerToSave)
                {
                    twitchClient.SendMessage(channel, "The doctor saved a villager!");
                    return;
                }
                var deadPlayer = werewolfplayers.Find(x => x.Name.ToLower() == playerToKill);
                var successful = werewolfplayers.Remove(deadPlayer);
                if (successful)
                {
                    twitchClient.SendMessage(channel, playerToKill + " has been killed!");
                }
                else
                {
                    twitchClient.SendMessage(channel, "Something went wrong");
                }
                playerToKill = null;
            }
            else
            {
                twitchClient.SendMessage(channel, "No one has been killed tonight");
            }
            Thread.Sleep(3000);
        }

        private bool GameIsOver()
        {
            var werewolves = werewolfplayers.FindAll(x => x.Role == Role.Werewolf);
            var villagers = werewolfplayers.FindAll(x => x.Role != Role.Werewolf);
            if (villagers.Count <= werewolves.Count)
            {
                twitchClient.SendMessage(channel, "Werewolves win! The werewolves were " + string.Join(", ", werewolves.Select(x => x.Name).ToList()));
                return true;
            }
            else if (werewolves.Count == 0)
            {
                twitchClient.SendMessage(channel, "Villagers win");
                return true;
            }
            return false;
        }

        private void WaitForVotes()
        {
            Thread.Sleep(5000);
            twitchClient.SendMessage(channel, "You now have 3 minutes to vote someone off. To vote someone off type /w youknowwhatactually [player]. if I receive more than 3 votes for a player they will be hanged.  The current players are:" + string.Join(", ", werewolfplayers.Select(x => x.Name).ToList()));
            waitingForVotes = true;
            Thread.Sleep(60000);
            twitchClient.SendMessage(channel, "You now have 2 minutes to vote someone off");
            Thread.Sleep(60000);
            twitchClient.SendMessage(channel, "You now have 1 minute to vote someone off");
            Thread.Sleep(60000);
            waitingForVotes = false;
        }

        private void CalculateVotes()
        {
            var maxvotes = 0;
            foreach (var player in werewolfplayers)
            {
                player.HasVoted = false;
                if (player.Votes > maxvotes)
                {
                    maxvotes = player.Votes;
                }
            }

            if (maxvotes > werewolfplayers.Count / 3)
            {
                var player = werewolfplayers.Find(x => x.Votes == maxvotes);
                twitchClient.SendMessage(channel, player.Name + " has been voted out.");
                werewolfplayers.Remove(player);
            }
            else
            {
                twitchClient.SendMessage(channel, "No one received enough votes to be voted off");
            }

            foreach (var player in werewolfplayers)
            {
                player.Votes = 0;
            }
        }

        public void OnWhisperReceived(object s, OnWhisperReceivedArgs e)
        {
            WerewolfPlayer sender = werewolfplayers.Find(x => x.Name == e.WhisperMessage.Username);
            List<string> playerNames = werewolfplayers.Select(x => x.Name.ToLower()).ToList();
            
            if (sender == null)
            {
                twitchClient.SendMessage(channel, e.WhisperMessage.Username + " you are not in the game");
                return;
            }

            // voting someone off
            if (waitingForVotes && sender != null)
            {
                if (!sender.HasVoted)
                {
                    WerewolfPlayer target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                    if (target == null)
                    {
                        twitchClient.SendMessage(channel, sender.Id + ": A player with that name does not exist, try again");
                    }
                    else
                    {
                        target.Votes++;
                        sender.HasVoted = true;
                    }
                }
                else
                {
                    twitchClient.SendMessage(channel, sender.Id + " you have already voted, stop cheating");

                }


            }
            // werewolf killing someone
            else if (sender.Role == Role.Werewolf && waitingForPrey) {
                // if user exists

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                if (target == null)
                {
                    twitchClient.SendMessage(channel, sender.Id + "A player with that name does not exist, try again");

                } else if (target.Role == Role.Werewolf) {
                    twitchClient.SendMessage(channel, sender.Id + "That player is the werewolf, try again");
                }
                else
                {
                    twitchClient.SendMessage(channel, "the werewolf has chosen its prey");
                    waitingForPrey = false;
                    playerToKill = e.WhisperMessage.Message.ToLower();
                    Thread.Sleep(3000);
                }

            }
            // doctor saving someone
            else if (sender.Role == Role.Doctor && waitingForDoctor)
            {
                // if user exists

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));

                if (target == null)
                {
                    twitchClient.SendMessage(channel, sender.Id + "A player with that name does not exist, try again");

                }
                else
                {
                    twitchClient.SendMessage(channel, "the doctor has chosen their patient");
                    waitingForDoctor = false;
                    playerToSave = e.WhisperMessage.Message.ToLower();
                    Thread.Sleep(3000);
                }

            }
            // seer checking someone
            else if (sender.Role == Role.Seer && waitingForSeer)
            {
                // if user exists
                var splitMessage = e.WhisperMessage.Message.Split(' ');

                if (splitMessage.Length != 2 ) {
                    twitchClient.SendMessage(channel, "Seer, your message must be in the format of [player] [2 digit pin]");
                    return;
                }

                string checkedPlayerString = splitMessage[0].ToLower();
                Int32.TryParse(splitMessage[1], out int pin);
                WerewolfPlayer checkedPlayer = werewolfplayers.Find(x => x.Name.ToLower() == checkedPlayerString);

                if (pin > 99 || pin < 0) {
                    twitchClient.SendMessage(channel, "Seer, your message must be in the format of [player] [4 digit pin]");
                    return;
                }

                var target = werewolfplayers.Find(x => e.WhisperMessage.Message.ToLower().Contains(x.Name.ToLower()));
                if (target == null)
                {
                    twitchClient.SendMessage(channel, sender.Id + "A player with that name does not exist, try again");
                    return;
                }

                if (checkedPlayer == null) { return; }

                switch (checkedPlayer.Role)
                {
                    case Role.Werewolf:
                        twitchClient.SendMessage(channel, "Seer, " + pin);
                        break;
                    default:
                        twitchClient.SendMessage(channel, "Seer, " + rnd.Next(99));
                        break;

                }
                waitingForSeer = false;
                Thread.Sleep(3000);
            }
            // getting roles
            else if (playerNames.Contains(e.WhisperMessage.Username) && lookingForPins){

                WerewolfPlayer existing = werewolfplayers.Find(x => x.Id == e.WhisperMessage.Message.ToLower());

                if (existing != null) {
                    twitchClient.SendMessage(channel, e.WhisperMessage.Message + " Pin already in use");
                    return;
                }

                var valid = Int32.TryParse(e.WhisperMessage.Message, out int result);
                if (e.WhisperMessage.Message.Length != 4 || !valid)
                {
                    twitchClient.SendMessage(channel, "Pin must be 4 digits");
                    return;
                }
                WerewolfPlayer user = werewolfplayers.Find(x => x.Name == e.WhisperMessage.Username.ToLower());
                user.Id = e.WhisperMessage.Message;
                twitchClient.SendMessage(channel, e.WhisperMessage.Message + " " + user.Role);
            }
        }

        public void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.ToLower().StartsWith("!startgame")) {
                StartWerewolf(e.ChatMessage.Username.ToLower());
            }

            if (lookingForWerewolfplayers && e.ChatMessage.Message.ToLower().StartsWith("!join"))
            {
                if (!werewolfplayers.Select(x => x.Name).Contains(e.ChatMessage.Username.ToLower()))
                {
                    werewolfplayers.Add(new WerewolfPlayer(e.ChatMessage.Username.ToLower()));
                } else
                {
                    twitchClient.SendMessage(channel, e.ChatMessage.Username + " you are already in the game.");
                }
            }
        }
    }
}

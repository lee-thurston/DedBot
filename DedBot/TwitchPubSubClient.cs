﻿using System;
using System.Threading;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

namespace DedBot
{
    /**
     * If you come back to this at some point and it's not working, it's probably because of auth issues. You'll need to refresh the token by going here:
     * https://twitchtokengenerator.com/ and but the refresh token from an email you got (just search for the url in outlook) and put it in the box
     */

    class TwitchPubSubClient
    {
        private readonly string channel;
        private readonly TwitchChatClient twitchChatClient;
        private readonly TwitchPubSub pubSubClient;
        private readonly OpenAIClient openAIClient;
        private readonly WerewolfController werewolfController;

        public TwitchPubSubClient(string channel, OpenAIClient openAIClient, TwitchChatClient twitchChatClient, WerewolfController werewolfController)
        {
            this.channel = channel;
            this.openAIClient = openAIClient;
            this.twitchChatClient = twitchChatClient;
            this.werewolfController = werewolfController;

            this.pubSubClient = new TwitchPubSub();
            this.pubSubClient.OnListenResponse += OnListenResponse;
            this.pubSubClient.OnPubSubServiceConnected += OnPubSubServiceConnected;
            this.pubSubClient.OnChannelPointsRewardRedeemed += OnChannelPoints;
            this.pubSubClient.ListenToFollows("26045144");
            this.pubSubClient.ListenToChannelPoints("26045144");
            this.pubSubClient.OnFollow += OnFollow;
            this.pubSubClient.Connect();
        }

        private void OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
                throw new Exception($"Failed to listen! Response: {e.Response}");
        }

        private async void OnFollow(object sender, OnFollowArgs e)
        {
            var response = await openAIClient.SendAndRecieveMessage("generate a short personalised thank you message for a person named " + e.DisplayName + "signing off with " + channel, false);
            twitchChatClient.SendMessage(response);
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e)
        {
            var oauth = Environment.GetEnvironmentVariable("topicAuth");
            pubSubClient.SendTopics(oauth);
        }
        private void OnChannelPoints(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            string user = e.RewardRedeemed.Redemption.User.DisplayName;
            switch (e.RewardRedeemed.Redemption.Reward.Title)
            {
                case "Talk to the bot":
                    new Thread(async () => {
                        var response = await this.openAIClient.SendAndRecieveMessage(e.RewardRedeemed.Redemption.UserInput, false);
                        this.twitchChatClient.SendMessage("@" + user + " " + response);
                    }).Start();
                    break;
                        
                case "Talk to Mongo Tom":
                    new Thread(async () => {
                        var mongoResponse = await this.openAIClient.SendAndRecieveMessage(e.RewardRedeemed.Redemption.UserInput, true);
                        this.twitchChatClient.SendMessage("@" + user + " " + mongoResponse);
                    }).Start();
                    break;
                case "Create an image":
                    new Thread(() => {
                        this.twitchChatClient.CreateImage(e.RewardRedeemed.Redemption.User.DisplayName, e.RewardRedeemed.Redemption.UserInput);
                    }).Start();
                    break;
                case "Create TTS":
                    new Thread(() => {
                        this.twitchChatClient.CreateTTS(e.RewardRedeemed.Redemption.User.DisplayName, e.RewardRedeemed.Redemption.UserInput);
                    }).Start();
                    break;
                case "Give someone a timeout":
                    this.twitchChatClient.CreateTimeout();
                    break;
                case "Let's play werewolf":
                    if (this.werewolfController.gameInProgress)
                    {
                        this.twitchChatClient.SendMessage("A game is currently is progress");
                    }
                    else
                    {
                        new Thread(() => {
                            this.werewolfController.StartWerewolf(user);
                        }).Start();
                    }
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
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
        string channel;
        TwitchChatClient twitchChatClient;
        TwitchPubSub pubSubClient;
        OpenAIClient openAIClient;

        public TwitchPubSubClient(string channel, OpenAIClient openAIClient, TwitchChatClient twitchChatClient)
        {
            this.channel = channel;
            this.openAIClient = openAIClient;
            this.twitchChatClient = twitchChatClient;
            this.pubSubClient = new TwitchPubSub();
            this.pubSubClient.OnListenResponse += onListenResponse;
            this.pubSubClient.OnPubSubServiceConnected += onPubSubServiceConnected;
            this.pubSubClient.OnChannelPointsRewardRedeemed += onChannelPoints;
            this.pubSubClient.ListenToFollows("26045144");
            this.pubSubClient.ListenToChannelPoints("26045144");
            this.pubSubClient.OnFollow += OnFollow;
            this.pubSubClient.Connect();
        }

        private void onListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
                throw new Exception($"Failed to listen! Response: {e.Response}");
        }

        private async void OnFollow(object sender, OnFollowArgs e)
        {
            var response = await openAIClient.SendAndRecieveMessage("generate a short personalised thank you message for a person named " + e.DisplayName + "signing off with " + channel, false);
            twitchChatClient.SendMessage(response);
        }

        private void onPubSubServiceConnected(object sender, EventArgs e)
        {
            var oauth = Environment.GetEnvironmentVariable("topicAuth");
            pubSubClient.SendTopics(oauth);
        }
        private async void onChannelPoints(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            string user = e.RewardRedeemed.Redemption.User.DisplayName;
            switch (e.RewardRedeemed.Redemption.Reward.Title)
            {
                case "Talk to the bot":
                    var response = await this.openAIClient.SendAndRecieveMessage(e.RewardRedeemed.Redemption.UserInput, false);
                    this.twitchChatClient.SendMessage("@" + user + " " + response);
                    break;
                        
                case "Talk to Mongo Tom":
                    var mongoResponse = await this.openAIClient.SendAndRecieveMessage(e.RewardRedeemed.Redemption.UserInput, true);
                    this.twitchChatClient.SendMessage("@" + user + " " + mongoResponse);
                    break;
                case "Create an image":
                    this.twitchChatClient.createImage(e.RewardRedeemed.Redemption.User.DisplayName, e.RewardRedeemed.Redemption.UserInput);
                    break;
                case "Create TTS":
                    this.twitchChatClient.createTTS(e.RewardRedeemed.Redemption.User.DisplayName, e.RewardRedeemed.Redemption.UserInput);
                    break;
                case "Give someone a timeout":
                    this.twitchChatClient.createTimeout();
                    break;
                case "Let's play werewolf":
                    this.twitchChatClient.startWerewolf(user);
                    break;
            }
        }
    }
}

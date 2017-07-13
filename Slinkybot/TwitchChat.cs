using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Slinkybot
{
    class TwitchCommand
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public string username { get; }
        public string message { get; }
        public bool isWhisper { get; }

        public bool validMessage { get; }
        public TwitchCommand(string buffer)
        {
            validMessage = false;
            buffer = buffer.Replace(":", "");
            string delimStr = " ";
            char[] delimiter = delimStr.ToCharArray();
            string[] parameters = buffer.Split(delimiter, 4);
            if (parameters.Length == 4)
            {
                //foreach(string p in parameters)
                //    log.Debug(p);
                username = parameters[0].Split('!')[0];
                message = parameters[3];
                isWhisper = parameters[1].Equals("WHISPER");
                validMessage = true;
            }

        }
    }
    class TwitchChat
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private bool isConnected;
        private IrcClient client;
        public string userName { get; set; }
        private string authCode;
        public string channel { get; set; }

        public GymCommands gymBotCommand;
        private Thread commandThread;
        private Thread wtCommandThread;
        public bool isWtRunning { get; private set; }

        public TwitchChat(ConnectionConfig config)
        {
            this.userName = config.username;
            this.authCode = config.oauth;
            this.channel = config.channel;
            isConnected = false;

            gymBotCommand = new GymCommands(channel);
            client = new IrcClient("irc.twitch.tv", 6667, userName, authCode);

            isWtRunning = false;
        }

        public void Connect()
        {

            client.connect();
            
            client.joinRoom(channel);
            //gymBotCommand = new GymCommands(channel);

            isConnected = true;
            commandThread = new Thread(new ThreadStart(ProcessCommands));
            commandThread.IsBackground = true;
            commandThread.Start();
        }
        
        public void Disconnect()
        {
            isConnected = false;
            //gymBotCommand.stopProcessing();
            client.disconnect();

        }

        public void runWondertrade()
        {
            log.Debug("Running wondertrade");
            lock (this)
            {
                if (!isWtRunning)
                {
                    isWtRunning = true;
                    wtCommandThread = new Thread(new ThreadStart(WtThread));
                    wtCommandThread.IsBackground = true;
                    wtCommandThread.Start();
                }
            }
        }

        private void WtThread()
        {
            client.SendChatMessage("/subscribers");
            client.SendChatMessage("Wondertrade in 3...");
            Thread.Sleep(1500);
            client.SendChatMessage("Wondertrade in 2...");
            Thread.Sleep(1500);
            client.SendChatMessage("Wondertrade in 1...");
            Thread.Sleep(1500);
            client.SendChatMessage("Wondertrade GO!!!");
            client.SendChatMessage("/subscribersoff");
            isWtRunning = false;
            log.Debug("Wondertrade completed");

        }
        public void ProcessCommands()
        {
            while (isConnected)
            {
                try
                {
                    string message = client.readMessage();
                    log.DebugFormat("<< {0}", message);
                    TwitchCommand tc = new TwitchCommand(message);

                    if (tc.validMessage)
                    {
                        if (tc.message.StartsWith("!hello"))
                        {
                            //System.Windows.MessageBox.Show(message);
                            //client.SendChatMessage(String.Format("Well hello there @{0}!", tc.username));
                        }
                        else
                        {
                            TwitchResponse response = gymBotCommand.processCommand(tc.username, tc.message);
                            if (response != null)
                            {
                                if (tc.isWhisper && !response.publicNotification)
                                {
                                    client.SendWhisperMessage(tc.username, response.response);
                                }
                                else
                                {
                                    client.SendChatMessage(response.response);
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    log.WarnFormat("Something when wrong processing commands.... {0}", ex.ToString());
                }
            }
        }
    }
}

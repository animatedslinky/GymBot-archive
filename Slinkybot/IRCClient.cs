using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Slinkybot
{
    class IrcClient
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string userName;
        private string channel;
        private TcpClient tcpClient;
        private StreamReader inputStream;
        private StreamWriter outputStream;
        private string ip;
        private int port;
        private string password;
        private BlockingCollection<string> messageQueue;
        private Thread readThreadHandle;
        CancellationToken cancelToken;
        CancellationTokenSource cancelSource;
        private bool isConnected;
        public IrcClient(string ip, int port, string userName, string password)
        {
            this.userName = userName;
            this.port = port;
            this.ip = ip;
            this.password = password;
            messageQueue = new BlockingCollection<string>();
        }

        public void connect()
        {
            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;
            messageQueue = new BlockingCollection<string>();
            tcpClient = new TcpClient(ip, port);
            inputStream = new StreamReader(tcpClient.GetStream());
            outputStream = new StreamWriter(tcpClient.GetStream());
            isConnected = true;
            outputStream.WriteLine("PASS {0}", password);
            outputStream.WriteLine("NICK {0}", userName);
            outputStream.WriteLine("User {0} 0 * :{0}", userName);
            outputStream.WriteLine("CAP REQ :twitch.tv/commands");
            outputStream.Flush();
            ThreadStart ts = new ThreadStart(readThread);
            readThreadHandle = new Thread(ts);
            readThreadHandle.IsBackground = true;
            readThreadHandle.Start();

        }

        public void disconnect()
        {
            isConnected = false;
            tcpClient.Close();
            tcpClient = null;
            inputStream.Close();
            outputStream.Close();
            cancelSource.Cancel();


        }
        public void joinRoom(string channel)
        {
            this.channel = channel;
            outputStream.WriteLine("Join #{0}", channel);
            outputStream.Flush();
        }

        private void sendIrcMessage(string message)
        {
            Console.WriteLine("Trying to send {0}", message);
            outputStream.WriteLine(message);
            outputStream.Flush();
        }

        public void SendChatMessage(string message)
        {
            log.DebugFormat(">> Sending chat message {0}", message);
            sendIrcMessage(String.Format(":{0}!{0}@{0}.tmi.twitch.tv PRIVMSG #{1} :{2}", userName, channel, message));
        }

        public void SendWhisperMessage(string whisperTo, string message)
        {
            log.DebugFormat(">> Whisper {0} message {1}", whisperTo, message);
            //sendIrcMessage(String.Format(":{0}!{0}@{0}.tmi.twitch.tv WHISPER {1} :{2}", userName, whisperTo, message));
            sendIrcMessage(String.Format("PRIVMSG #jtv :/w {0} {1}", whisperTo, message));
        }
        private void readThread()
        {
            while (isConnected)
            {
                try
                {
                    string message = inputStream.ReadLine();
                    if (message.StartsWith("PING"))
                    {
                        sendPong();
                    }
                    else
                    {
                        messageQueue.Add(message);
                    }
                }
                catch (Exception)
                { }
            }

        }



        private void sendPong()
        {
            outputStream.WriteLine("PONG tmi.twitch.tv");
            outputStream.Flush();
        }
        public string readMessage()
        {
            string message = messageQueue.Take(cancelToken);
            return message;
        }
    }
}

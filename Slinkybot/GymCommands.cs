using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using log4net;
using log4net.Config;
using System.Collections.ObjectModel;

namespace Slinkybot
{

    public class Links
    {
    }

    public class Chatters
    {
        public List<string> moderators { get; set; }
        public List<string> staff { get; set; }
        public List<string> admins { get; set; }
        public List<string> global_mods { get; set; }
        public List<string> viewers { get; set; }
    }
    public class TwitchResponse
    {
        public bool publicNotification { get; set; }
        public string response { get; set; }
    }
    public class TwitchChannelApi
    {
        public Links _links { get; set; }
        public int chatter_count { get; set; }
        public Chatters chatters { get; set; }
    }

    class GymCommands
    {
        private const int numberOfMissedChecksAllowed = 5;
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Thread channelListThreadHandle;
        private string channel;

        private List<string> chattersList;
        private bool isRunning;

        public ObservableCollection<GymLeader> gymLeaders;

        EventWaitHandle sleepHandle;
        public enum GymType
        {
            Gym,
            Elite4
        };

        public class GymLeader
        {
            public string Name { get; set; }
            public string Online { get; set; }
            public GymType gymType { get; set; }
            public int offlineCountdown { get; set; }
            public string gymUpCommand { get; set; }
            public string gymDownCommand { get; set; }
            public string gymUpMessage { get; set; }
            public string gymDownMessage { get; set; }
        }

        public GymCommands(string channel)
        {
            //XmlConfigurator.Configure(new FileInfo(@"d:\gymbot-log4net.xml"));
            sleepHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            isRunning = true;
            this.channel = channel;

            gymLeaders = new ObservableCollection<GymLeader>()
            {
                new GymLeader() {Name = "kyawolfcupcakes",      gymType=GymType.Elite4, Online = "Offline", offlineCountdown=0, gymUpCommand = "", gymDownCommand = "", gymUpMessage = "", gymDownMessage = ""},
                new GymLeader() {Name = "swagmandergaming",     gymType=GymType.Elite4, Online = "Offline", offlineCountdown=0, gymUpCommand = "", gymDownCommand = "", gymUpMessage = "", gymDownMessage = ""},
                new GymLeader() {Name = "marshallartistuk",     gymType=GymType.Elite4, Online = "Offline", offlineCountdown=0, gymUpCommand = "", gymDownCommand = "", gymUpMessage = "", gymDownMessage = ""},
                new GymLeader() {Name = "strikewitch50",        gymType=GymType.Elite4, Online = "Offline", offlineCountdown=0, gymUpCommand = "", gymDownCommand = "", gymUpMessage = "", gymDownMessage = ""},
                new GymLeader() {Name = "cybereli01",           gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!flowerup", gymDownCommand = "!flowerdown", gymUpMessage = "@cybereli01 has opened their gym, please whisper them your fc and ign to queue up!",       gymDownMessage = "@cybereli01 has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "soccerdude26",         gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!flyup",    gymDownCommand = "!flydown",    gymUpMessage = "@soccerdude26 has opened their gym, please whisper them your fc and ign to queue up!",     gymDownMessage = "@soccerdude26 has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "9connor4",             gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!iceup",    gymDownCommand = "!icedown",    gymUpMessage = "@9connor4 has opened their gym, please whisper them your fc and ign to queue up!",         gymDownMessage = "@9connor4 has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "newpokebattler",       gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!fairyup",  gymDownCommand = "!fairydown",  gymUpMessage = "These fairies don't play nice, can you keep them in check? @newpokebattler has opened their gym, please whisper them your fc and ign to queue up!",   gymDownMessage = "@newpokebattler has closed their gym, please return next time!"},
                new GymLeader() {Name = "scarfedsylveon",       gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!dragonup", gymDownCommand = "!dragondown", gymUpMessage = "@scarfedsylveon has opened their gym, please whisper them your fc and ign to queue up!",   gymDownMessage = "@scarfedsylveon has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "dillconley112",        gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!zapup",    gymDownCommand = "!zapdown",    gymUpMessage = "@dillconley112 has opened their gym, please whisper them your fc and ign to queue up!",    gymDownMessage = "@dillconley112 has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "magicjnm",             gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!ghostup",  gymDownCommand = "!ghostdown",  gymUpMessage = "@magicjnm has opened their gym, please whisper them your fc and ign to queue up!",         gymDownMessage = "@magicjnm has closed their gym, they are no longer taking battles for now."},
                new GymLeader() {Name = "blacksunomega",        gymType=GymType.Gym,    Online = "Offline", offlineCountdown=0, gymUpCommand = "!rockup",  gymDownCommand = "!rockdown",  gymUpMessage = "@blacksunomega has opened their gym, please whisper them your fc and ign to queue up!",         gymDownMessage = "@blacksunomega has closed their gym, they are no longer taking battles for now."},
            };

            chattersList = new List<string>();
            channelListThreadHandle = new Thread(new ThreadStart(channelListThread));
            channelListThreadHandle.IsBackground = true;
            channelListThreadHandle.Start();
        }

        private TwitchResponse processGymCommand()
        {
            TwitchResponse response = null;
            StringBuilder sb = new StringBuilder();
            lock (this)
            {
                //sb.Append("Elite 4 members currently online:");
                //var elite4online = from leader in gymLeaders
                //                   where leader.Online == "Open" &&
                //                   leader.gymType == GymType.Elite4
                //                   select leader;
                var gymsOnline = from leader in gymLeaders
                                 where leader.Online == "Open" &&
                                 leader.gymType == GymType.Gym
                                 select leader;
                //if (elite4online.Count() > 0)
                //{
                //    foreach (GymLeader s in elite4online)
                //        sb.Append(String.Format(" {0} ", s.Name));
                //}
                //else
                //{
                //    sb.Append(" None ");
                //}
                if (gymsOnline.Count() > 0)
                {
                    sb.Append("The following gym leaders currently have their gyms open:");
                    foreach (GymLeader s in gymsOnline)
                        sb.Append(String.Format(" @{0} ", s.Name));
                }
                else
                {
                    sb.Append("Sorry, there are currently no gyms accepting challengers.");
                }
                response = new TwitchResponse();
                response.publicNotification = false;
                response.response = sb.ToString();
            }
            return response;
        }

        private TwitchResponse processOpenGymCommand(string username, string messageformat)
        {
            TwitchResponse response = null;
            var leader = from l in gymLeaders
                         where l.Name.ToLower() == username.ToLower()
                         select l;
            if (leader.Count() > 0)
            {
                response = new TwitchResponse();
                response.response = String.Format(messageformat, username);
                response.publicNotification = true;
                leader.First().Online = "Open";
                leader.First().offlineCountdown = numberOfMissedChecksAllowed;
            }
            return response;
        }

        private TwitchResponse processCloseGymCommand(string username, string messageformat)
        {
            TwitchResponse response = null;
            var leader = from l in gymLeaders
                         where l.Name.ToLower() == username.ToLower()
                         select l;
            if (leader.Count() > 0)
            {
                response = new TwitchResponse();
                response.response = String.Format(messageformat, username);
                response.publicNotification = true;
                leader.First().Online = "Online";
            }
            return response;
        }
        public TwitchResponse processCommand(string username, string command)
        {
            if (isRunning == false)
                return null;
            TwitchResponse response = null;
            if (command.StartsWith("!gyms"))
            {
                response = processGymCommand();
            }
            else if (command.ToLower().StartsWith("!"))
            {
                var leader = from l in gymLeaders
                             where l.Name.ToLower() == username.ToLower()
                             select l;
                if (leader.Count() > 0)
                {
                    if (command.ToLower().StartsWith(leader.First().gymUpCommand))
                        response = processOpenGymCommand(username, leader.First().gymUpMessage);
                    else if (command.ToLower().StartsWith(leader.First().gymDownCommand))
                        response = processCloseGymCommand(username, leader.First().gymDownMessage);
                }
            }
            return response;
        }

        public void stopProcessing()
        {
            isRunning = false;
            sleepHandle.Set();
        }

        private TwitchChannelApi QueryUserList()
        {
            TwitchChannelApi chatters = null;
            try
            {
                string contents;
                string url = String.Format(@"http://tmi.twitch.tv/group/user/{0}/chatters", channel);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    HttpStatusCode statusCode = ((HttpWebResponse)response).StatusCode;
                    if (statusCode == HttpStatusCode.OK)
                        contents = reader.ReadToEnd();
                    else
                        contents = "";
                }
                log.DebugFormat("Got viewers list: {0}", contents);
                chatters = JsonConvert.DeserializeObject<TwitchChannelApi>(contents);
            }
            catch (Exception ex)
            {
                log.WarnFormat("Failed to parse view list: {0}", ex.ToString());
            }
            return chatters;
        }
        private void channelListThread()
        {
            while (isRunning)
            {
                try
                {
                    log.Debug("Getting viewers list");
                    TwitchChannelApi viewerList = QueryUserList();
                    if (viewerList != null)
                    {
                        lock (this)
                        {
                            chattersList = new List<string>();

                            foreach (string s in viewerList.chatters.moderators)
                            {
                                log.DebugFormat("Adding {0} to chatters list", s);
                                chattersList.Add(s.ToLower());
                            }
                            foreach (string s in viewerList.chatters.viewers)
                            {
                                log.DebugFormat("Adding {0} to chatters list", s);
                                chattersList.Add(s.ToLower());
                            }

                            for (int x = 0; x < gymLeaders.Count; x++)
                            {
                                if (chattersList.Contains(gymLeaders[x].Name.ToLower()))
                                {
                                    if (!gymLeaders[x].Online.Equals("Open"))
                                    {
                                        gymLeaders[x].Online = "Online";
                                    }
                                }
                                else
                                {
                                    if (gymLeaders[x].offlineCountdown > 0)
                                    {
                                        gymLeaders[x].offlineCountdown--;
                                    }
                                    else
                                    {
                                        gymLeaders[x].Online = "Offline";
                                    }
                                }
                            }
                        }
                        //gymLeaders.CollectionChanged
                        sleepHandle.WaitOne(60 * 1000);
                        //Thread.Sleep(60000);
                    }
                    else
                    {
                        log.Debug("Viewers list is null");
                        //Thread.Sleep(10000);
                        sleepHandle.WaitOne(10000);
                    }
                }
                catch (Exception ex)
                {
                    log.WarnFormat("Something went wrong processing the channel lists... {0}", ex.ToString());
                }
            }
            log.Debug("Exiting gym thread");
        }
    }
}

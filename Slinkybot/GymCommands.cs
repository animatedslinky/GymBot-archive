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
        private string gymsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlinkyBot\\Gyms.xml");

        private const int numberOfMissedChecksAllowed = 5;
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Thread channelListThreadHandle;
        private string channel;

        private List<string> chattersList;
        private bool isRunning;

        [JsonProperty("GymLeaders")]
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

            [JsonProperty("GymType")]
            public GymType gymType { get; set; }

            public int offlineCountdown { get; set; }
            //public string gymUpCommand { get; set; }
            //public string gymDownCommand { get; set; }
            public string gymUpMessage { get; set; }
            public string gymDownMessage { get; set; }
        }

        public GymCommands(string channel)
        {
            //XmlConfigurator.Configure(new FileInfo(@"d:\gymbot-log4net.xml"));
            sleepHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            isRunning = true;
            this.channel = channel;

            gymLeaders = new ObservableCollection<GymLeader>();

            if (File.Exists(gymsFile))
            {
                using (StreamReader file = File.OpenText(gymsFile))
                {
                    var leaders = JsonConvert.DeserializeObject<ObservableCollection<GymCommands.GymLeader>>(file.ReadToEnd());
                    foreach (GymCommands.GymLeader leader in leaders)
                    {
                        gymLeaders.Add(leader);
                    }
                }

            }

            chattersList = new List<string>();
            channelListThreadHandle = new Thread(new ThreadStart(channelListThread));
            channelListThreadHandle.IsBackground = true;
            channelListThreadHandle.Start();
        }

        private void UpdateGymFile()
        {
            using (StreamWriter file = File.CreateText(gymsFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, gymLeaders);
            }
        }

        public void AddOrUpdateLeader(GymLeader leader)
        {
            var item = gymLeaders.FirstOrDefault(i => i.Name.ToLower() == leader.Name.ToLower());
            if (item != null)
            {
                int i = gymLeaders.IndexOf(item);
                gymLeaders[i] = leader;
            }
            else
            {
                gymLeaders.Add(leader);
            }
            UpdateGymFile();
            
        }

        public void RemoveLeader(string name)
        {
            var item = gymLeaders.FirstOrDefault(i => i.Name.ToLower() == name);
            if (item != null)
            {
                gymLeaders.Remove(item);
                UpdateGymFile();
            }
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
            else if (command.ToLower().StartsWith("!gymup"))
            {
                var leader = from l in gymLeaders
                             where l.Name.ToLower() == username.ToLower()
                             select l;
                response = processOpenGymCommand(username, leader.First().gymUpMessage);
            }
            else if (command.ToLower().StartsWith("!gymdown"))
            {
                var leader = from l in gymLeaders
                             where l.Name.ToLower() == username.ToLower()
                             select l;
                response = processCloseGymCommand(username, leader.First().gymDownMessage);
            }

                //else if (command.ToLower().StartsWith("!"))
                //{
                //    var leader = from l in gymLeaders
                //                 where l.Name.ToLower() == username.ToLower()
                //                 select l;
                //    if (leader.Count() > 0)
                //    {
                //        if (command.ToLower().StartsWith(leader.First().gymUpCommand))
                //            response = processOpenGymCommand(username, leader.First().gymUpMessage);
                //        else if (command.ToLower().StartsWith(leader.First().gymDownCommand))
                //            response = processCloseGymCommand(username, leader.First().gymDownMessage);
                //    }
                //}
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

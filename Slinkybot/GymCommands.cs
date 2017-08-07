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

    public class GymLeader
    {
        public string Name { get; set; }
        public string Online { get; set; }

        [JsonProperty("GymType")]
        public GymType gymType { get; set; }

        public int offlineCountdown { get; set; }
        public string gymUpMessage { get; set; }
        public string gymDownMessage { get; set; }
        public DateTime LastOpen { get; set; }
    }


    public class badgeInfo
    {
        public string GymLeader { get; set; }
        public string Trainer { get; set; }
        public DateTime Earned { get; set; }
    }

    public enum GymType
    {
        Gym,
        Elite4
    };

    class GymCommands
    {

        private string badgeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlinkyBot\\Badges.xml");
        public List<badgeInfo> badgeList;

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

        public GymCommands(string channel)
        {
            //XmlConfigurator.Configure(new FileInfo(@"d:\gymbot-log4net.xml"));
            sleepHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            isRunning = true;
            this.channel = channel;

            gymLeaders = new ObservableCollection<GymLeader>();
            badgeList = new List<badgeInfo>();
            if (File.Exists(gymsFile))
            {
                using (StreamReader file = File.OpenText(gymsFile))
                {
                    var leaders = JsonConvert.DeserializeObject<ObservableCollection<GymLeader>>(file.ReadToEnd());
                    foreach (GymLeader leader in leaders)
                    {
                        gymLeaders.Add(leader);
                    }
                }
            }

            if (File.Exists(badgeFile))
            {
                using (StreamReader file = File.OpenText(badgeFile))
                {
                    var badges = JsonConvert.DeserializeObject<List<badgeInfo>>(file.ReadToEnd());
                    foreach (badgeInfo badge in badges)
                    {
                        badgeList.Add(badge);
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

        private void UpdateBadgeFile()
        {
            using (StreamWriter file = File.CreateText(badgeFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, badgeList);
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

        private TwitchResponse givebadge(string leaderName, string challenger)
        {
            TwitchResponse response = null;
            var leader = from l in gymLeaders
                         where l.Name.ToLower() == leaderName.ToLower()
                         select l;
            if (leader.Count() > 0)
            {
                badgeInfo badge = new badgeInfo
                {
                    GymLeader = leaderName,
                    Trainer = challenger,
                    Earned = DateTime.Now
                   
                };

                var checklist = from l in badgeList
                                where l.GymLeader.ToLower().Equals(leaderName.ToLower()) &&
                                      l.Trainer.ToLower().Equals(challenger.ToLower())
                                select l;

                if (checklist.FirstOrDefault() == null)
                {
                    badgeList.Add(badge);
                    UpdateBadgeFile();
                    response = new TwitchResponse
                    {
                        publicNotification = true,
                        response = String.Format("@{0} has earned the badge from @{1}.",challenger,leaderName)
                    };
                }
                else
                {
                    response = new TwitchResponse
                    {
                        publicNotification = true,
                        response = String.Format("@{0} already earned the badge from @{1} on {2}.",challenger,leaderName, checklist.First().Earned)
                    };
                }
            }
            return response;
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
                leader.First().LastOpen = DateTime.Now;
                UpdateGymFile();
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
            else if (command.ToLower().StartsWith("!opengym"))
            {
                var leader = from l in gymLeaders
                             where l.Name.ToLower() == username.ToLower()
                             select l;
                response = processOpenGymCommand(username, leader.First().gymUpMessage);
            }
            else if (command.ToLower().StartsWith("!closegym"))
            {
                var leader = from l in gymLeaders
                             where l.Name.ToLower() == username.ToLower()
                             select l;
                response = processCloseGymCommand(username, leader.First().gymDownMessage);
            }
            else if (command.ToLower().StartsWith("!givebadge"))
            {
                string[] buffer = command.Split(' ');
                if (buffer.Count() == 2)
                    response = givebadge(username, buffer[1]);
            }
            else if (command.ToLower().StartsWith("!checkbadges"))
            {
                string checkuser = username;
                string[] buffer = command.Split(' ');
                if (buffer.Count() == 2)
                    checkuser = buffer[1];
                response = checkBadges(username, checkuser);

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

        private TwitchResponse checkBadges(string username, string checkuser)
        {
            TwitchResponse response = null;

            bool found = false;
            StringBuilder sb = new StringBuilder();

            var leader = from l in gymLeaders
                         where l.Name.ToLower() == username.ToLower() &&
                               l.gymType == GymType.Elite4
                         select l;

            if (leader.Count()>0)
            {
                var badges = from l in badgeList
                             where l.Trainer.ToLower().Equals(checkuser.ToLower())
                             select l;
                if (badges.Count() == 0)
                {
                    found = true;
                    sb.AppendFormat("@{0} has not yet obtained any badges",checkuser);
                }
                else if (badges.Count() == 1)
                {
                    found = true;
                    sb.AppendFormat("@{0} has obtained 1 badge from @{1}", checkuser, badges.First().GymLeader);
                }
                else
                {
                    found = true;
                    sb.AppendFormat("@{0} has obtained {1} badges from the following gyms: ", checkuser, badges.Count());
                    foreach (badgeInfo badge in badges)
                    {
                        sb.AppendFormat( "@{0} ", badge.GymLeader);
                    }
                }
            }
            else if (username.ToLower().Equals(checkuser.ToLower()))
            {
                var badges = from l in badgeList
                             where l.Trainer.ToLower().Equals(checkuser.ToLower())
                             select l;
                if (badges.Count() == 0)
                {
                    found = true;
                    sb.AppendFormat("@{0} has not yet obtained any badges", checkuser);
                }
                else if (badges.Count() == 1)
                {
                    found = true;
                    sb.AppendFormat("@{0} has obtained 1 badge from @{1}", checkuser, badges.First().GymLeader);
                }
                else
                {
                    found = true;
                    sb.AppendFormat("@{0} has obtained {1} badges from the following gyms: ", checkuser, badges.Count());
                    foreach (badgeInfo badge in badges)
                    {
                        sb.AppendFormat(" @{0} ", badge.GymLeader);
                    }
                }
            }

            if (found)
            {
                response = new TwitchResponse
                {
                    publicNotification = true,
                    response = sb.ToString()
                };
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

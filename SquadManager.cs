/* SquadManager.cs

Copyright 2015 by LumPenPacK
    
 * Contact: prei[-@-]me.com

Permission is hereby granted, free of charge, to any person or organization
obtaining a copy of the software and accompanying documentation covered by
this license (the "Software") to use, reproduce, display, distribute,
execute, and transmit the Software, and to prepare derivative works of the
Software, and to permit third-parties to whom the Software is furnished to
do so, without restriction.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

*/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.Reflection;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

    //Aliases
    using EventType = PRoCon.Core.Events.EventType;
    using CapturableEvent = PRoCon.Core.Events.CapturableEvents;


    public class SquadManager : PRoConPluginAPI, IPRoConPluginInterface
    {

        private int fDebugLevel;
        private bool enabled;

        private Squads squads;
        private bool WaitingSquadList;
        private bool WaitingSquadLeaders;
        private bool BuildComplete;
        private bool BuildCompleteMessageSent;
        private String GameMode;
        private bool? SquadSwitchPossible;
        private double RoundTime;
        private List<NewPlayer> NewPlayersQueue;
        private List<String> RestoreOnRoundStart;
        private bool started;
        private System.Timers.Timer aTimer;
        private System.Timers.Timer bTimer;
        private System.Timers.Timer cTimer;
        private System.Timers.Timer PluginIntervalTimer;
        private List<CPlayerInfo> PlayersList;
        private bool? ReservedSlotsReceived;
        private List<Vote> Votes;
        bool RestoreComplete;
        private List<SquadInviter> ListSquadInviters;
        private int ServerSize;
        private int CurrentPlayers;
        private int[] CurrentPlayersTeams;
        private List<List<object>> JoinSwitchQueue;
        public List<String> Messages;
        private int MessageCounter;

        public static String[] SQUAD_NAMES = new String[] { "None",
      "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
      "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa",
      "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray",
      "Yankee", "Zulu", "Haggard", "Sweetwater", "Preston", "Redford", "Faith", "Celeste"};

        // Settings

        private bool RestoreSquads;
        private bool RemoveIdleLeader;
        private bool RemoveNoOrdersLeader;
        private bool SendWarnings = false;
        private int NoOrdersWarnings = 3;
        private List<String> WhiteList;
        private bool UseReservedList;
        private List<String> ReservedSlots;
        private bool VoteDismiss;
        private static int VotesNeededDismiss;
        private bool UseLeaderList;
        private bool Enforce;
        private bool YellVote;
        private static int VoteDuration;
        private int MaxWaiting;
        private bool YellWarnings;
        private int MaxIdleTime;
        private bool ExcludeRush;
        private bool ExcludeChainLink;
        private bool InviteCommand;
        private static int MaxInvites;
        private bool SquadLeadersOnly;
        private bool AllowTeamSwitches;
        private static bool WriteMessages;
        private int Interval;
        private static int HowManyInviteMessages;
        private bool MoveLead;
        private bool UnlockSquads;
        private bool Regroup;

        public class Squad
        {
            private int[] ID { get; set; }
            private List<String> Members { get; set; }
            private String Leader;

            private DateTime LastUpdated { get; set; }
            private int LeaderIdleTime { get; set; }
            private DateTime LastOrder { get; set; }
            private int LeaderOrders { get; set; }
            private int OrderWarnings { get; set; }

            public Squad(String FirstPlayer, int TeamID, int SquadID)
            {
                this.Members = new List<String>();
                this.Members.Add(FirstPlayer);

                if (TeamID > 0 && SquadID > 0)
                    this.Leader = FirstPlayer;
                else
                    this.Leader = "#NONE#";

                this.ID = new int[2] { TeamID, SquadID };

                this.LeaderIdleTime = 0;
                this.LastUpdated = DateTime.Now.AddSeconds(-300);
                this.LastOrder = DateTime.Now;
                this.LeaderOrders = 0;
                this.OrderWarnings = 0;
            }

            public Squad(int TeamID, int SquadID)
            {
                this.Members = new List<String>();

                if (TeamID > 0 && SquadID > 0)
                    this.Leader = String.Empty;
                else
                    this.Leader = "#NONE#";

                this.ID = new int[2] { TeamID, SquadID };

                this.LeaderIdleTime = 0;
                this.LastUpdated = DateTime.Now.AddSeconds(-300);
                this.LastOrder = DateTime.Now;
                this.LeaderOrders = 0;
                this.OrderWarnings = 0;
            }

            public int[] getID()
            {
                return this.ID;
            }

            public int getID(int i)
            {
                return this.ID[i];
            }

            public void AddPlayer(String member)
            {
                if (!this.Members.Contains(member))
                {
                    if (this.Members.Count == 0)
                    {
                        SetSquadLeader(member);
                    }

                    this.Members.Add(member);
                }
            }

            public int[] RemPlayer(String member)
            {

                int[] RequestLeader = new int[2];

                this.Members.Remove(member);

                if (this.Leader.Equals(member))
                {
                    if (this.Members.Count == 1)
                    {
                        SetSquadLeader(member);
                    }
                    else if (this.Members.Count == 0)
                    {
                        SetSquadLeader(String.Empty);
                    }

                    RequestLeader[0] = getID(0);
                    RequestLeader[1] = getID(1);

                }
                return RequestLeader;
            }

            public void SetSquadLeader(String leader)
            {
                if (getID(1) == 0)
                    this.Leader = "#NONE#";
                else
                    this.Leader = leader;

                this.LeaderIdleTime = 0;
                this.LastUpdated = DateTime.Now.AddSeconds(-300);
                this.LastOrder = DateTime.Now;
                this.LeaderOrders = 0;
                this.OrderWarnings = 0;
            }

            public void UnsetSquadLeader()
            {
                if (getID(1) == 0)
                    this.Leader = "#NONE#";
                else
                    this.Leader = String.Empty;

                this.LeaderIdleTime = 0;
                this.LastUpdated = DateTime.Now.AddSeconds(-300);
                this.LastOrder = DateTime.Now;
                this.LeaderOrders = 0;
                this.OrderWarnings = 0;
            }

            public String GetSquadLeader()
            {
                return this.Leader;
            }

            public String GetNoSquadLeader()
            {
                if (getID(1) == 0)
                {
                    return "#NONE#";
                }

                if (Members.Count == 1)
                {
                    return String.Empty;
                }

                if (Members.Count == 2)
                {
                    return (Members[0] == GetSquadLeader()) ? Members[1] : Members[0];
                }

                Random rnd = new Random();
                int index = rnd.Next(Members.Count);
                String NewLeader = Members[index];

                if (NewLeader == GetSquadLeader())
                    return (Members[0] == GetSquadLeader()) ? Members[1] : Members[0];
                else
                    return NewLeader;
            }

            public int getOrderWarnings()
            {
                return this.OrderWarnings;
            }

            public void setOrderWarnings()
            {
                this.OrderWarnings++;
            }

            public String getName()
            {
                return SQUAD_NAMES[getID(1)];
            }

            public void setID(int i, int value)
            {
                this.ID[i] = value;
            }

            public void OnRoundStart()
            {
                this.LeaderIdleTime = 0;
                this.LastUpdated = DateTime.Now.AddSeconds(-300);
                this.LastOrder = DateTime.Now;
                this.LeaderOrders = 0;
                this.OrderWarnings = 0;
            }

            public void setLeaderOrders()
            {
                this.LeaderOrders++;
                this.LastOrder = DateTime.Now;
            }

            public int getLeaderOrders()
            {
                return this.LeaderOrders;
            }

            public int getTimeLastOrders()
            {
                return Convert.ToInt32(DateTime.Now.Subtract(this.LastOrder).TotalSeconds);
            }

            public void setLeaderIdle(int IdleTime)
            {
                this.LeaderIdleTime = IdleTime;
                this.LastUpdated = DateTime.Now;
            }

            public int getLeaderIdleTimeSeconds()
            {
                return this.LeaderIdleTime;
            }

            public int getLeaderIdleTimeLastUpdateSeconds()
            {
                return Convert.ToInt32(DateTime.Now.Subtract(this.LastUpdated).TotalSeconds);
            }

            public List<String> getMembers()
            {
                return this.Members;
            }

            public bool isMember(String SoldierName)
            {
                return this.Members.Contains(SoldierName);
            }

            public bool isMember(CPlayerInfo Soldier)
            {
                return this.Members.Contains(Soldier.SoldierName);
            }

            public int getSize()
            {
                return Members.Count;
            }

            public bool Equals(Squad OtherSquad)
            {
                return this.getID(0).Equals(OtherSquad.getID(0)) && this.getID(1).Equals(OtherSquad.getID(1));
            }
        }
        public class Squads
        {
            private List<Squad> SquadList { get; set; }

            public Squads()
            {
                this.SquadList = new List<Squad>();
            }

            public void AddSquad(Squad squad)
            {
                if (!this.SquadList.Contains(squad))
                    this.SquadList.Add(squad);
            }

            public void RemSquad(Squad squad)
            {
                this.SquadList.Remove(squad);
            }

            public int[] AddPlayer(String SoldierName, int TeamID, int SquadID)
            {
                int[] RequestLeader = new int[2];
                bool SquadFound = false;
                bool PlayerDelete = false;

                if (String.IsNullOrEmpty(SoldierName))
                    return RequestLeader;

                foreach (Squad squad in SquadList)
                {
                    if (squad.getMembers().Contains(SoldierName))
                    {
                        RequestLeader = squad.RemPlayer(SoldierName);
                        PlayerDelete = true;
                    }

                    if (squad.getID(0) == TeamID && squad.getID(1) == SquadID)
                    {
                        squad.AddPlayer(SoldierName);
                        if (PlayerDelete)
                            return RequestLeader;
                        SquadFound = true;
                    }
                }

                if (SquadFound)
                    return RequestLeader;

                Squad NewSquad = new Squad(SoldierName, TeamID, SquadID);
                SquadList.Add(NewSquad);

                return RequestLeader;
            }

            public void AddPlayer(CPlayerInfo player)
            {
                if (player == null)
                    return;

                AddPlayer(player.SoldierName, player.TeamID, player.SquadID);
            }

            public int[] RemPlayer(String SoldierName)
            {
                int[] RequestLeader = new int[2];

                foreach (Squad squad in SquadList)
                {
                    if (squad.getMembers().Contains(SoldierName))
                    {
                        RequestLeader = squad.RemPlayer(SoldierName);

                        /*if (squad.getMembers().Count == 0)
                            SquadList.Remove(squad);*/
                    }
                }
                return RequestLeader;
            }

            public Squad SearchSquad(int TeamID, int SquadID)
            {
                foreach (Squad squad in SquadList)
                {
                    if (squad.getID(0) == TeamID && squad.getID(1) == SquadID)
                        return squad;
                }

                return null;
            }

            public Squad SearchSquad(CPlayerInfo Soldier)
            {
                if (Soldier == null || String.IsNullOrEmpty(Soldier.SoldierName))
                    return null;

                return SearchSquad(Soldier.TeamID, Soldier.SquadID);
            }

            public Squad SearchSquad(String SoldierName)
            {
                if (String.IsNullOrEmpty(SoldierName))
                    return null;

                foreach (Squad squad in SquadList)
                {
                    if (squad.isMember(SoldierName))
                        return squad;
                }
                return null;
            }

            public List<Squad> getSquads()
            {
                return this.SquadList;
            }

            public List<String> getSquadLeaders()
            {
                List<String> SquadLeaders = new List<String>();
                foreach (Squad squad in SquadList)
                    if (squad.GetSquadLeader() != String.Empty && squad.getID(0) > 0 && squad.getID(1) > 0)
                        SquadLeaders.Add(squad.GetSquadLeader());
                return SquadLeaders;
            }

            public void Clear()
            {
                SquadList.Clear();
            }

        }
        public class Vote
        {
            private List<String> YesVotes;
            private String StartedBy;
            private DateTime TimeStamp;
            private int[] SquadID;
            private bool ResultSent;
            private bool VoteCanceled;

            public Vote(String StartedBy, int TeamID, int SquadID)
            {
                this.YesVotes = new List<String>();
                this.StartedBy = StartedBy;
                this.TimeStamp = DateTime.Now;
                this.ResultSent = false;
                this.VoteCanceled = false;
                if (TeamID < 1 || SquadID < 1)
                    this.SquadID = new int[2] { -1, -1 };
                else
                    this.SquadID = new int[2] { TeamID, SquadID };
            }

            public bool getResultSent()
            {
                return this.ResultSent;
            }

            public void setResultSent(bool b)
            {
                this.ResultSent = b;
            }

            public String VoteYes(String Voter)
            {
                if (Voter == getVoteInitiator())
                    return "You've already voted by starting this vote.";
                else if (YesVotes.Contains(Voter))
                    return "You've already voted.";
                else
                {
                    YesVotes.Add(Voter);
                    return "Your vote has been counted.";
                }
            }

            public void RemoveVote(String Voter)
            {
                YesVotes.Remove(Voter);
                if (Voter == getVoteInitiator())
                    this.TimeStamp = DateTime.Now.AddSeconds(-9999);
            }

            public String getVoteInitiator()
            {
                return this.StartedBy;
            }

            public int getVoteAge()
            {
                return Convert.ToInt32(DateTime.Now.Subtract(this.TimeStamp).TotalSeconds);
            }

            public bool VoteIsRunning()
            {
                if (getVoteAge() > VoteDuration)
                    return false;
                else
                    return true;
            }

            public int getVoteID(int i)
            {
                if (i == 0 || i == 1)
                    return SquadID[i];
                else
                    return -1;
            }

            public int getVoteResult()
            {
                if (VoteCanceled)
                {
                    return 4;
                }
                else
                {
                    if (YesVotes.Count > VotesNeededDismiss)
                        return 2;
                    else if (VoteIsRunning())
                        return 1;
                    else
                        return 3;
                }

            }

            public void setVoteCanceled(bool b)
            {
                this.VoteCanceled = b;
            }
        }
        public class NewPlayer
        {
            int TeamID, SquadID;
            String SoldierName;
            public NewPlayer(String SoldierName)
            {
                this.SoldierName = SoldierName;
            }

            public NewPlayer(String SoldierName, int TeamID, int SquadID)
            {
                this.SoldierName = SoldierName;
                this.TeamID = TeamID;
                this.SquadID = SquadID;
            }

            public void setTeamID(int TeamID)
            {
                this.TeamID = TeamID;
            }

            public void setSquadID(int SquadID)
            {
                this.SquadID = SquadID;
            }

            public int getTeamID()
            {
                return TeamID;
            }

            public int getSquadID()
            {
                return SquadID;
            }

            public String getSoldierName()
            {
                return SoldierName;
            }
        }
        public class SquadInviter
        {
            String inviter;
            int RoundInvites;
            List<String> invitees;
            List<List<object>> MessagesSentTo;

            public SquadInviter(String Inviter)
            {
                this.inviter = Inviter;
                this.RoundInvites = 0;
                this.invitees = new List<String>();
                this.MessagesSentTo = new List<List<object>>();
            }

            public void RemoveInvite(String inviteeLeft)
            {
                invitees.Remove(inviteeLeft);
            }

            /* Inviter left ---> Could be used to spam players
            public void RemoveInvite()
            {

            }
            */

            public void SendInvite(String invitee)
            {
                List<object> message = new List<object>();
                message.Add(invitee);
                message.Add(0);
                MessagesSentTo.Add(message);
                this.invitees.Add(invitee);
                this.RoundInvites++;
            }

            public void SendMessageTo(String recipient)
            {
                foreach (List<object> entry in MessagesSentTo)
                {
                    if ((String)entry[0] == recipient)
                        entry[1] = (int)entry[1] + 1;
                }
            }

            public void SendMessageTo(String recipient, int Count)
            {
                foreach (List<object> entry in MessagesSentTo)
                {
                    if ((String)entry[0] == recipient)
                        entry[1] = Count;
                }
            }

            public List<String> getInvitees()
            {
                return this.invitees;
            }

            public String getInviter()
            {
                return this.inviter;
            }

            public int getRoundInvites()
            {
                return this.RoundInvites;
            }

            public int getMessagesSentTo(String AskRecipient)
            {
                foreach (List<object> entry in MessagesSentTo)
                {
                    if ((String)entry[0] == AskRecipient)
                        return (int)entry[1];
                }
                return int.MaxValue;
            }
        }


        public SquadManager()
        {
            WaitingSquadList = true;
            WaitingSquadLeaders = true;
            BuildComplete = false;
            BuildCompleteMessageSent = false;
            squads = new Squads();
            GameMode = String.Empty;
            SquadSwitchPossible = null;
            RoundTime = 0.0;
            ServerSize = 0;
            ReservedSlots = new List<String>();
            ReservedSlotsReceived = null;
            NewPlayersQueue = new List<NewPlayer>();
            started = true;
            bTimer = null;
            PlayersList = new List<CPlayerInfo>();
            RestoreOnRoundStart = new List<String>();
            Votes = new List<Vote>();
            RestoreComplete = true;
            ListSquadInviters = new List<SquadInviter>();
            CurrentPlayers = 0;
            JoinSwitchQueue = new List<List<object>>();  // TeamOrigin, TeamDestination, SquadOrigin, SquadDestion, force
            CurrentPlayersTeams = new int[4];
            MessageCounter = 0;


            Messages = new List<String>();

            // Settings

            RestoreSquads = false;
            RemoveIdleLeader = false;
            RemoveNoOrdersLeader = false;
            SendWarnings = false;
            NoOrdersWarnings = 3;
            WhiteList = new List<String>();
            UseReservedList = false;
            VoteDismiss = false;
            VotesNeededDismiss = 3;
            UseLeaderList = false;
            Enforce = true;
            MaxIdleTime = 30;
            YellVote = false;
            VoteDuration = 30;
            MaxWaiting = 3;
            YellWarnings = false;
            ExcludeRush = true;
            ExcludeChainLink = true;
            InviteCommand = true;
            MaxInvites = 3;
            SquadLeadersOnly = false;
            AllowTeamSwitches = true;
            WriteMessages = true;
            Interval = 120;
            HowManyInviteMessages = 3;
            MoveLead = true;
            UnlockSquads = false;
            Regroup = true;

        }
        public enum MessageType
        {
            Warning, Error, Exception, Normal
        };
        public String FormatMessage(String msg, MessageType type)
        {
            String prefix = "[^bSquadManager^n] ";

            if (type.Equals(MessageType.Warning))
                prefix += "^1^bWARNING^0^n: ";
            else if (type.Equals(MessageType.Error))
                prefix += "^1^bERROR^0^n: ";
            else if (type.Equals(MessageType.Exception))
                prefix += "^1^bEXCEPTION^0^n: ";

            return prefix + msg;
        }
        public void LogWrite(String msg)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
        }
        public void ConsoleWrite(string msg, MessageType type)
        {
            LogWrite(FormatMessage(msg, type));
        }
        public void ConsoleWrite(string msg)
        {
            ConsoleWrite(msg, MessageType.Normal);
        }
        public void ConsoleWarn(String msg)
        {
            ConsoleWrite(msg, MessageType.Warning);
        }
        public void ConsoleError(String msg)
        {
            ConsoleWrite(msg, MessageType.Error);
        }
        public void ConsoleException(String msg)
        {
            ConsoleWrite(msg, MessageType.Exception);
        }
        public void DebugWrite(string msg, int level)
        {
            if (fDebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
        }
        public void ServerCommand(params String[] args)
        {
            List<string> list = new List<string>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }
        public string GetPluginName()
        {
            return "SquadManager";
        }
        public string GetPluginVersion()
        {
            return "0.9.8.1";
        }
        public string GetPluginAuthor()
        {
            return "LumPenPacK";
        }
        public string GetPluginWebsite()
        {
            return "TBD";
        }
        public string GetPluginDescription()
        {
            return @"<h1>Squad Manager with the goal to improve Squad and Team Play</h1>



<p>Supported Games:</b> BF4<br>
Supported Game Modes:</b> All but not tested with SQDM yet.</p><br>

<p>Do you think this plug-in useful and you want to support my work on future updates?</p>
<form action=""https://www.paypal.com/cgi-bin/webscr"" method=""post"" target=""_blank"">
<input type=""hidden"" name=""cmd"" value=""_s-xclick"">
<input type=""hidden"" name=""hosted_button_id"" value=""H6MM23JN4SVHL"">
<input type=""image"" src=""https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif"" border=""0"" name=""submit"" alt=""PayPal - The safer, easier way to pay online!"">
<img alt="""" border=""0"" src=""https://www.paypalobjects.com/de_DE/i/scr/pixel.gif"" width=""1"" height=""1"">
</form>



<h2><p>Features and Settings</p></h2>

<blockquote> 
<p><b>1 - Restore Squad Leaders</b><br>
Use this feature to restore Squad Leaders after Team Scramble.<br>
For balancing reasons the plug-in doesn't restore Squads structure. This should be done by the Balancing tool, otherwise this plug-in would destroy the Team Scramble between the rounds.<br>
That's the reason why you should primarily use this option with Team Scramble that doesn't destroy Squads.<br>
It was tested with MULTIBalancer's ""Keep Squads Together"" feature.
</p>
</blockquote> 

<blockquote> 
<p><b>2 - Dismiss Idle Squad Leaders</b><br> 
With this option the plugin gives anyone else Squad Lead if the current Squad Leader is idling.<br>
The old Squad Leader stays in the Squad and a random other player from the Squad takes over Squad Lead.<br>
At the moment the new Squad Leader will be chosen randomly. For future updates there will be an option to choose the new Leader based on stats or reputation. <br>
You can choose the <b>Maximum Idle Time in seconds</b>. This value should be >= 30s since Squad Leaders idle times aren't updated faster than every 30s.<br>
</p>
</blockquote> 

<blockquote> 
<p><b>3 - Dismiss No Orders Squad Leaders</b><br> 
With this option the plugin gives anyone else Squad Lead if the current Squad Leader is giving no Squad Orders.<br>
The old Squad Leader stays in the Squad and a random other player from the Squad takes over Squad Lead.<br>
At the moment the new Squad Leader will be chosen randomly. For future updates there will be an option to choose the new Leader based on stats or reputation. <br>
Please note: Only Squad Leader commands via Comme Rose can be counted as Orders.<br>
<b>Only on Conquest</b> disables this feature on any other game Mode than Conquest.<br>
<b>Maximum waiting time for orders (minutes)</b> determines how long the plug-in waits for Squad Orders before it will warn or remove a Squad Leaders.<br>
If you enable <b>Send warnings before dismiss)</b>, a Squad Leader will get your selected number of warnings before dismiss.<br>
The interval between the warnings equals the Maximum waiting time for orders (minutes).<br>
You can also choose whether the warnings should be shown as yell message or not.
</p>
</blockquote> 

<blockquote> 
<p><b>4 - Squad Command Lead</b><br> 
Enable this option to give players with <b>Reserved Slot List</b> or VIPs (<b>Squad Leader List</b>) the possibility to take over the Squad Lead with <b>!lead</b> command in chat.<br>
You can add VIPs to the <b>Squad Leader List</b>. The Squad Leader List has a higher priority than the Reserved Slot List<br>

Squad Leader List >  Reserved Slot List > anyone else

If a Squad Leader is on the Reserved Slot List, someone else from Reserved Slot List can NOT apply for Squad Lead but players from Squad Leader List can do.<br>
If a Squad Leader is on the Squad Leader List, someone else from Reserved Slot or Squad Leader List List can NOT apply for Squad Lead.<br>
 </p>
</blockquote> 

<blockquote> 
<p><b>5 - Squad Command Vote</b><br> 
Squad members can apply for Squad Lead with <b>!newleader</b> command.<br>
Everyone in the Squad will receive messages and can vote for the new Squad Leader with <b>!accept</b> command.<br>
<b>Votes needed:</b> How many !accept votes are needed for a successful vote.<br> 
<b>Vote duration (seconds):</b> How long should the vote be active.<br> 
<b>Yell vote announcement:</b> Should the vote announcement shown as a yell message?<br> 
</p>
</blockquote> 

<blockquote> 
<p><b>6 - Squad Command Invite</b><br>  
You can invite players to join your Squad with <b>!invite [playername]</b> command.<br> 
Invitees can use <b>!join</b> or <b>!deny</b> command to accept or don't accept the invite.<br> 
If the Squad or Team of the player who has invited you is full, Invitees will join a <b>Waiting Queue</b> that will move them to the Team/Squad of the player who has invited you as soon as possible.<br> 
The invitee gets the invite message on his next death.<br> 
This has the advantage the invitee won't get spammed with yells (if enabled) and chat messages when the invitee is in combat. <br> 
It can also prevent most of the kills via admin move command because the invitee won't get killed again if he accepts the invite while he/she is waiting to spawn.<br> 
<b>Squad Leaders only:</b> Only Squad Leaders can send invites to other players.<br> 
<b>Allow Team Switches:</b> Players can invite other players even if they are in the enemy team.<br> 
<b>Maximum invites per round:</b> The maximum number of invites a player can send during a round.<br> 
<b>Send invite messages how often: </b> Determines how often an invite message should be shown when the invitee is death.<br> 
</p>
</blockquote> 

<blockquote> 
<p><b>7 - Squad Command GiveLead</b><br>  
If this option is enabled, Squad Leaders can give the leadership to someone else in Squad.<br>  
Example: If you are Squad Leader use the command <b>!givelead LumPenPacK</b> to give player LumPenPacK Squad Lead if you are currently Squad Leader.<br>  
</p>
</blockquote> 

<blockquote> 
<p><b>8 - Squad Unlock</b><br>  
<b>Unlock all Squads</b> feature can be used to force all Squads to be not private.<br> 
</p>
</blockquote> 

<blockquote> 
<p><b>9 - Dynamic Messages</b><br>  
Decide whether the plugin should send some chat messages how this plugin can be used by the players.<br> 
The messages will be updated automatically depending on the settings you use.<br> 
This means if you disable a feature or change a setting the chat message will be updated. <br> 
</p>
</blockquote> 

<h2><p>Commands</p></h2>
<blockquote> 
<p><b>!lead </b> 
</p>Give you Squad Lead if you're allowed.(Reserved Slot, Squad Leader List)<br> 
</blockquote> 

<blockquote> 
<p><b>!unlead </b> <br>  
</p>Give anyone else Squad Lead if you're Squad Leader.<br>  
</blockquote> 

<blockquote> 
<p><b>!newleader </b>  <br>  
</p>Start a Vote that ask everyone in Squad if you should be the new Squad Leader.<br>  
</blockquote> 

<blockquote> 
<p><b>!invite [playername] </b> <br>  
</p>Invite a player to join your Team/Squad.<br>  
</blockquote> 

<blockquote> 
<p><b>!join </b><br>   
</p>Accept an invite.<br>  
</blockquote> 

<blockquote> 
<p><b>!deny </b>  <br>  
</p>Don't accept an invite. You can't get any more invites from the player who has invited you this round.<br>  
</blockquote> 


<blockquote> 
<p><b>!givelead [playername]</b><br>  
</p>Give [playername] Squad Lead if you are currently Squad Leader.<br>  
</blockquote> 

<h2><p>Suggestions and Support</p></h2>
<p>Feel free to write any suggestion how this plugin could be improved into the plugin thread.</p><br>    

<h2><p>Known Issues</p></h2>
<li>It takes a few seconds to restore Squad Leaders on Round start. This time is necessary to rebuild the internal Squads structure.</li><br>
<li>Orders can only be detected if a Squad Leader uses Commo Rose to set Attack/Defent Orders. Commands set without a Commo Rose don't trigger any event that could be captured. </li><br>

<h2><p>Planned Updates</p></h2>

<li>Squad Lead based on Stats</li><br>
<li>Limit Weapons/Classes per Squad</li><br>
<li>Squad Lead based on Stats</li><br>
<li>Squad Leader Rotation</li><br>
<li>Merge Squads</li><br/>
<li>Force ""Semi-Normal"" Squad Spawn on Classic Mode</li><br>
<li>Enable/Disable ""Semi-Normal"" Squad Spawn depending on Team Balance</li><br>
<li> ...</li><br>
	
<p>Feel free to write any suggestion how this plugin could be improved into the plugin thread.</p><br>  

<h2><p>Changelog</p></h2>  
<blockquote><h4>0.9.8.1 (11-Jan-2015)</h4><br>  
<li>Plugin Approval release</li><br/>
</blockquote>";
        }
        public List<CPluginVariable> GetDisplayPluginVariables()
        {

            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            lstReturn.Add(new CPluginVariable("1 - Restore Squad Leaders|Restore after Scramble", RestoreSquads.GetType(), RestoreSquads));

            lstReturn.Add(new CPluginVariable("2 - Dismiss Idle Squad Leaders|Dismiss Idle Squad Leaders", RemoveIdleLeader.GetType(), RemoveIdleLeader));
            lstReturn.Add(new CPluginVariable("2 - Dismiss Idle Squad Leaders|Maximum Idle Time (seconds)", MaxIdleTime.GetType(), MaxIdleTime));

            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Dismiss Squad Leaders giving no orders (via Commo Rose)", RemoveNoOrdersLeader.GetType(), RemoveNoOrdersLeader));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Exclude Rush/Obliteraion/Defuse/CTF", ExcludeRush.GetType(), ExcludeRush));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Exclude Domination/ChainLink", ExcludeChainLink.GetType(), ExcludeChainLink));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Maximum waiting time for orders (minutes)", MaxWaiting.GetType(), MaxWaiting));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Send warnings before dismiss", SendWarnings.GetType(), SendWarnings));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|How many warnings", NoOrdersWarnings.GetType(), NoOrdersWarnings));
            lstReturn.Add(new CPluginVariable("3 - Dismiss No Orders Squad Leaders|Yell warnings", YellWarnings.GetType(), YellWarnings));

            lstReturn.Add(new CPluginVariable("4.1 - Squad Command Lead|Enforce Squad Lead [!lead]", Enforce.GetType(), Enforce));
            lstReturn.Add(new CPluginVariable("4.1 - Squad Command Lead|Use Reserved Slot List", UseReservedList.GetType(), UseReservedList));
            lstReturn.Add(new CPluginVariable("4.1 - Squad Command Lead|Use Squad Leader List", UseLeaderList.GetType(), UseLeaderList));
            lstReturn.Add(new CPluginVariable("4.1 - Squad Command Lead|Squad Leaders List", typeof(string[]), WhiteList.ToArray()));

            lstReturn.Add(new CPluginVariable("4.2 - Squad Command Vote|Allow Vote new Squad Leader [!newleader]", VoteDismiss.GetType(), VoteDismiss));
            lstReturn.Add(new CPluginVariable("4.2 - Squad Command Vote|Votes needed", VotesNeededDismiss.GetType(), VotesNeededDismiss));
            lstReturn.Add(new CPluginVariable("4.2 - Squad Command Vote|Vote duration (seconds)", VoteDuration.GetType(), VoteDuration));
            lstReturn.Add(new CPluginVariable("4.2 - Squad Command Vote|Yell vote announcement", YellVote.GetType(), YellVote));

            lstReturn.Add(new CPluginVariable("4.3 - Squad Command Invite|Allow invite players [!invite playername]", InviteCommand.GetType(), InviteCommand));
            lstReturn.Add(new CPluginVariable("4.3 - Squad Command Invite|Squad Leaders only", SquadLeadersOnly.GetType(), SquadLeadersOnly));
            lstReturn.Add(new CPluginVariable("4.3 - Squad Command Invite|Allow Team Switches", AllowTeamSwitches.GetType(), AllowTeamSwitches));
            lstReturn.Add(new CPluginVariable("4.3 - Squad Command Invite|Maximum invites per round", MaxInvites.GetType(), MaxInvites));
            lstReturn.Add(new CPluginVariable("4.3 - Squad Command Invite|Send invite messages how often?", HowManyInviteMessages.GetType(), HowManyInviteMessages));

            lstReturn.Add(new CPluginVariable("4.4 - Squad Command GiveLead|Allow to give someone else Squad Lead [!givelead playername]", MoveLead.GetType(), MoveLead));

            //lstReturn.Add(new CPluginVariable("4.4 - Squad Command Regroup|Allow to regroup Squads [!regroup playernameA playernameB ...]", Regroup.GetType(), Regroup));

            lstReturn.Add(new CPluginVariable("5 - Squad Unlock|Unlock all Squads", UnlockSquads.GetType(), UnlockSquads));

            lstReturn.Add(new CPluginVariable("6 - Dynamic Messages|Send messages how to use this plugin", WriteMessages.GetType(), WriteMessages));
            lstReturn.Add(new CPluginVariable("7 - Dynamic Messages|Interval (seconds)", Interval.GetType(), Interval));

            lstReturn.Add(new CPluginVariable("Debug Options|Debug level", fDebugLevel.GetType(), fDebugLevel));

            return lstReturn;

        }
        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }
        public void SetPluginVariable(string strVariable, string strValue)
        {
            //  -- 1 --
            if (Regex.Match(strVariable, @"Restore after Scramble").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                RestoreSquads = tmp;
            }

        //  -- 2 --
            else if (Regex.Match(strVariable, @"Maximum Idle Time \(seconds\)").Success)
            {
                int tmp = 30;
                int.TryParse(strValue, out tmp);
                MaxIdleTime = tmp;
            }
            else if (Regex.Match(strVariable, @"Dismiss Idle Squad Leaders").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                RemoveIdleLeader = tmp;
            }
            // -- 3 --

            else if (Regex.Match(strVariable, @"Dismiss Squad Leaders giving no orders \(via Commo Rose\)").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                RemoveNoOrdersLeader = tmp;
            }
            else if (Regex.Match(strVariable, @"Exclude Rush\/Obliteraion\/Defuse\/CTF").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                ExcludeRush = tmp;
            }
            else if (Regex.Match(strVariable, @"Exclude Domination\/ChainLink").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                ExcludeChainLink = tmp;
            }
            else if (Regex.Match(strVariable, @"Maximum waiting time for orders \(minutes\)").Success)
            {
                int tmp = 3;
                int.TryParse(strValue, out tmp);
                MaxWaiting = tmp;
            }
            else if (Regex.Match(strVariable, @"Yell warnings").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                YellWarnings = tmp;
            }
            else if (Regex.Match(strVariable, @"Send warnings before dismiss").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                SendWarnings = tmp;
            }
            else if (Regex.Match(strVariable, @"How many warnings").Success)
            {
                int tmp = 3;
                int.TryParse(strValue, out tmp);
                NoOrdersWarnings = tmp;
            }

        // -- 4 --
            else if (Regex.Match(strVariable, @"Use Squad Leader List").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                UseLeaderList = tmp;
            }
            else if (Regex.Match(strVariable, @"Squad Leaders List").Success)
            {
                WhiteList = new List<string>(CPluginVariable.DecodeStringArray(strValue));
            }
            else if (Regex.Match(strVariable, @"Use Reserved Slot List").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                UseReservedList = tmp;
            }
            else if (Regex.Match(strVariable, @"Enforce Squad Lead \[\!lead\]").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                Enforce = tmp;
            }

        // -- 5 ---
            else if (Regex.Match(strVariable, @"Allow Vote new Squad Leader \[\!newleader\]").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                VoteDismiss = tmp;
            }
            else if (Regex.Match(strVariable, @"Votes needed").Success)
            {
                int tmp = 3;
                int.TryParse(strValue, out tmp);
                VotesNeededDismiss = tmp;
            }
            else if (Regex.Match(strVariable, @"Vote duration \(seconds\)").Success)
            {
                int tmp = 30;
                int.TryParse(strValue, out tmp);
                VoteDuration = tmp;
            }
            else if (Regex.Match(strVariable, @"Yell vote announcement").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                YellVote = tmp;
            }
            // -- 6 --
            else if (Regex.Match(strVariable, @"Allow invite players \[\!invite playername\]").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                InviteCommand = tmp;
            }
            else if (Regex.Match(strVariable, @"Squad Leaders only").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                SquadLeadersOnly = tmp;
            }
            else if (Regex.Match(strVariable, @"Allow Team Switches").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                AllowTeamSwitches = tmp;
            }
            else if (Regex.Match(strVariable, @"Maximum Invites per Round").Success)
            {
                int tmp = 3;
                int.TryParse(strValue, out tmp);
                MaxInvites = tmp;
            }
            else if (Regex.Match(strVariable, @"Send invite messages how often?").Success)
            {
                int tmp = 3;
                int.TryParse(strValue, out tmp);
                HowManyInviteMessages = tmp;
            }
            else if (Regex.Match(strVariable, @"Allow to give someone else Squad Lead \[\!givelead playername\]").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                MoveLead = tmp;
            }
            else if (Regex.Match(strVariable, @"Allow to regroup Squads \[\!regroup playernameA playernameB ...\]").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                Regroup = tmp;
            }
            else if (Regex.Match(strVariable, @"Unlock all Squads").Success)
            {
                bool tmp = true;
                bool.TryParse(strValue, out tmp);
                UnlockSquads = tmp;
            }
            else if (Regex.Match(strVariable, @"Send messages how to use this plugin").Success)
            {
                bool tmp = false;
                bool.TryParse(strValue, out tmp);
                UnlockSquads = tmp;
            }
            else if (Regex.Match(strVariable, @"Interval \(seconds\)").Success)
            {
                int tmp = 120;
                int.TryParse(strValue, out tmp);
                Interval = tmp;
            }

        // -- 7 --
            else if (Regex.Match(strVariable, @"Debug level").Success)
            {
                int tmp = 2;
                int.TryParse(strValue, out tmp);
                fDebugLevel = tmp;
            }
        }
        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            this.RegisterEvents(this.GetType().Name, "OnVersion", "OnServerInfo", "OnResponseError", "OnListPlayers", "OnPlayerJoin", "OnPlayerLeft", "OnPlayerKilled", "OnPlayerSpawned", "OnGlobalChat", "OnTeamChat", "OnSquadChat", "OnRoundOverPlayers", "OnLevelLoaded", "OnPlayerSquadChange", "OnPlayerIdleDuration", "OnSquadLeader", "OnReservedSlotsList", "OnPlayerIsAlive", "OnPlayerMovedByAdmin", "OnPlayerTeamChange");
        }
        public void OnPluginEnable()
        {
            enabled = true;
            WaitingSquadList = true;
            WaitingSquadLeaders = true;
            BuildComplete = false;
            BuildCompleteMessageSent = false;
            SquadSwitchPossible = null;
            started = true;
            RestoreComplete = true;
            RestoreOnRoundStart = new List<String>();
            ListSquadInviters = new List<SquadInviter>();
            NewPlayersQueue = new List<NewPlayer>();
            JoinSwitchQueue.Clear();
            ConsoleWrite("Enabled!");

        }
        public void OnPluginDisable()
        {
            enabled = false;
            PluginIntervalTimer = null;
            WaitingSquadList = false;
            WaitingSquadLeaders = false;
            BuildComplete = false;
            BuildCompleteMessageSent = false;
            started = false;
            GameMode = String.Empty;
            SquadSwitchPossible = null;
            RoundTime = 0.0;
            ReservedSlotsReceived = null;
            started = true;
            bTimer = null;
            aTimer = null;
            cTimer = null;
            RestoreComplete = true;
            PlayersList = null;
            RestoreOnRoundStart = null;
            ListSquadInviters = null;
            NewPlayersQueue = null;
            Votes.Clear();
            squads.Clear();
            ReservedSlots.Clear();
            JoinSwitchQueue.Clear();
            ConsoleWrite("Disabled!");
        }

        public List<int> getTeamsIds(List<CPlayerInfo> players)
        {
            List<int> TeamIds = new List<int>();
            int PlayersTeamID;
            foreach (CPlayerInfo player in players)
            {
                if (player == null)
                    continue;

                PlayersTeamID = player.TeamID;
                if (PlayersTeamID < 1)
                    continue;
                if (!TeamIds.Contains(PlayersTeamID))
                    TeamIds.Add(PlayersTeamID);
            }
            return TeamIds;
        }
        public List<int> getSquadIds(List<CPlayerInfo> players)
        {
            List<int> SquadIds = new List<int>();
            int PlayersSquadID;
            foreach (CPlayerInfo player in players)
            {
                if (player == null)
                    continue;

                PlayersSquadID = player.SquadID;
                if (PlayersSquadID < 1)
                    continue;
                if (!SquadIds.Contains(PlayersSquadID))
                    SquadIds.Add(PlayersSquadID);
            }
            return SquadIds;
        }
        public String StripModifiers(String text)
        {
            return Regex.Replace(text, @"\^[0-9a-zA-Z]", "");
        }
        public String E(String text)
        {
            text = Regex.Replace(text, @"\\n", "\n");
            text = Regex.Replace(text, @"\\t", "\t");
            return text;
        }
        public List<String> StoreCurrentSquadsLeaders()
        {
            return squads.getSquadLeaders();
        }

        public void UnlockAllSquads()
        {
            foreach (Squad squad in squads.getSquads())
            {
                if (squad.getID(1) > 0 && squad.getMembers().Count > 1)
                    ServerCommand("squad.private", squad.getID(0).ToString(), squad.getID(1).ToString(), "false");
            }
        }
        public void RequestLeader(int TeamID, int SquadID)
        {
            if (TeamID < 1 || SquadID < 1)
                return;
            else
                ServerCommand("squad.leader", TeamID.ToString(), SquadID.ToString());
        }
        public void BuildSquadList(List<CPlayerInfo> players)
        {
            if (!enabled)
                return;

            DebugWrite("Build Squads list", 1);

            foreach (CPlayerInfo player in players)
            {
                if (player == null)
                    continue;
                if (String.IsNullOrEmpty(player.SoldierName))
                    continue;

                squads.AddPlayer(player.SoldierName, player.TeamID, player.SquadID);
                DebugWrite("Added player ^2" + player.SoldierName + "^n to Squad ^b[" + player.TeamID + "][" + SQUAD_NAMES[player.SquadID] + "]^n", 3);
            }

            WaitingSquadList = false;

            bool SquadsComplete = true;

            foreach (Squad squad in squads.getSquads())
            {
                if (squad.getID(1) == 0)
                    continue;

                if (squad.getSize() > 1)
                {
                    squad.SetSquadLeader(String.Empty);
                    DebugWrite("Squad Leader ^bunknown^n. Request Squad Leader of Squad  ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n", 3);
                    ServerCommand("squad.leader", squad.getID(0).ToString(), squad.getID(1).ToString());
                    SquadsComplete = false;
                }
                else if (squad.getSize() == 1)
                {
                    DebugWrite("Squad Leader ^bknown^n. SquadLeader of Team/Squad " + "^b[" + squad.getID(0) + "]" + "[" + squad.getName() + "]^n --> " + "^b^2" + squad.GetSquadLeader() + "^n", 3);
                }

            }

            DebugWrite("All Squad Leaders requested.", 1);

            if (SquadsComplete)
            {
                DebugWrite("All Squad Leaders received.", 1);
                if (RestoreSquads)
                {
                    DebugWrite("Requesting Squad Leader restore.", 1);
                    RestoreSquadsLeaders();
                }
            }

        }
        /*public void AssignJoinedgPlayersAfterBuildSquadList()
        {
            if (!enabled)
                return;

            if (NewPlayersQueue.Count > 0)
            {
                string soldierName;
                int teamId;
                int squadId;

                foreach (NewPlayer Player in NewPlayersQueue)
                {
                    soldierName = Player.getSoldierName();
                    teamId = Player.getTeamID();
                    squadId = Player.getSquadID();
                    NewPlayersQueue.Remove(Player);

                    int[] ReuestLeader = squads.AddPlayer(soldierName, teamId, squadId);
                    RequestLeader(ReuestLeader[0], ReuestLeader[1]);
                }
            }
        }*/
        public void UpdateSquadLeaderIdleTime(List<CPlayerInfo> players)
        {
            if (!enabled)
                return;

            if (!RemoveIdleLeader)
                return;
            foreach (CPlayerInfo player in players)
            {
                if (player == null)
                    continue;

                if (player.SoldierName == String.Empty || player.TeamID == -1 || player.SquadID == -1)
                    continue;
                Squad squad = squads.SearchSquad(player.TeamID, player.SquadID);

                if (squad == null)
                    continue;

                if (squad.getID(1) < 1)
                    continue;

                if (squad.GetSquadLeader() == player.SoldierName && squad.getSize() > 1)
                {

                    if (squad.getLeaderIdleTimeLastUpdateSeconds() > 30)
                    {
                        DebugWrite("Need to request ^b" + player.SoldierName + "^n's IdleDuration", 3);
                        ServerCommand("player.idleDuration", squad.GetSquadLeader().ToString());
                    }

                }
            }
        }
        public void RemoveIdleSquadLeaders()
        {
            if (!enabled)
                return;

            foreach (Squad squad in squads.getSquads())
            {
                String NewLeader = null;
                if (squad.getLeaderIdleTimeSeconds() > MaxIdleTime)
                {
                    if (UseLeaderList)
                    {
                        foreach (String soldier in WhiteList)
                        {
                            if (squad.isMember(soldier) && soldier != squad.GetSquadLeader())
                                NewLeader = soldier;
                        }
                    }
                    if (UseReservedList && NewLeader == null)
                    {
                        if (ReservedSlotsReceived == true)
                            foreach (String soldier in ReservedSlots)
                            {
                                if (squad.isMember(soldier) && soldier != squad.GetSquadLeader())
                                {
                                    NewLeader = soldier;
                                }

                            }
                    }
                    if (squad.getSize() > 1 && NewLeader == null)
                    {
                        NewLeader = squad.GetNoSquadLeader();
                    }


                    if (NewLeader != null)
                    {
                        DebugWrite("^2" + squad.GetSquadLeader() + "^n as SquadLeader of Team/Squad " + "^b[" + squad.getID(0) + "]^n" + "^b[" + squad.getName() + "]^n has been dismissed due to inactivity.^n [" + squad.getLeaderIdleTimeSeconds() + "] seconds", 3);

                        foreach (String member in squad.getMembers())
                        {
                            if (member == squad.GetSquadLeader())
                                ServerCommand("admin.say", "You've been removed as Squad Leader due to your inactivity.", "player", member);
                            else
                                ServerCommand("admin.say", "Your Squad Leader " + squad.GetSquadLeader() + " has been replaced due to inactivity. New Squad Leader: " + NewLeader, "player", member);
                        }

                        ServerCommand("squad.leader", squad.getID(0).ToString(), squad.getID(1).ToString(), NewLeader);

                    }
                }
            }
        }
        public void RemoveNoOrderSquadLeader()
        {
            if (!enabled)
                return;

            if (GameMode == String.Empty)
                return;

            if (GameMode == "TeamDeathMatch0" || GameMode == "AirSuperiority0" || GameMode == "TeamDeathMatch0")
                return;

            if (ExcludeRush && (GameMode == "RushLarge0" || GameMode == "Elimination0" || GameMode == "Obliteration"))
                return;

            if (ExcludeChainLink && (GameMode == "Domination0" || GameMode == "Chainlink0"))
                return;

            foreach (Squad squad in squads.getSquads())
            {
                if (squad.getSize() < 2 || squad.getID(1) < 1)
                    continue;


                if (squad.getTimeLastOrders() > MaxWaiting * 60)
                {
                    if (SendWarnings && NoOrdersWarnings > squad.getOrderWarnings())
                    {
                        squad.setOrderWarnings();
                        {
                            DebugWrite("admin.say," + squad.GetSquadLeader() + " you are Squad Leader! Please give some orders (via Commo Rose) or you're losing your leadership." + "player" + squad.GetSquadLeader(), 3);
                            ServerCommand("admin.say", squad.GetSquadLeader() + ", you're Squad Leader! Please give some orders (via Commo Rose) or you're losing your leadership.", "player", squad.GetSquadLeader());
                            if (YellWarnings)
                            {
                                ServerCommand("admin.yell", squad.GetSquadLeader() + ", you are Squad Leader! Please give some orders (via Commo Rose) or you're losing your leadership.", "15", "player", squad.GetSquadLeader());
                                DebugWrite("admin.yell" + squad.GetSquadLeader() + ", you are Squad Leader! Please give some orders (via Commo Rose) or you're losing your leadership." + "15" + "player" + squad.GetSquadLeader(), 3);

                            }
                        }
                    }
                    else
                    {
                        String NewLeader = squad.GetNoSquadLeader();

                        foreach (String member in squad.getMembers())
                        {
                            if (member == squad.GetSquadLeader())
                            {
                                DebugWrite("admin.say" + "You've been removed as Squad Leader because you gave not enough orders." + "player" + member, 3);
                                ServerCommand("admin.say", "You've been removed as Squad Leader because you gave not enough orders.", "player", member);

                            }

                            else
                            {

                                ServerCommand("admin.say", "Your Squad Leader " + squad.GetSquadLeader() + " has been replaced because not enough orders were given. New Squad Leader: " + NewLeader, "player", member);
                                DebugWrite("admin.say" + "Your Squad Leader " + squad.GetSquadLeader() + " has been replaced because not enough orders were given. New Squad Leader: " + NewLeader + "^n" + "player" + member, 3);

                            }
                        }
                        ServerCommand("squad.leader", squad.getID(0).ToString(), squad.getID(1).ToString(), NewLeader);
                        DebugWrite("squad.leader" + squad.getID(0).ToString() + squad.getName().ToString() + NewLeader, 3);

                    }
                }
            }
        }
        public void CheckVoteResults()
        {
            if (!enabled)
                return;

            foreach (Vote Vote in Votes)
            {
                if (Vote.getVoteResult() == 4 && !Vote.getResultSent())
                {
                    Vote.setResultSent(true);

                    DebugWrite("Vote in Sqaud/Team ^b[" + Vote.getVoteID(0) + "][" + Vote.getVoteID(1) + "]^n has been canceled since Squad Leader has changed.", 3);

                    foreach (Squad squad in squads.getSquads())
                    {
                        if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                        {
                            foreach (String member in squad.getMembers())
                            {
                                if (member == Vote.getVoteInitiator())
                                {
                                    ServerCommand("admin.say", "Your Squad Leader vote has been canceled since Squad Leader has changed.", "player", member);
                                    DebugWrite("admin.say Your Squad Leader vote has been canceled since Squad Leader has changed player " + member, 3);
                                }

                                else
                                {
                                    ServerCommand("admin.say", Vote.getVoteInitiator() + "'s Squad Leader vote has been canceled since Squad Leader has changed.", "player", member);
                                    DebugWrite("admin.say " + Vote.getVoteInitiator() + "'s Squad Leader vote has been canceled since Squad Leader has changed." + member, 3);
                                }
                            }

                            break;
                        }
                    }

                    return;

                }

                if (Vote.getVoteResult() == 1)
                {
                    DebugWrite("Vote in Sqaud/Team ^b[" + Vote.getVoteID(0) + "][" + Vote.getVoteID(1) + "]^n is still in progress.", 3);

                    foreach (Squad squad in squads.getSquads())
                    {
                        if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                        {
                            foreach (String member in squad.getMembers())
                            {
                                if (member == Vote.getVoteInitiator())
                                {
                                    ServerCommand("admin.say", "Your Squad Leader vote is in progress. Waiting for results", "player", member);
                                    DebugWrite("admin.say Your Squad Leader vote is in progress. Waiting for results " + member, 3);
                                }

                                else
                                {
                                    ServerCommand("admin.say", Vote.getVoteInitiator() + " wants to be your new Squad Leader. Type '!accept' in chat to give him leadership.", "player", member);
                                    DebugWrite("admin.say " + Vote.getVoteInitiator() + " wants to be your new Squad Leader. Type '!accept' in chat to give him leadership. " + member, 3);
                                }
                            }

                            break;
                        }

                    }

                    return;
                }

                else if (Vote.getVoteResult() == 2 && !Vote.getResultSent())
                {
                    DebugWrite("Vote in Sqaud/Team ^b[" + Vote.getVoteID(0) + "][" + Vote.getVoteID(1) + "]^n was successful. New Squad Leader will be ^b" + Vote.getVoteInitiator() + "^n", 3);
                    ServerCommand("squad.leader", Vote.getVoteID(0).ToString(), Vote.getVoteID(1).ToString(), Vote.getVoteInitiator());
                    Vote.setResultSent(true);
                    foreach (Squad squad in squads.getSquads())
                    {
                        if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                        {
                            foreach (String member in squad.getMembers())
                            {
                                if (member == Vote.getVoteInitiator())
                                {
                                    ServerCommand("admin.say", "Your Squad Leader vote was successful. You are the new Squad Leader", "player", member);
                                    DebugWrite("admin.say Your Squad Leader vote was successful. You are the new Squad Leader " + member, 3);
                                }

                                else
                                {
                                    ServerCommand("admin.say", "Squad Leader vote was successfull. New Squad Leader is " + Vote.getVoteInitiator(), "player", member);
                                    DebugWrite("admin.say Squad Leader vote was successfull. New Squad Leader is " + Vote.getVoteInitiator() + " " + member, 3);
                                }
                            }

                            break;
                        }
                    }
                }

                else if (Vote.getVoteResult() == 3 && !Vote.getResultSent())
                {
                    DebugWrite("Vote in Sqaud/Team ^b[" + Vote.getVoteID(0) + "][" + Vote.getVoteID(1) + "]^n was NOT successful.", 3);
                    Vote.setResultSent(true);
                    foreach (Squad squad in squads.getSquads())
                    {
                        if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                        {
                            foreach (String member in squad.getMembers())
                            {
                                if (member == Vote.getVoteInitiator())
                                {
                                    ServerCommand("admin.say", "Your Squad Leader vote was NOT successful.", "player", member);
                                    DebugWrite("admin.say Your Squad Leader vote was NOT successful." + member, 3);
                                }
                                else
                                {
                                    ServerCommand("admin.say", "Squad Leader vote was NOT successfull.", "player", member);
                                    DebugWrite("admin.say Squad Leader vote was NOT successfull." + member, 3);
                                }
                            }

                            break;
                        }
                    }
                }
            }
        }
        public void SpawnPossible(object source, ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            bTimer.Enabled = false;
            bTimer.Stop();
            bTimer = null;
            DebugWrite("Round has started.", 1);
            WaitingSquadList = true;
            WaitingSquadLeaders = true;
            BuildComplete = false;
            BuildCompleteMessageSent = false;
            squads = null;
            Votes = null;
            Votes = new List<Vote>();
            squads = new Squads();
            SquadSwitchPossible = true;
            PlayersList = null;
            ServerCommand("listPlayers", "all");

        }
        public void RestoreSquadsLeaders()
        {
            if (!enabled)
                return;

            if (RestoreOnRoundStart == null)
                return;

            if (RestoreOnRoundStart.Count == 0)
                return;

            DebugWrite("Requesting Squad Leader restore.", 2);

            Squad RestoreSquadLeader;
            foreach (String SquadLeader in RestoreOnRoundStart)
            {
                DebugWrite("Found old Squad Leader ^b" + SquadLeader + "^n", 3);
                RestoreSquadLeader = squads.SearchSquad(SquadLeader);
                if (RestoreSquadLeader == null)
                    continue;
                if (RestoreSquadLeader.GetSquadLeader() != SquadLeader)
                    ServerCommand("squad.leader", RestoreSquadLeader.getID(0).ToString(), RestoreSquadLeader.getID(1).ToString(), SquadLeader);
            }
            RestoreComplete = true;
            RestoreOnRoundStart.Clear();
        }
        public void OnSquadInvite(String message, String speaker, Match cmd)
        {
            if (!enabled)
                return;

            if (PlayersList == null || !BuildComplete)
                return;

            Squad SpeakerSquad = squads.SearchSquad(speaker);

            if (SpeakerSquad == null)
            {
                ServerCommand("admin.say", "Only Squad members can send Squad invites.", "player", speaker);
                DebugWrite(speaker + "--> Only Squad members can send Squad invites.", 3);
                return;
            }

            if (SquadLeadersOnly && SpeakerSquad.GetSquadLeader() != speaker)
            {
                ServerCommand("admin.say", "Only squad leaders are allowed to send squad invites", "player", speaker);
                DebugWrite(speaker + "--> is not allowed to send Squad invites.", 3);
                return;
            }

            foreach (SquadInviter Inviter in ListSquadInviters)
            {
                if (Inviter.getInviter() != speaker)
                    continue;

                if (Inviter.getRoundInvites() >= MaxInvites)
                {
                    ServerCommand("admin.say", "You can't send any more invites this round.", "player", speaker);
                    DebugWrite(speaker + "-->  can't send any more invites this round", 3);
                    return;
                }
            }

            int found = 0;
            String name = cmd.Groups[1].Value;
            CPlayerInfo target = null;
            foreach (CPlayerInfo p in PlayersList)
            {
                if (p == null)
                    continue;

                if (Regex.Match(p.SoldierName, name, RegexOptions.IgnoreCase).Success)
                {
                    ++found;
                    target = p;
                }
            }

            if (found == 0)
            {
                ServerCommand("admin.say", "No such player name matches (" + name + ")", "player", speaker);
                DebugWrite("admin.say No such player name matches (" + name + ") " + "player " + speaker, 4);
                return;
            }
            if (found > 1)
            {
                ServerCommand("admin.say", "Multiple players match the target name (" + name + "), try again!", "player", speaker);
                DebugWrite("admin.say Multiple players match the target name (" + name + "), try again!  player " + speaker, 4);
                return;
            }
            if (target.SoldierName == speaker)
            {
                ServerCommand("admin.say", "You can't invite yourself.", "player", speaker);
                DebugWrite("admin.say You can't invite yourself. player " + speaker, 4);
                return;
            }
            if (!AllowTeamSwitches && target.TeamID != SpeakerSquad.getID(0))
            {
                ServerCommand("admin.say", "(" + target.SoldierName + ") is not in the same team as you.", "player", speaker);
                DebugWrite("admin.say (" + target.SoldierName + ") is not in the same team as you. player " + speaker, 4);
                return;
            }

            foreach (SquadInviter Inviter in ListSquadInviters)
            {
                if (Inviter.getInviter() != speaker)
                    continue;

                if (Inviter.getInvitees().Contains(target.SoldierName))
                {
                    ServerCommand("admin.say", "You already sent an invite to " + target.SoldierName + " this round.", "player", speaker);
                    DebugWrite("admin.say You already sent an invite to " + target.SoldierName + " this round. player" + speaker, 4);
                    return;
                }
            }

            SquadInviter NewInvite = new SquadInviter(speaker);
            NewInvite.SendInvite(target.SoldierName);
            ListSquadInviters.Add(NewInvite);

            ServerCommand("player.isAlive", target.SoldierName);

            if (fDebugLevel > 1)
            {
                foreach (SquadInviter entry in ListSquadInviters)
                {
                    if (entry.getInviter() == speaker)
                    {
                        ServerCommand("admin.say", "Your invite has been successfully sent to " + target.SoldierName + " Player will see this message on next death.", "player", speaker);
                        DebugWrite("admin.say Your invite has been successfully sent to " + target.SoldierName + "player" + speaker, 3);
                    }

                }
            }


            return;
        }
        public void OnReGroup(String message, String speaker, Match cmd, int groupsize)
        {
            //Save all playernames
            String[] playernames = new String[groupsize];
            for (int i = 0; i < groupsize; i++)
            {
                playernames[i] = cmd.Groups[i + 1].Value;
                DebugWrite("playernames[i] " + playernames[i], 1);
            }
        }

        public void PerformJoinSwitchQueue()
        {
            PerformJoinSwitchQueue(String.Empty);
        }

        public void PerformJoinSwitchQueue(String soldiername)
        {
            if (!enabled)
                return;

            DebugWrite("CurrentPlayers: " + CurrentPlayers + " ServerSize " + ServerSize, 4);

            if (ServerSize == 0 || GameMode == String.Empty || PlayersList == null || !BuildComplete || CurrentPlayers == 0)
                return;

            int TeamDestination, SquadDestination, TeamOrigin, SquadOrigin;
            bool force;
            string target;
            SquadInviter inviter;

            int MaxTeamsize;

            if (GameMode == "SquadDeathMatch0")
                MaxTeamsize = ServerSize/4; 
            else
                MaxTeamsize = ServerSize/2;

            foreach (List<object> entry in JoinSwitchQueue)
            {
                TeamOrigin = (int)entry[0];
                TeamDestination = (int)entry[1];
                SquadOrigin = (int)entry[2];
                SquadDestination = (int)entry[3];
                force = (bool)entry[4];
                target = (string)entry[5];
                inviter = (SquadInviter)entry[6];

                if (SquadDestination == 0)
                {
                    ServerCommand("admin.say", inviter + " has left the Squad. Waiting for new Squad.", "player", target);
                    DebugWrite("admin.say " + inviter + " has left the Squad. Waiting for new Squad.", 3);
                    continue;
                }

                if (TeamDestination == 0)
                {
                    ServerCommand("admin.say", inviter + " has left the server. Invite canceled.", "player", target);
                    DebugWrite("admin.say " + inviter + " has left the server. Invite canceled.", 3);
                    JoinSwitchQueue.Remove(entry);
                    continue;
                }


                DebugWrite("MaxTeamsize " + MaxTeamsize + " CurrentPlayersTeams[TeamDestination]" + CurrentPlayersTeams[TeamDestination - 1], 3);

                Squad DestinationSquad = squads.SearchSquad(TeamDestination, SquadDestination);
                if (DestinationSquad == null)
                {
                    JoinSwitchQueue.Remove(entry);
                    ServerCommand("admin.say", "Invite canceled. Squad doesn't exist anymore.", "player", target);
                    DebugWrite("admin.say Player " + target + " has accepted your invite. player " + inviter + " but Squad was closed.", 3);
                    inviter.SendMessageTo(target, int.MaxValue);
                    continue;
                }

                // Team Switch
                if (TeamDestination != TeamOrigin)
                {
                    if (CurrentPlayersTeams[TeamDestination - 1] == MaxTeamsize)
                    {
                        DebugWrite("Team ^b[" + TeamDestination + "^n is currently full. Waiting for someone is leaving the team", 3);
                        ServerCommand("admin.say", "Team [" + TeamDestination + "] is currently full. Waiting for someone is leaving the team", "player", target);
                        return;
                    }
                    else
                    {
                        DebugWrite("Switching player ^b" + target + "^n to Team ^b[" + TeamDestination + "]^n.", 3);
                        ServerCommand("admin.say", "Switching you to Team [" + TeamDestination + "].", "player", target);
                        ServerCommand("admin.movePlayer", target, TeamDestination.ToString(), "0", force.ToString());

                        // admin.movePlayer event might be not fast enough
                        /*CurrentPlayersTeams[SquadOrigin]--;
                        CurrentPlayersTeams[TeamDestination]++;
                
                        Squad OriginSquad = squads.SearchSquad(TeamOrigin, SquadOrigin);
                        int[] RequestL = OriginSquad.RemPlayer(target);
                        RequestLeader(RequestL[0], RequestL[1]);

                        AdminMovesQueue.Add(TeamDestination);
                        AdminMovesQueue.Add(0);
                        AdminMovesQueue.Add(target);*/

                        entry[0] = TeamDestination;

                        // return and wait for admin.movePlayer has been completed.
                        return;
                        // TEAM SWITCH COMPLETE
                    }
                }

                // Squad Switch 
                if (DestinationSquad.getMembers().Count > 4)
                {
                    DebugWrite("DestinationSquad.getMembers().Count: " + DestinationSquad.getMembers().Count + " Squad ^b[" + DestinationSquad.getID(0) + "][" + DestinationSquad.getName() + "]^n is currently full. Waiting for someone leaves the squad", 3);
                    ServerCommand("admin.say", "Squad [" + DestinationSquad.getID(0) + "][" + DestinationSquad.getName() + " is currently full. Waiting for someone leaves the squad", "player", target);
                    return;
                }
                else
                {
                    DebugWrite("Switching player ^b" + target + "^n to Squad ^b[" + DestinationSquad.getID(0) + "][" + DestinationSquad.getName() + "^n.", 3);
                    ServerCommand("admin.say", "Switching you to Squad [" + SQUAD_NAMES[SquadDestination] + "].", "player", target);
                    ServerCommand("admin.movePlayer", target, TeamDestination.ToString(), SquadDestination.ToString(), force.ToString());

                    /*AdminMovesQueue.Add(TeamDestination);
                    AdminMovesQueue.Add(SquadDestination);
                    AdminMovesQueue.Add(target);*/

                    JoinSwitchQueue.Remove(entry);
                    return;
                }
            }
        }
        public void PerformSwitchQueueBeforeScramble(object source, ElapsedEventArgs e)
        {
            if (!enabled)
                return;
            if (cTimer == null)
                return;

            cTimer.Enabled = false;
            cTimer.Stop();
            cTimer = null;
            DebugWrite("PerformJoinSwitchQueue()", 4);
            PerformJoinSwitchQueue();
            ListSquadInviters.Clear();


        }
        public void AddJoinSwitch(String Invitee, SquadInviter Inviter)
        {
            if (!enabled)
                return;

            ServerCommand("admin.say", "Player " + Invitee + " has accepted your invite.", "player", Inviter.getInviter());
            ServerCommand("admin.say", "You have accepted " + Inviter.getInviter() + "\'s invite. Server is switching you as soon as possible", "player", Invitee);
            DebugWrite("admin.say Player " + Invitee + " has accepted your invite. player " + Inviter.getInviter(), 3);
            Inviter.SendMessageTo(Invitee, int.MaxValue);

            // TeamOrigin, TeamDestination, SquadOrigin, SquadDestion, force, target
            Squad InviteeS = squads.SearchSquad(Invitee);
            Squad InviterS = squads.SearchSquad(Inviter.getInviter());


            int TeamOrigin = 0;
            int TeamDestination = 0;
            int SquadOrigin = 0;
            int SquadDestination = 0;


            if (InviterS == null)
            {
                foreach (String InviteeEntry in Inviter.getInvitees())
                {
                    ServerCommand("admin.say", "Invite canceled. Squad doesn't exist anymore.", "player", InviteeEntry);
                    DebugWrite("admin.say Player " + Invitee + " has accepted your invite. player " + Inviter.getInviter() + " but Squad was closed.", 3);
                    Inviter.SendMessageTo(InviteeEntry, int.MaxValue);
                }
                return;
            }

            if (InviteeS == null)
            {
                TeamOrigin = 0;
                SquadOrigin = 0;
            }
            else
            {
                TeamOrigin = InviteeS.getID(0);
                TeamDestination = InviterS.getID(0);
                SquadOrigin = InviteeS.getID(1);
                SquadDestination = InviterS.getID(1);

            }

            bool force = true;
            string target = Invitee;
            SquadInviter inviter = Inviter;

            if (TeamDestination == 0 || SquadDestination == 0)
                return;

            List<object> JoinSwitchQueueEntry = new List<object>();
            JoinSwitchQueueEntry.Add(TeamOrigin);
            JoinSwitchQueueEntry.Add(TeamDestination);
            JoinSwitchQueueEntry.Add(SquadOrigin);
            JoinSwitchQueueEntry.Add(SquadDestination);
            JoinSwitchQueueEntry.Add(force);
            JoinSwitchQueueEntry.Add(target);
            JoinSwitchQueueEntry.Add(inviter);

            JoinSwitchQueue.Add(JoinSwitchQueueEntry);

        }
        public void UpdateJoinSwitch(String player, int NewDestinationTeam, int NewDestinationSquad)
        {
            if (!enabled)
                return;

            /*if (NewDestinationTeam < 1 || NewDestinationSquad < 1)
                return;*/

            int TeamOrigin = -1;
            int TeamDestination = -1;
            int SquadOrigin = -1;
            int SquadDestination = -1;
            bool DelEntry = false;


            foreach (SquadInviter Inviter in ListSquadInviters)
            {
                if (Inviter.getInviter() == player)
                {
                    Squad InviterS = squads.SearchSquad(Inviter.getInviter());

                    if (InviterS == null)
                    {
                        foreach (String Invitee in Inviter.getInvitees())
                        {
                            ServerCommand("admin.say", "Invite canceled. Squad doesn't exist anymore.", "player", Invitee);
                            DebugWrite("admin.say Player " + Invitee + " has accepted your invite. player " + Inviter.getInviter() + " but Squad was closed.", 3);
                            Inviter.SendMessageTo(Invitee, int.MaxValue);
                        }
                        DelEntry = true;
                    }
                    else
                    {
                        TeamDestination = InviterS.getID(0);
                        SquadDestination = InviterS.getID(1);
                    }

                    break;
                }
                else
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        if (Invitee == player)
                        {
                            Squad InviteeS = squads.SearchSquad(Invitee);

                            if (InviteeS == null)
                            {
                                TeamOrigin = 0;
                                SquadOrigin = 0;
                            }
                            else
                            {

                                TeamOrigin = InviteeS.getID(0);
                                SquadOrigin = InviteeS.getID(1);
                            }

                            break;
                        }
                    }
                }
            }

            if (TeamDestination == -1 || TeamOrigin == -1)
                return;

            SquadInviter WhichInviter;
            string WhichInvitee;

            foreach (List<object> JoinSwitchQueueEntry in JoinSwitchQueue)
            {

                WhichInviter = (SquadInviter)JoinSwitchQueueEntry[6];
                WhichInvitee = (string)JoinSwitchQueueEntry[5];

                if (WhichInvitee == player)
                {
                    JoinSwitchQueueEntry[0] = TeamOrigin;
                    JoinSwitchQueueEntry[2] = SquadOrigin;
                    DebugWrite("Invitee " + player + " has changed to Team/Squad ^b[" + TeamOrigin + "][" + SquadOrigin + "]^n", 3);
                    if (DelEntry)
                    {
                        JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                        DebugWrite("Squad null ---> delete SwitchQueueEntry", 4);
                    }
                }
                else if (WhichInviter.getInviter() == player)
                {
                    JoinSwitchQueueEntry[1] = TeamDestination;
                    JoinSwitchQueueEntry[2] = SquadDestination;
                    DebugWrite("Inviter " + player + " has changed to Team/Squad ^b[" + TeamDestination + "][" + SquadDestination + "]^n", 3);
                    if (DelEntry)
                    {
                        JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                        DebugWrite("Squad null ---> delete SwitchQueueEntry", 4);
                    }
                }
            }
        }
        public void RemoveJoinSwitch(String player)
        {
            if (!enabled)
                return;

            if (player == String.Empty)
                return;

            foreach (SquadInviter Inviter in ListSquadInviters)
            {
                if (Inviter.getInviter() == player)
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        ServerCommand("admin.say", "Invite canceled. Inviter " + Inviter.getInviter() + " has left the squad or the server", "player", Invitee);
                        DebugWrite("admin.say Invite canceled. Inviter " + Inviter.getInviter() + "has left the server.", 3);
                        // Inviter left --> Dont reset vote to prevent vote spam while reconnecting to the server all the time
                        Inviter.SendMessageTo(Invitee, int.MaxValue);
                    }

                }
                else
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        if (Invitee == player)
                        {

                            ServerCommand("admin.say", "Invite canceled. Invitee " + Invitee + " has left the squad or the server", "player", Inviter.getInviter());
                            DebugWrite("admin.say Invite canceled. Invitee " + Invitee + "  has left the server.", 3);
                            Inviter.RemoveInvite(Invitee);

                        }
                    }
                }
            }

            SquadInviter WhichInviter;
            string WhichInvitee;

            foreach (List<object> JoinSwitchQueueEntry in JoinSwitchQueue)
            {
                WhichInviter = (SquadInviter)JoinSwitchQueueEntry[6];
                WhichInvitee = (string)JoinSwitchQueueEntry[5];

                if (WhichInvitee == player || WhichInviter.getInviter() == player)
                {
                    JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                    return;
                }
            }
        }
        public void RebuildJoinSwitch()
        {
            if (!BuildComplete || PlayersList == null)
                return;

            DebugWrite("RebuildJoinSwitch()", 4);

            SquadInviter WhichInviter;
            string WhichInvitee;

            int TeamOrigin = -1;
            int SquadOrigin = -1;
            bool WhichInviterFound;
            bool WhichInviteeFound;

            foreach (List<object> JoinSwitchQueueEntry in JoinSwitchQueue)
            {
                WhichInviterFound = false;
                WhichInviteeFound = false;

                WhichInviter = (SquadInviter)JoinSwitchQueueEntry[6];
                WhichInvitee = (string)JoinSwitchQueueEntry[5];

                foreach (CPlayerInfo player in PlayersList)
                {
                    if (player.SoldierName == WhichInvitee)
                    {
                        WhichInviteeFound = true;
                        TeamOrigin = player.TeamID;
                        SquadOrigin = player.SquadID;
                    }

                    if (player.SoldierName == WhichInviter.getInviter())
                    {
                        WhichInviterFound = true;
                    }

                }

                WhichInviter = (SquadInviter)JoinSwitchQueueEntry[6];
                WhichInvitee = (string)JoinSwitchQueueEntry[5];

                if (!WhichInviterFound && !WhichInviteeFound)
                {
                    DebugWrite("admin.say Invite canceled. Inviter " + WhichInviter + " and " + WhichInvitee + " have left the server.", 3);
                    JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                    continue;
                }


                if (!WhichInviteeFound)
                {
                    ServerCommand("admin.say", "Invite canceled. Invitee " + WhichInvitee + " has left the server", "player", WhichInviter.getInviter());
                    DebugWrite("admin.say Invite canceled. Inviter " + WhichInvitee + "has left the server.", 3);
                    JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                    continue;
                }

                if (!WhichInviterFound)
                {
                    ServerCommand("admin.say", "Invite canceled. Inviter " + WhichInviter.getInviter() + " has left the server", "player", WhichInvitee);
                    DebugWrite("admin.say Invite canceled. Inviter " + WhichInviter + "has left the server.", 3);
                    JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                    continue;
                }


                Squad InviterS = squads.SearchSquad(WhichInviter.getInviter());

                if (InviterS == null)
                {
                    ServerCommand("admin.say", "Invite canceled. Inviter " + WhichInviter.getInviter() + " is in no Squad", "player", WhichInvitee);
                    DebugWrite("admin.say Invite canceled. Inviter " + WhichInviter.getInviter() + " is in no Squad.", 3);
                    JoinSwitchQueue.Remove(JoinSwitchQueueEntry);
                    continue;
                }


                Squad InviteeS = squads.SearchSquad(WhichInvitee);

                if (InviteeS == null)
                {
                    JoinSwitchQueueEntry[0] = TeamOrigin;
                    JoinSwitchQueueEntry[1] = InviterS.getID(0);
                    JoinSwitchQueueEntry[2] = SquadOrigin;
                    JoinSwitchQueueEntry[3] = InviterS.getID(1);
                }
            }
        }
        public void OnIntervalMessages(object source, ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            ServerCommand("admin.say", Messages[MessageCounter], "all");
            DebugWrite(Messages[MessageCounter], 4);

            MessageCounter = MessageCounter + 1;
            if (MessageCounter >= Messages.Count)
                MessageCounter = 0;


        }
        public void UpdateMessages()
        {
            if (WriteMessages)
            {

                Messages.Clear();

                String msg = String.Empty;
                String RemoveIdle = String.Empty;
                String RemoveNoOrders = String.Empty;
                String AutoLead = String.Empty;
                String SquadVote = String.Empty;
                String SquadInvite = String.Empty;
                String MoveLeadMsg = String.Empty;
                String UnlockMsg = String.Empty;

                msg = "Squad Manager Plugin is running on this server.";
                Messages.Add(msg);

                if (RemoveIdleLeader)
                {
                    RemoveIdle = "Idle Squad Leaders will be dismissed after " + MaxIdleTime.ToString() + " seconds inactivity.";
                    Messages.Add(RemoveIdle);
                }


                if (RemoveNoOrdersLeader)
                {
                    RemoveNoOrders = "Please give some Squad Orders (via Commo Rose) or you will be automatically removed as Squad Leader.";
                    Messages.Add(RemoveNoOrders);
                }


                if (Enforce)
                {
                    if (UseLeaderList)
                    {
                        AutoLead = "Every player having a Reserved Slot can use !lead command to get Squad Leader.";
                        Messages.Add(AutoLead);
                    }

                }

                if (VoteDismiss)
                {
                    SquadVote = "Do you want a new Squad Leader? Type !newleader in Squad Chat to start a vote.";
                    Messages.Add(SquadVote);
                }


                if (AllowTeamSwitches)
                {
                    SquadInvite = "Do you want to play with friends in a Squad? Join a Squad and type !invite [playername] in chat.";
                    if (AllowTeamSwitches)
                        SquadInvite = SquadInvite + " This works even if your friend is in the enemy team.";
                    Messages.Add(SquadInvite);
                }

                if (MoveLead)
                {
                    MoveLeadMsg = "You can give up your Squad Lead with !givelead [playername] command.";
                    Messages.Add(MoveLeadMsg);
                }

                if (UnlockSquads)
                {
                    UnlockMsg = "Private Squads will be unlocked automatically.";
                    Messages.Add(UnlockMsg);
                }

                if (aTimer == null)
                    aTimer = new System.Timers.Timer();

                int IntervalMS = Interval * 1000;

                if (aTimer.Interval != IntervalMS)
                {
                    aTimer.Interval = IntervalMS;
                    aTimer = null;
                    aTimer = new System.Timers.Timer();
                }


                if (!aTimer.Enabled)
                {
                    aTimer.Start();
                    aTimer.Elapsed += new ElapsedEventHandler(OnIntervalMessages);
                    aTimer.Interval = IntervalMS;
                    aTimer.Enabled = true;
                }

            }
            else
            {
                aTimer = null;
                Messages.Clear();
            }
        }
        public void PluginInterval(object source, ElapsedEventArgs e)
        {
            if (!enabled)
                return;

            if (PlayersList == null)
                return;

            else if (BuildComplete && SquadSwitchPossible == true)
            {

                DebugWrite("Updating Player Information", 4);

                UpdateMessages();
                DebugWrite("UpdateMessages()", 4);


                DebugWrite("RoundTime: " + RoundTime + " seconds", 4);

                // Update Idle Times
                if (RemoveIdleLeader)
                {
                    DebugWrite("Remove Idle Leaders", 3);
                    UpdateSquadLeaderIdleTime(PlayersList);
                }

                // Remove Idle Squad Leaders
                if (RoundTime > 180.0 && RemoveIdleLeader)
                {
                    DebugWrite("Check Idle Times", 3);
                    RemoveIdleSquadLeaders();
                }


                // Check Vote Results
                if (VoteDismiss)
                {
                    DebugWrite("Check Vote Results", 3);
                    CheckVoteResults();
                }


                // Remove no order giving Squad Leaders
                if (RoundTime > 60 * 3 && RemoveNoOrdersLeader)
                {
                    DebugWrite("Check Squad Leaders Orders", 3);
                    RemoveNoOrderSquadLeader();
                }

                // Unlock all Squads
                if (UnlockSquads)
                {
                    DebugWrite("Unlock Squads", 3);
                    UnlockAllSquads();
                }

            }


        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (!enabled)
                return;

            //  Build Squads list
            if (WaitingSquadList && SquadSwitchPossible == true)
                BuildSquadList(players);

            PlayersList = players;

            int tmp = 0;
            int[] tmpT = new int[4];

            foreach (CPlayerInfo player in players)
            {
                if (player == null)
                    continue;

                tmp++;

                if (player.TeamID > 0)
                    tmpT[player.TeamID - 1]++;
            }

            CurrentPlayers = tmp;
            CurrentPlayersTeams = tmpT;

        }
        public override void OnVersion(string serverType, string version)
        {
        }
        public override void OnServerInfo(CServerInfo serverInfo)
        {
            if (SquadSwitchPossible == true)
            {
                GameMode = serverInfo.GameMode;
                RoundTime = serverInfo.RoundTime;
                ServerSize = serverInfo.MaxPlayerCount;
            }

        }
        public override void OnResponseError(List<string> requestWords, string error)
        {
        }
        public override void OnSquadLeader(int teamId, int squadId, string soldierName)
        {
            if (!enabled)
                return;

            if (WaitingSquadList)
                return;

            DebugWrite("SquadLeader of Team/Squad " + "^b[" + teamId + "]" + "[" + SQUAD_NAMES[squadId] + "]^n --> " + "^b^2" + soldierName + "^n", 1);

            ////////
            Squad SetLeader = squads.SearchSquad(teamId, squadId);
            if (SetLeader == null)
                return;
            SetLeader.SetSquadLeader(soldierName);
            ////////

            if (WaitingSquadLeaders)
            {
                String SquadLeader;
                foreach (Squad squad in squads.getSquads())
                {
                    if (squad.getID(1) < 1)
                        continue;
                    SquadLeader = squad.GetSquadLeader();
                    if (SquadLeader == String.Empty)
                    {
                        DebugWrite("Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n has no Squad Leader", 3);
                        WaitingSquadLeaders = true;
                        break;
                    }

                    WaitingSquadLeaders = false;
                }
            }

            if (!WaitingSquadLeaders)
            {
                // redundant
                BuildComplete = true;
            }

            if (!BuildCompleteMessageSent && BuildComplete)
            {
                BuildCompleteMessageSent = true;
                DebugWrite("All Squad Leaders received", 1);

                if (RestoreSquads)
                    RestoreSquadsLeaders();

                RebuildJoinSwitch();
            }

            foreach (Vote vote in Votes)
            {
                if (vote.getVoteInitiator() == SetLeader.GetSquadLeader())
                {
                    vote.setVoteCanceled(true);
                    CheckVoteResults();
                }
            }


        }
        public override void OnPlayerJoin(string soldierName)
        {
            if (!enabled)
                return;

            DebugWrite("^2" + soldierName + "^n has joined the server.", 4);


        }
        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {

            if (!enabled)
                return;

            if (playerInfo == null || PlayersList == null)
                return;

            PlayersList.Remove(playerInfo);

            CurrentPlayers--;

            if (playerInfo.TeamID > 0)
                CurrentPlayersTeams[playerInfo.TeamID - 1]--;

            if (String.IsNullOrEmpty(playerInfo.SoldierName) || playerInfo.TeamID < 0 || playerInfo.SquadID < 0)
                return;

            if (WaitingSquadList)
                return;

            // Remove players from SquadList
            Squad OldSquad = squads.SearchSquad(playerInfo);
            if (OldSquad != null)
                DebugWrite("^b" + playerInfo.SoldierName + "^n left the server from ^b[" + OldSquad.getID(0) + "][" + OldSquad.getName() + "]^n", 3);

            int[] RequestL = squads.RemPlayer(playerInfo.SoldierName);
            RequestLeader(RequestL[0], RequestL[1]);

            // Remove from Vote
            foreach (Vote Vote in Votes)
            {
                if (OldSquad.getID(0) == Vote.getVoteID(0) && OldSquad.getID(1) == Vote.getVoteID(1))
                    Vote.RemoveVote(playerInfo.SoldierName);
            }

            if (InviteCommand)
            {
                foreach (SquadInviter Inviter in ListSquadInviters)
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        if (Inviter.getInviter() == playerInfo.SoldierName || Invitee == playerInfo.SoldierName)
                        {
                            Inviter.SendMessageTo(Invitee, int.MaxValue);
                        }


                    }
                }
            }

            RemoveJoinSwitch(playerInfo.SoldierName);
            DebugWrite("OnPlayerJoin - Removed", 4);
            PerformJoinSwitchQueue(playerInfo.SoldierName);
            DebugWrite("OnPlayerJoin - PerformJoinSwitchQueue()", 4);

        }
        public override void OnPlayerTeamChange(string soldierName, int teamId, int squadId)
        {

            if (!enabled)
                return;

            Squad LookingForNewPlayer = squads.SearchSquad(soldierName);

            UpdateJoinSwitch(soldierName, teamId, squadId);


        }
        public override void OnPlayerSquadChange(string soldierName, int teamId, int squadId)
        {

            if (!enabled)
                return;

            // Detecting Squad Leaders leaving squad on RoundStart which means they will lose leadership
            if (SquadSwitchPossible == true && !RestoreComplete && RestoreSquads)
            {
                RestoreOnRoundStart.Remove(soldierName);
            }

            ////////
            if (WaitingSquadList)
                return;
            ///////

            if (VoteDismiss)
            {
                foreach (Vote vote in Votes)
                {
                    if (vote.getVoteInitiator() == soldierName && squadId != vote.getVoteID(1) && teamId != vote.getVoteID(0))
                        vote.setVoteCanceled(true);
                }
            }

            Squad OldSquad = squads.SearchSquad(soldierName);
            Squad NewSquad = squads.SearchSquad(teamId, squadId);

            if (fDebugLevel > 1)
            {
                if (OldSquad != null)
                {
                    DebugWrite("^b" + soldierName + "^n left Squad ^b[" + OldSquad.getID(0) + "][" + OldSquad.getName() + "]^n", 2);
                    DebugWrite("Old Squad SquadSize before leave " + OldSquad.getMembers().Count, 3);
                }

                if (NewSquad != null)
                {
                    DebugWrite("^b" + soldierName + "^n joined Squad ^b[" + NewSquad.getID(0) + "][" + NewSquad.getName() + "]^n - Compare ^b[" + teamId + "][" + SQUAD_NAMES[squadId] + "]^n", 2);
                    DebugWrite("New Squad SquadSize before leave " + NewSquad.getMembers().Count, 3);
                }
            }


            int[] RequestL = squads.AddPlayer(soldierName, teamId, squadId);
            RequestLeader(RequestL[0], RequestL[1]);

            if (OldSquad != null)
                DebugWrite("Old Squad SquadSize after leave " + OldSquad.getMembers().Count, 3);
            if (NewSquad != null)
            {
                DebugWrite("New Squad SquadSize after join " + NewSquad.getMembers().Count, 3);
                if (NewSquad.getSize() < 2 && NewSquad.getID(1) > 0)
                    DebugWrite("^2" + NewSquad.GetSquadLeader() + "^n is the first player in Squad/Team " + "^b[" + NewSquad.getID(0) + "][" + NewSquad.getName() + "]^n", 2);
            }

            foreach (List<object> entry in JoinSwitchQueue)
            {
                SquadInviter SquadInviter = (SquadInviter)entry[6];
                String Inviter = SquadInviter.getInviter();

                if (soldierName == Inviter)
                {
                    if (squadId == 0)
                    {
                        RemoveJoinSwitch(soldierName);
                        return;
                    }
                }
            }


            UpdateJoinSwitch(soldierName, teamId, squadId);
            DebugWrite("UpdateJoinSwitch() - OnSquadChange", 4);
            PerformJoinSwitchQueue(soldierName);
            DebugWrite("PerformJoinSwitchQueue() - OnSquadChange", 4);

        }
        public override void OnPlayerIdleDuration(string soldierName, int idleTime)
        {
            if (!enabled)
                return;

            Squad squad = squads.SearchSquad(soldierName);
            if (squad == null)
                return;

            if (squad.getID(1) < 1)
                return;

            if (squad.GetSquadLeader() == soldierName)
            {
                DebugWrite("Squad Leader ^b" + soldierName + "^n Idle Time: ^b[" + idleTime + "s]^n ", 3);
                squad.setLeaderIdle(idleTime);
            }

            if (idleTime > MaxIdleTime && RoundTime > 30.0 && RemoveIdleLeader)
                RemoveIdleSquadLeaders();

        }

        public bool OnDenyChat(string message, string speaker)
        {
            if (!InviteCommand)
                return false;

            if (message.Equals("!deny"))
            {
                foreach (SquadInviter Inviter in ListSquadInviters)
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        if (Invitee == speaker)
                        {
                            ServerCommand("admin.say", "Player " + Invitee + " has denied your invite.", "player", Inviter.getInviter());
                            ServerCommand("admin.say", "You have denied " + Inviter.getInviter() + "\'s invite.", "player", Invitee);
                            DebugWrite("admin.say Player " + Invitee + " has denied your invite. player " + Inviter.getInviter(), 4);
                            Inviter.SendMessageTo(Invitee, int.MaxValue);
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        public bool OnLeadChat(string message, string speaker)
        {

            if (!Enforce)
                return false;

            if (message.Equals("!lead"))
            {

                Squad squad = squads.SearchSquad(speaker);

                if (squad == null)
                {
                    ServerCommand("admin.say", "You're not a member of any squad.", "player", speaker);
                    DebugWrite("^b" + speaker + "^n has requested leadership. Player is not a member of any squad", 3);
                    return true;
                }


                if (!Enforce)
                {
                    ServerCommand("admin.say", "You're not allowed to lead this squad.", "player", speaker);
                    DebugWrite("^b" + speaker + "^n has requested leadership of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n. Player is not allowed. ^b'Enforce Squad Lead'^n is disabled.", 3);
                    return true;
                }

                if (squad.getID(1) < 1)
                    return true;

                if (squad.GetSquadLeader() == speaker)
                {
                    ServerCommand("admin.say", "You are already Squad Leader.", "player", speaker);
                    return true;
                }


                bool Permission = false;
                if (UseLeaderList)
                    if (WhiteList.Contains(speaker) && !WhiteList.Contains(squad.GetSquadLeader()))
                    {
                        DebugWrite("^b" + speaker + "^n ask for lead of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n. Player is in Squad List.", 3);
                        Permission = true;
                    }
                if (UseReservedList)
                    if (Permission == false && ReservedSlots.Contains(speaker) && !ReservedSlots.Contains(squad.GetSquadLeader()))
                    {
                        DebugWrite("^b" + speaker + "^n ask for lead of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n. Player has a Reserved Slot.", 3);
                        Permission = true;
                    }

                if (Permission)
                {
                    DebugWrite("^b" + speaker + "^n is the new of Squad Leader of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n.", 3);
                    ServerCommand("squad.leader", squad.getID(0).ToString(), squad.getID(1).ToString(), speaker);
                    foreach (String Member in squad.getMembers())
                    {
                        if (Member == speaker)
                            ServerCommand("admin.say", "You're the new Squad Leader.", "player", speaker);
                        else
                            ServerCommand("admin.say", speaker + " is the new Squad Leader of your Squad.", "player", Member);
                    }
                }

                else
                {
                    DebugWrite("^b" + speaker + "^n has requested leadership of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n. Player is not allowed.", 3);
                    ServerCommand("admin.say", "You're not allowed to lead this squad.", "player", speaker);
                }
            }

            return false;
        }
        public bool OnUnLeadChat(string message, string speaker)
        {

            if (message.Equals("!unlead"))
            {
                // Check if player is in Squad
                Squad squad = squads.SearchSquad(speaker);
                if (squad == null)
                    return true;

                if (squad.getID(1) < 1)
                    return true;

                if (squad.GetSquadLeader() != speaker)
                {
                    ServerCommand("admin.say", "You are not Squad Leader.", "player", speaker);
                    return true;
                }


                if (squad.getSize() < 2)
                    return true;
                else
                {
                    String NewSquadLeader = squad.GetNoSquadLeader();
                    DebugWrite("^b" + NewSquadLeader + "^n is the new of Squad Leader of Squad ^b[" + squad.getID(0) + "][" + squad.getName() + "]^n.", 3);
                    ServerCommand("squad.leader", squad.getID()[0].ToString(), squad.getID()[1].ToString(), NewSquadLeader);
                    foreach (String Member in squad.getMembers())
                    {
                        if (Member == speaker)
                            ServerCommand("admin.say", "You gave up your Squad Leadership.", "player", speaker);
                        else
                            ServerCommand("admin.say", NewSquadLeader + " is the new Squad Leader of your Squad.", "player", Member);
                    }
                }
            }

            return false;
        }
        public bool NewLeaderChat(string message, string speaker)
        {

            if (!VoteDismiss)
                return false;

            if (message.Equals("!newleader"))
            {
                if (!VoteDismiss)
                    return true;

                // Check if player is in Squad
                Squad squad = squads.SearchSquad(speaker);
                if (squad == null)
                    return true;

                if (squad.GetSquadLeader() == speaker)
                {
                    ServerCommand("admin.say", "You're already Squad Leader.", "player", speaker);
                    return true;
                }


                if (squad.getID(1) < 1)
                    return true;

                foreach (Vote Vote in Votes)
                {
                    if (Vote.getVoteInitiator() == speaker)
                    {
                        ServerCommand("admin.say", "You've already started a vote this round. You can only start one vote per round.", "player", speaker);
                        return true;
                    }
                    else if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                        if (Vote.VoteIsRunning())
                        {
                            ServerCommand("admin.say", "There's already a Vote running.", "player", speaker);
                            return true;
                        }
                }

                Vote NewVote = new Vote(speaker, squad.getID(0), squad.getID(1));
                Votes.Add(NewVote);

                foreach (String Member in squad.getMembers())
                {
                    if (Member == speaker)
                        ServerCommand("admin.say", "You asked for leadership. Waiting for vote result.", "player", speaker);
                    else
                    {
                        if (YellVote)
                            ServerCommand("admin.yell", speaker + " wants to be your new Squad Leader. Type '!accept' in chat to give him leadership.", "15", "player", Member);
                        ServerCommand("admin.say", speaker + " wants to be your new Squad Leader. Type '!accept' in chat to give him leadership.", "player", Member);
                    }
                }
            }

            return false;
        }
        public bool OnMoveLeadChat(string message, string speaker)
        {

            if (!MoveLead)
                return false;

            Match cmd = Regex.Match(message, @"[!@#]givelead\s+([^\s]+)", RegexOptions.IgnoreCase);
            if (cmd.Success)
            {
                if (PlayersList == null || !BuildComplete)
                    return true;

                Squad SpeakerSquad = squads.SearchSquad(speaker);

                if (SpeakerSquad == null)
                {
                    ServerCommand("admin.say", "Only Squad Leaders can give someone else Squad Lead.", "player", speaker);
                    DebugWrite(speaker + "--> Only Squad Leaders can give someone else Squad Lead.", 3);
                    return true;
                }

                if (SpeakerSquad.GetSquadLeader() != speaker)
                {
                    ServerCommand("admin.say", "Only Squad Leaders can give someone else Squad Lead.", "player", speaker);
                    DebugWrite(speaker + "--> Only Squad Leaders can give someone else Squad Lead.", 3);
                    return true;
                }

                int found = 0;
                String name = cmd.Groups[1].Value;
                String target = null;
                foreach (String p in SpeakerSquad.getMembers())
                {
                    if (p == null)
                        continue;

                    if (Regex.Match(p, name, RegexOptions.IgnoreCase).Success)
                    {
                        ++found;
                        target = p;
                    }
                }

                if (found == 0)
                {
                    ServerCommand("admin.say", "No such player name matches (" + name + ")", "player", speaker);
                    DebugWrite("admin.say No such player name matches (" + name + ") " + "player " + speaker, 3);
                    return true;
                }
                if (found > 1)
                {
                    ServerCommand("admin.say", "Multiple players match the target name (" + name + "), try again!", "player", speaker);
                    DebugWrite("admin.say Multiple players match the target name (" + name + "), try again!  player " + speaker, 3);
                    return true;
                }
                if (target == speaker)
                {
                    ServerCommand("admin.say", "You are already Squad Leader", "player", speaker);
                    DebugWrite("admin.say You are already Squad Leader. player " + speaker, 3);
                    return true;
                }


                DebugWrite("Give Squad Leader of Team/Squad ^b[" + SpeakerSquad.getID(0) + "][" + SpeakerSquad.getName() + "]^n to " + target, 3);
                ServerCommand("admin.say", "You gave your Squad Lead to " + target, "player", speaker);
                ServerCommand("admin.say", speaker + " gave you Squad Lead.", "player", target);
                ServerCommand("squad.leader", SpeakerSquad.getID(0).ToString(), SpeakerSquad.getID(1).ToString(), target);

                return true;
            }

            return false;
        }
        public bool OnAcceptChat(string message, string speaker)
        {

            if (message.Equals("!accept"))
            {
                // Check if player is in Squad
                Squad squad = squads.SearchSquad(speaker);
                if (squad == null)
                    return true;
                if (squad.getID(1) < 1)
                    return true;

                if (VoteDismiss)
                {
                    foreach (Vote Vote in Votes)
                    {
                        if (Vote.getVoteID(0) == squad.getID(0) && Vote.getVoteID(1) == squad.getID(1))
                            if (Vote.VoteIsRunning())
                            {
                                String msg = Vote.VoteYes(speaker);
                                ServerCommand("admin.say", msg, "player", speaker);
                                CheckVoteResults();
                                return true;
                            }
                    }
                }
            }

            return false;
        }
        public bool OnJoinChat(string message, string speaker)
        {

            if (message.Equals("!join"))
            {
                if (!InviteCommand)
                    return true;

                foreach (SquadInviter Inviter in ListSquadInviters)
                {
                    foreach (String Invitee in Inviter.getInvitees())
                    {
                        if (Invitee == speaker)
                        {
                            AddJoinSwitch(Invitee, Inviter);
                            DebugWrite("OnJoinChat - Added", 4);
                            PerformJoinSwitchQueue(speaker);
                            DebugWrite("OnJoinChat - PerformJoinSwitchQueue()", 4);
                            return true;
                        }

                    }

                }

            }

            return false;
        }
        public bool OnInviteChat(string message, string speaker)
        {

            if (!InviteCommand)
                return false;

            Match cmd = Regex.Match(message, @"[!@#]invite\s+([^\s]+)", RegexOptions.IgnoreCase);
            if (cmd.Success)
            {
                OnSquadInvite(message, speaker, cmd);
                return true;
            }

            return false;
        }
        public bool OnReGroupChat(string message, string speaker)
        {
            // some regex skills might be useful...

            if (!Regroup)
                return false;

            //Pattern matches the regroup command with a minimum of 2 and maximum of 5 entered players
            String pattern = @"[!@#]regroup\s([A-Z0-9-_]+)\s([A-Z0-9-_]+)(?:\s([A-Z0-9-_]+))?(?:\s([A-Z0-9-_]+))?(?:\s([A-Z0-9-_]+))?$";
            Match cmd_match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);

            if (!cmd_match.Success)
                return false;

            //Count all entered player names
            int playerCount = 0;
            for (int i = 1; i <= 5; i++)
            {
                if (!cmd_match.Groups[i].Value.Equals(String.Empty))
                {
                    playerCount++;
                }
            }

            OnReGroup(message, speaker, cmd_match, playerCount);

            return true;
          
        }

        public override void OnGlobalChat(string speaker, string message)
        {
            if (!enabled || !BuildComplete)
                return;

            if (OnDenyChat(message, speaker))
                return;
            if (OnLeadChat(message, speaker))
                return;
            if (OnUnLeadChat(message, speaker))
                return;
            if (NewLeaderChat(message, speaker))
                return;
            if (OnAcceptChat(message, speaker))
                return;
            if (OnJoinChat(message, speaker))
                return;
            if (OnInviteChat(message, speaker))
                return;
            if (OnMoveLeadChat(message, speaker))
                return;
            if (OnReGroupChat(message, speaker))
                return;

            return;
        }
        public override void OnTeamChat(string speaker, string message, int teamId)
        {
            if (!enabled || !BuildComplete)
                return;

            if (OnDenyChat(message, speaker))
                return;
            if (OnLeadChat(message, speaker))
                return;
            if (OnUnLeadChat(message, speaker))
                return;
            if (NewLeaderChat(message, speaker))
                return;
            if (OnAcceptChat(message, speaker))
                return;
            if (OnJoinChat(message, speaker))
                return;
            if (OnInviteChat(message, speaker))
                return;
            if (OnMoveLeadChat(message, speaker))
                return;
            if (OnReGroupChat(message, speaker))
                return;

            return;
        }
        public override void OnSquadChat(string speaker, string message, int teamId, int squadId)
        {

            if (!enabled || !BuildComplete)
                return;

            if (OnDenyChat(message, speaker))
                return;

            if (OnLeadChat(message, speaker))
                return;

            if (OnUnLeadChat(message, speaker))
                return;

            if (NewLeaderChat(message, speaker))
                return;

            if (OnAcceptChat(message, speaker))
                return;

            if (OnJoinChat(message, speaker))
                return;

            if (OnInviteChat(message, speaker))
                return;

            if (OnMoveLeadChat(message, speaker))
                return;

            if (OnReGroupChat(message, speaker))
                return;

            if (message.Equals("ID_CHAT_ATTACK/DEFEND") && RemoveNoOrdersLeader)
            {
                // Check if player is in Squad
                Squad squad = squads.SearchSquad(teamId, squadId);
                if (squad == null)
                    return;

                if (squad.getID(1) < 1)
                    return;
                DebugWrite(speaker + " gave orders", 4);
                squad.setLeaderOrders();
            }

            return;
        }
        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            PlayersList = players;
            SquadSwitchPossible = false;
            RestoreComplete = false;
            BuildComplete = false;

            DebugWrite("Round finished.", 1);

            Votes = null;
            RestoreOnRoundStart = StoreCurrentSquadsLeaders();
            foreach (String SquadLeader in RestoreOnRoundStart)
            {
                Squad OldSquad = squads.SearchSquad(SquadLeader);
                if (OldSquad == null)
                    continue;
                DebugWrite("Found Squad Leader " + SquadLeader + " of Team/Squad ^b[" + OldSquad.getID(0) + "][" + OldSquad.getName() + "]^n", 3);
            }

            cTimer = new System.Timers.Timer();
            cTimer.Start();
            cTimer.Elapsed += new ElapsedEventHandler(PerformSwitchQueueBeforeScramble);
            cTimer.Interval = 20000;
            cTimer.Enabled = true;

        }
        public override void OnLevelLoaded(string mapFileName, string Gamemode, int roundsPlayed, int roundsTotal)
        {
            if (!enabled)
                return;

            GameMode = Gamemode;
            RoundTime = 0.0;

            bTimer = new System.Timers.Timer();
            bTimer.Start();
            bTimer.Elapsed += new ElapsedEventHandler(SpawnPossible);
            bTimer.Interval = 25000;
            bTimer.Enabled = true;
            DebugWrite("Map loaded. Waiting " + (bTimer.Interval / 1000) + " seconds until round start", 1);

        }
        public override void OnPlayerSpawned(string soldierName, Inventory spawnedInventory)
        {
            if (!enabled)
                return;

            if (started)
            {
                started = false;
                SquadSwitchPossible = true;
                DebugWrite("Round is currently running.", 1);
                ServerCommand("listPlayers", "all");

                PluginIntervalTimer = new System.Timers.Timer();
                PluginIntervalTimer.Start();
                PluginIntervalTimer.Elapsed += new ElapsedEventHandler(PluginInterval);
                PluginIntervalTimer.Interval = 30000;
                PluginIntervalTimer.Enabled = true;

                return;

            }


            Squad squad = squads.SearchSquad(soldierName);
            if (squad == null)
                return;
            if (squad.GetSquadLeader() == soldierName)
            {
                DebugWrite("Squad Leader ^b" + soldierName + "^n Idle Time: ^b[" + 0 + "s]^n ", 4);
                squad.setLeaderIdle(0);
            }

        }
        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            if (!enabled)
                return;

            if (started)
            {
                started = false;
                SquadSwitchPossible = true;
                DebugWrite("Round is currently running.", 1);
                ServerCommand("listPlayers", "all");

                PluginIntervalTimer = new System.Timers.Timer();
                PluginIntervalTimer.Start();
                PluginIntervalTimer.Elapsed += new ElapsedEventHandler(PluginInterval);
                PluginIntervalTimer.Interval = 30000;
                PluginIntervalTimer.Enabled = true;

                return;
            }

            if (RemoveIdleLeader)
            {
                Squad squadKiller = squads.SearchSquad(kKillerVictimDetails.Killer.TeamID, kKillerVictimDetails.Killer.SquadID);
                //Squad squadVictim = squads.SearchSquad(kKillerVictimDetails.Victim.TeamID, kKillerVictimDetails.Victim.SquadID);
                if (squadKiller != null)
                {
                    if (squadKiller.GetSquadLeader() == kKillerVictimDetails.Killer.SoldierName)
                    {
                        DebugWrite("Squad Leader ^b" + squadKiller.GetSquadLeader() + "^n Idle Time: ^b[" + 0 + "s]^n ", 4);
                        squadKiller.setLeaderIdle(0);
                    }
                }
                /* Victims can idle!
                 * if (squadVictim != null)
                {
                    if (squadVictim.GetSquadLeader() == kKillerVictimDetails.Victim.SoldierName)
                    {
                        DebugWrite("Squad Leader ^b" + squadVictim.GetSquadLeader() + "^n Idle Time: ^b[" + 0 + "s]^n ", 5);
                        squadVictim.setLeaderIdle(0);
                    }
                }*/
            }


            if (InviteCommand)
            {
                if (ListSquadInviters.Count > 0 && BuildComplete && kKillerVictimDetails != null)
                {
                    foreach (SquadInviter Inviter in ListSquadInviters)
                    {
                        foreach (String Invitee in Inviter.getInvitees())
                        {
                            if (kKillerVictimDetails.Victim.SoldierName == Invitee && Inviter.getMessagesSentTo(kKillerVictimDetails.Victim.SoldierName) < HowManyInviteMessages)
                            {
                                Squad InviterSquad = squads.SearchSquad(Inviter.getInviter());
                                if (InviterSquad == null)
                                    continue;

                                Inviter.SendMessageTo(kKillerVictimDetails.Victim.SoldierName);

                                if (kKillerVictimDetails.Victim.TeamID == InviterSquad.getID(1))
                                {
                                    ServerCommand("admin.say", "Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + ". Type !join to accept.", "player", Invitee);
                                    ServerCommand("admin.yell", "Player " + Inviter.getInviter() + StripModifiers(E(" has invited you to join Squad " + InviterSquad.getName() + ".\n  Type '!join' to accept\nor '!deny' to reject.")), "15", "player", Invitee);
                                    DebugWrite("admin.say Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + "Type !join to accept. player" + Invitee, 4);

                                }
                                else
                                {
                                    ServerCommand("admin.say", "Player " + Inviter.getInviter() + " has invited you to switch the Team and join Squad " + InviterSquad.getName() + ". Type !join to accept.", "player", Invitee);
                                    DebugWrite("admin.say Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + "Type !join to accept. player" + Invitee, 4);
                                }

                            }

                        }

                    }

                }
            }
        }
        public override void OnReservedSlotsList(List<string> soldierNames)
        {
            if (!enabled)
                return;

            if (ReservedSlotsReceived == false)
                DebugWrite("Received Reserved Slot List.", 1);
            ReservedSlotsReceived = true;
            ReservedSlots = soldierNames;

        }
        public override void OnSquadIsPrivate(int teamId, int squadId, bool isPrivate)
        {

        }
        public override void OnPlayerIsAlive(string soldierName, bool isAlive)
        {
            if (!enabled)
                return;

            if (isAlive)
                return;

            if (PlayersList == null)
                return;

            int TeamID = -1;
            int SquadID = -1;

            foreach (CPlayerInfo player in PlayersList)
            {
                if (player.SoldierName == soldierName)
                {
                    TeamID = player.TeamID;
                    SquadID = player.SquadID;
                }
            }

            if (TeamID < 0 || SquadID < 0)
                return;

            if (InviteCommand)
            {
                if (ListSquadInviters.Count > 0 && BuildComplete)
                {
                    foreach (SquadInviter Inviter in ListSquadInviters)
                    {
                        foreach (String Invitee in Inviter.getInvitees())
                        {
                            if (soldierName == Invitee && Inviter.getMessagesSentTo(soldierName) < HowManyInviteMessages)
                            {
                                Squad InviterSquad = squads.SearchSquad(Inviter.getInviter());
                                if (InviterSquad == null)
                                    continue;

                                Inviter.SendMessageTo(soldierName);

                                if (TeamID == InviterSquad.getID(1))
                                {
                                    ServerCommand("admin.say", "Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + ". Type !join to accept.", "player", Invitee);
                                    ServerCommand("admin.yell", "Player " + Inviter.getInviter() + StripModifiers(E(" has invited you to join Squad " + InviterSquad.getName() + ".\n  Type '!join' to accept\nor '!deny' to reject.")), "15", "player", Invitee);
                                    DebugWrite("admin.say Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + "Type !join to accept. player" + Invitee, 3);

                                }
                                else
                                {
                                    ServerCommand("admin.say", "Player " + Inviter.getInviter() + " has invited you to switch the Team and join Squad" + InviterSquad.getName() + ". Type !join to accept.", "player", Invitee);
                                    DebugWrite("admin.say Player " + Inviter.getInviter() + " has invited you to join Squad " + InviterSquad.getName() + "Type !join to accept. player" + Invitee, 3);
                                }

                            }

                        }

                    }

                }
            }
        }
        public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled)
        {
            if (!enabled)
                return;

            PerformJoinSwitchQueue(soldierName);
        }

    } // end SquadManager

} // end namespace PRoConEvents


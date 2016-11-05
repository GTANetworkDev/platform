using System;
using System.Timers;
using System.Linq;
using System.Collections.Generic;
using GTANetworkServer;
using GTANetworkShared;

namespace Votemanager
{
    public delegate void VoteEvent(int idWon, string optionWon);

    public class Vote
    {
        public Vote(VoteDescriptor description, int length)
        {
            Votes = new Dictionary<int, int>();
            AlreadyVoted = new List<Client>();
            Type = description;
            _timerLen = length;
        }

        public void Dispose()
        {
            _mainTimer.Stop();
            _mainTimer.Dispose();
        }

        public void Start()
        {            
            SendVoteToAll(Type);

            _mainTimer = new Timer(_timerLen);
            _mainTimer.AutoReset = false;
            _mainTimer.Elapsed += (sender, args) =>
            {
                var idWon = Votes.OrderByDescending(pair => pair.Value).ToList()[0].Key;
                var opWon = Type.Options[idWon];
                invokeFinished(idWon, opWon);
                Type.Finish(idWon, opWon);
                EndVote();
            };
            _mainTimer.Start();
        }

        public void SendVoteToClient(VoteDescriptor description, Client target)
        {
            List<object> objParam = new List<object>();
            objParam.Add(description.name);
            objParam.Add(description.Options.Length);
            objParam.AddRange(description.Options);

            cr.c.triggerClientEvent(target, "start_vote", objParam.ToArray());
        }

        public void SendVoteToAll(VoteDescriptor description)
        {
            List<object> objParam = new List<object>();
            objParam.Add(description.name);
            objParam.Add(description.Options.Length);
            objParam.AddRange(description.Options);

            cr.c.triggerClientEventForAll("start_vote", objParam.ToArray());
        }

        private void EndVote()
        {
            cr.c.triggerClientEventForAll("end_vote");
        }

        private Dictionary<int, int> Votes { get; set; }
        private List<Client> AlreadyVoted { get; set; }

        private Timer _mainTimer;
        private int _timerLen;

        // ms
        public int TotalTime
        {
            get
            {
                return _timerLen;
            }
        }

        public VoteDescriptor Type { get; set; }

        public event VoteEvent OnFinished;

        private void invokeFinished(int id, string text)
        {
            if (OnFinished != null) OnFinished.Invoke(id, text);
        }

        public bool CastVote(Client voter, int option)
        {
            if (AlreadyVoted.Contains(voter)) return false;
            AlreadyVoted.Add(voter);

            lock (Votes)
            {
                if (Votes.ContainsKey(option))
                {
                    Votes[option]++;
                }
                else
                {
                    Votes.Add(option, 1);
                }
            }

            return true;
        }
    }

    public abstract class VoteDescriptor
    {
        public abstract void Finish(int winning, string optionWon);

        public virtual string[] Options { get; set; }
        public abstract string name { get; }
    }

    public class MapVote : VoteDescriptor
    {
        public override void Finish(int winningOption, string optionWon)
        {
            var mapName = optionWon.ToString();

            if (cr.c.doesResourceExist(mapName))
            {
                cr.c.stopResource(mapName);
                cr.c.startResource(mapName);
            }
        }

        public override string name
        {
            get { return "VOTE FOR NEXT MAP"; }
        }
    }

    public class VoteKick : VoteDescriptor
    {
        public Client Target;

        public VoteKick(Client target)
        {
            Target = target;
            Options = new [] 
            {
                "No",
                "Yes"
            };
        }

        public override void Finish(int winningOption, string optionWon)
        {
            if (winningOption == 1) // Yes
            {
                cr.c.kickPlayer(Target, "You have been votekicked.");
            }
        }

        public override string name
        {
            get { return string.Format("KICK ~r~{0}~w~ FROM THE GAME?", Target.name); }
        }
    }
}
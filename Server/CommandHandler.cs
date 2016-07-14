using System.Collections.Generic;
using System.Reflection;
using System;
using System.Globalization;
using System.Linq;
using GTANetworkShared;

namespace GTANetworkServer
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CommandAttribute : System.Attribute
    {
        public readonly string CommandString;
        public bool GreedyArg { get; set; }

        public CommandAttribute(string command)
        {
            CommandString = command.TrimStart('/');
        }
    }

    public class CommandParser
    {
        public string Command;
        public bool Greedy;
        public ScriptingEngine Engine;
        public MethodInfo Method;
        public ParameterInfo[] Parameters;

        public bool Parse(string cmdRaw)
        {
            if (string.IsNullOrWhiteSpace(cmdRaw)) return false;
            var args = cmdRaw.Split();

            if (args[0].TrimStart('/') != Command) return false;

            if (args.Length - 1 < Parameters.Length || (args.Length - 1 > Parameters.Length && !Greedy))
            {
                Program.Output("~y~USAGE: ~w~/" + Command + " " + Parameters.Select(param => param.Name).Aggregate((prev, next) => prev + " [" + next + "]"));
                return true;
            }

            object[] arguments = new object[Parameters.Length];

            for (int i = 0; i < Parameters.Length; i++)
            {
                if (Parameters[i].GetType() == typeof(Client))
                {
                    var cTarget = Program.ServerInstance.GetClientFromName(args[i]);

                    if (cTarget == null)
                    {
                        Program.Output("~r~ERROR: ~w~ No player named \"" + args[i] + "\" has been found for " + Parameters[i].Name + ".");
                        return true;
                    }

                    arguments[i] = cTarget;
                    continue;
                }

                if (i == Parameters.Length - 1 && Greedy)
                {
                    arguments[i] = string.Join(" ", args.Skip(i));
                    continue;
                }

                arguments[i] = Convert.ChangeType(args[i], Parameters[i].GetType(), CultureInfo.InvariantCulture);
            }

            try
            {
                Engine.InvokeMethod(Method.Name, arguments);
            }
            catch (Exception ex)
            {
                Program.Output("UNHANDLED EXCEPTION IN COMMAND " + Command + " FOR RESOURCE " + Engine.ResourceParent.DirectoryName);
                Program.Output(ex.ToString());
            }

            return true;
        }
    }
    
    public class CommandCollection
    {
        public List<CommandParser> ResourceCommands = new List<CommandParser>();
        public Resource Resource;


        public CommandCollection(Resource res)
        {
            Resource = res;

            foreach (var engine in res.Engines)
            {
                var info = engine.GetAssembly.GetType();
                var methods = info.GetMethods();
                foreach (var method in methods.Where(ifo => ifo.CustomAttributes.Any(att =>
                                            att.AttributeType == typeof(CommandAttribute))))
                {
                    var cmd = method.GetCustomAttribute<CommandAttribute>();
                    var args = method.GetParameters();
                    var parser = new CommandParser();
                    parser.Command = cmd.CommandString;
                    parser.Greedy = cmd.GreedyArg;
                    parser.Engine = engine;
                    parser.Parameters = args;
                    parser.Method = method;

                    lock (ResourceCommands) ResourceCommands.Add(parser);
                }
            }
        }

        public bool Parse(string rawCommand)
        {
            lock (ResourceCommands)
            {
                foreach (var cmd in ResourceCommands)
                {
                    if (cmd.Parse(rawCommand)) return true;
                }
            }
            return false;
        }
    }

    public class CommandHandler
    {
        public CommandHandler()
        {
            Commands = new Dictionary<string, CommandCollection>();
        }

        public Dictionary<string, CommandCollection> Commands { get; set; }


        public void Register(Resource res)
        {
            lock (Commands)
            {
                Commands.Set(res.DirectoryName, new CommandCollection(res));
            }
        }

        public void Unregister(string resource)
        {
            lock (Commands) Commands.Remove(resource);
        }

        public bool Parse(string rawCommand)
        {
            lock (Commands)
            {
                foreach (var resCmd in Commands)
                {
                    if (resCmd.Value.Parse(rawCommand))
                        return true;
                }
            }
            return false;
        }
    }
}

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
        public readonly string CommandHelpText;
        public bool GreedyArg { get; set; }
        public bool SensitiveInfo { get; set; }
        public bool ACLRequired { get; set; }

        public CommandAttribute(string command)
        {
            CommandString = command.TrimStart('/');
            CommandHelpText = null;
        }

        public CommandAttribute(string command, string helpText)
        {
            CommandString = command.TrimStart('/');
            CommandHelpText = helpText;
        }

        public CommandAttribute()
        {
            CommandString = null;
            CommandHelpText = null;
        }
    }

    public class CommandParser
    {
        public string Command;
        public string Helptext;
        public bool Greedy;
        public ScriptingEngine Engine;
        public MethodInfo Method;
        public ParameterInfo[] Parameters;
        public bool Sensitive;
        public bool ACLRequired;

        public bool Parse(Client sender, string cmdRaw)
        {
            if (string.IsNullOrWhiteSpace(cmdRaw)) return false;
            cmdRaw = cmdRaw.TrimEnd();
            var args = cmdRaw.Split();

            if (args[0].TrimStart('/').ToLower() != Command.ToLower()) return false;

            string helpText;

            if (Parameters.Length > 1)
            {
                int paramCounter = 0;
                helpText = "~y~USAGE: ~w~/" + Command + " [" +
                           Parameters.Skip(1)
                               .Select(param => param.Name)
                               .Aggregate((prev, next) => prev + (paramCounter++ == 0 ? "]" : "") + " [" + next + "]") +
                           (Parameters.Length == 2 ? "]" : "");
            }
            else
            {
                helpText = "~y~USAGE: ~w~/" + Command;
            }

            if (!string.IsNullOrEmpty(Helptext))
                helpText = Helptext;

            if (args.Length < Parameters.Length || (args.Length > Parameters.Length && !Greedy))
            {
                Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, helpText);
                return true;
            }

            if (ACLRequired && !Program.ServerInstance.ACLEnabled)
            {
                Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ACL must be running!");
                return true;
            }

            object[] arguments = new object[Parameters.Length];
            arguments[0] = sender;

            for (int i = 1; i < Parameters.Length; i++)
            {
                if (Parameters[i].ParameterType == typeof(Client))
                {
                    var cTarget = Program.ServerInstance.GetClientFromName(args[i]);

                    if (cTarget == null)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No player named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = cTarget;
                    continue;
                }
                else if (Parameters[i].ParameterType == typeof (VehicleHash))
                {
                    var model = Program.ServerInstance.PublicAPI.vehicleNameToModel(args[i]);

                    if (model == 0)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No vehicle model named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = (VehicleHash) model;
                    continue;
                }
                else if (Parameters[i].ParameterType == typeof(PedHash))
                {
                    var model = Program.ServerInstance.PublicAPI.pedNameToModel(args[i]);

                    if (model == 0)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No player model named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = (PedHash)model;
                    continue;
                }
                else if (Parameters[i].ParameterType == typeof(PickupHash))
                {
                    var model = Program.ServerInstance.PublicAPI.pickupNameToModel(args[i]);

                    if (model == 0)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No pickup model named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = (PickupHash)model;
                    continue;
                }
                else if (Parameters[i].ParameterType == typeof(WeaponHash))
                {
                    var model = Program.ServerInstance.PublicAPI.weaponNameToModel(args[i]);

                    if (model == 0)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No weapon model named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = (WeaponHash)model;
                    continue;
                }

                if (i == Parameters.Length - 1 && Greedy)
                {
                    arguments[i] = string.Join(" ", args.Skip(i));
                    continue;
                }

                try
                {
                    arguments[i] = Convert.ChangeType(args[i], Parameters[i].ParameterType, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    // TODO: Check if logging is extremely verbose
                    //Program.Output("UNHANDLED EXCEPTION WHEN PARSING COMMAND " + (Sensitive ? "[SENSITIVE INFO]" : cmdRaw) + " FROM PLAYER " + sender.SocialClubName);
                    //Program.Output(ex.ToString());

                    Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, helpText);
                    return true;
                }
            }

            try
            {
                Engine.InvokeVoidMethod(Method.Name, arguments);
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
                    parser.Command = (string.IsNullOrWhiteSpace(cmd.CommandString) && method.Name.StartsWith("Command_")) ? method.Name.Substring(8).ToLower() : cmd.CommandString;
                    parser.Greedy = cmd.GreedyArg;
                    parser.Engine = engine;
                    parser.Parameters = args;
                    parser.Method = method;
                    parser.Sensitive = cmd.SensitiveInfo;
                    parser.ACLRequired = cmd.ACLRequired;
                    parser.Helptext = cmd.CommandHelpText;

                    lock (ResourceCommands) ResourceCommands.Add(parser);
                }
            }
        }

        public bool Parse(Client sender, string rawCommand)
        {
            var result = false;
            lock (ResourceCommands)
            {
                foreach (var cmd in ResourceCommands)
                {
                    result = result || cmd.Parse(sender, rawCommand);
                }
            }

            return result;
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

        public bool Parse(Client sender, string rawCommand)
        {
            var result = false;
            lock (Commands)
            {
                foreach (var resCmd in Commands)
                {
                    result = result || resCmd.Value.Parse(sender, rawCommand);
                }
            }
            return result;
        }
    }
}

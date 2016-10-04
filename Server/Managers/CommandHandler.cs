using System.Collections.Generic;
using System.Reflection;
using System;
using System.Globalization;
using System.Linq;
using GTANetworkShared;

namespace GTANetworkServer
{
    public delegate object ArgumentConversionDelegate(Type conversionTarget, string argument);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class CommandAttribute : System.Attribute
    {
        public readonly string CommandString;
        public readonly string CommandHelpText;
        public bool GreedyArg { get; set; }
        public bool SensitiveInfo { get; set; }
        public bool ACLRequired { get; set; }
        public string Alias { get; set; }
        public string ArgumentConverter { get; set; }
        public bool AddToHelpmanager { get; set; }
        public string Description { get; set; }
        public string Group { get; set; }

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

    public struct CommandInfo
    {
        public CommandInfo(string cmd, ParameterInfo[] param, CommandAttribute from)
        {
            Command = cmd;
            CustomUsage = from.CommandHelpText;
            Parameters = param;
            Aliases = string.IsNullOrEmpty(from.Alias) ? new string[0] : from.Alias.Split(',');
            Greedy = from.GreedyArg;
            Sensitive = from.SensitiveInfo;
            ACLRequired = from.ACLRequired;
            AddToHelpmanager = from.AddToHelpmanager;
            Description = from.Description;
            Group = from.Group;

            if (Parameters.Length > 1)
            {
                int paramCounter = 0;
                Usage = " [" +
                           Parameters.Skip(1)
                               .Select(par => par.IsOptional ? par.Name + "?" : par.Name)
                               .Aggregate((prev, next) => prev + (paramCounter++ == 0 ? "]" : "") + " [" + next + "]") +
                           (Parameters.Length == 2 ? "]" : "");
            }
            else
            {
                Usage = "";
            }
        }

        public string Command;
        public string CustomUsage;
        public string Usage;
        public string[] Aliases;
        public bool Greedy;
        public ParameterInfo[] Parameters;
        public bool Sensitive;
        public bool ACLRequired;
        public bool AddToHelpmanager;
        public string Description;
        public string Group;
    }

    internal class CommandParser
    {
        public string Command;
        public string Usage;
        public string[] Aliases;
        public bool Greedy;
        public ScriptingEngine Engine;
        public MethodInfo Method;
        public ParameterInfo[] Parameters;
        public bool Sensitive;
        public bool ACLRequired;
        public ArgumentConversionDelegate CustomArgumentParser;
        public bool AddToHelpmanager;
        public string Description;
        public string Group;
        public CommandInfo PublicInfo;

        public bool Parse(Client sender, string cmdRaw)
        {
            if (string.IsNullOrWhiteSpace(cmdRaw)) return false;
            cmdRaw = cmdRaw.TrimEnd();
            var args = cmdRaw.Split();

            var ourcmd = args[0].TrimStart('/').ToLower();

            if (ourcmd != Command.ToLower() && (Aliases == null || Aliases.All(a => a.ToLower() != ourcmd))) return false;

            string commandUsed = Command.ToLower();

            string aliasCmd;
            if (Aliases != null && (aliasCmd = Aliases.FirstOrDefault(a => a.ToLower() == ourcmd)) != null)
                commandUsed = aliasCmd;

            string helpText;

            if (Parameters.Length > 1)
            {
                int paramCounter = 0;
                helpText = "~y~USAGE: ~w~/" + commandUsed + " [" +
                           Parameters.Skip(1)
                               .Select(param => param.IsOptional ? param.Name + "?" : param.Name)
                               .Aggregate((prev, next) => prev + (paramCounter++ == 0 ? "]" : "") + " [" + next + "]") +
                           (Parameters.Length == 2 ? "]" : "");
            }
            else
            {
                helpText = "~y~USAGE: ~w~/" + commandUsed;
            }

            if (!string.IsNullOrEmpty(Usage))
                helpText = Usage;

            int optionalArguments = Parameters.Skip(1).Count(p => p.IsOptional);

            if (args.Length < (Parameters.Length - optionalArguments) || (args.Length > Parameters.Length && !Greedy))
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
                if (args.Length <= i)
                {
                    arguments[i] = Type.Missing;
                    continue;
                }

                if (CustomArgumentParser != null)
                {
                    try
                    {
                        var parsedObject = CustomArgumentParser.Invoke(Parameters[i].ParameterType, args[i]);

                        if (parsedObject != null)
                        {
                            arguments[i] = parsedObject;
                            continue;
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        if (string.IsNullOrWhiteSpace(ex.Message))
                        {
                            Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, helpText);
                        }
                        else
                        {
                            Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, ex.Message);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Program.Output("UNHANDLED EXCEPTION WHEN PARSING COMMAND " + (Sensitive ? "[SENSITIVE INFO]" : cmdRaw) + " FROM PLAYER " + sender.SocialClubName);
                        Program.Output(ex.ToString());

                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, helpText);

                        return true;
                    }
                }

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
                else if (Parameters[i].ParameterType.IsEnum)
                {
                    object enumOut;

                    try
                    {
                        enumOut = Enum.Parse(Parameters[i].ParameterType, args[i], true);
                    }
                    catch(ArgumentException)
                    {
                        Program.ServerInstance.PublicAPI.sendChatMessageToPlayer(sender, "~r~ERROR: ~w~ No value named \"" + args[i] + "\" has been found for argument \"" + Parameters[i].Name + "\"!");
                        return true;
                    }

                    arguments[i] = enumOut;
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
                    if (Program.ServerInstance.LogLevel >= 2)
                    {
                        Program.Output("UNHANDLED EXCEPTION WHEN PARSING COMMAND " + (Sensitive ? "[SENSITIVE INFO]" : cmdRaw) + " FROM PLAYER " + sender.SocialClubName);
                        Program.Output(ex.ToString());
                    }

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

    internal class CommandCollection
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
                    parser.Command = (string.IsNullOrWhiteSpace(cmd.CommandString)) ? method.Name.ToLower() : cmd.CommandString;
                    parser.Greedy = cmd.GreedyArg;
                    parser.Engine = engine;
                    parser.Parameters = args;
                    parser.Method = method;
                    parser.Sensitive = cmd.SensitiveInfo;
                    parser.ACLRequired = cmd.ACLRequired;
                    parser.Usage = cmd.CommandHelpText;
                    parser.AddToHelpmanager = cmd.AddToHelpmanager;
                    parser.Description = cmd.Description;
                    parser.Group = cmd.Group;

                    parser.PublicInfo = new CommandInfo(parser.Command, args, cmd);

                    if (!string.IsNullOrEmpty(cmd.ArgumentConverter))
                    {
                        Script eng = engine._compiledScript;

                        if (cmd.ArgumentConverter.IndexOf('.') != -1)
                        {
                            var spl = cmd.ArgumentConverter.Split('.');

                            var ourEng = Resource.Engines.FirstOrDefault(r => r.Filename == spl[0]);

                            if (ourEng != null)
                            {
                                eng = ourEng._compiledScript;
                            }
                        }

                        var del = Delegate.CreateDelegate(typeof (ArgumentConversionDelegate), eng,
                            eng.GetType().GetMethod(cmd.ArgumentConverter));
                        parser.CustomArgumentParser = (ArgumentConversionDelegate)del;
                    }
                    
                    if (!string.IsNullOrEmpty(cmd.Alias)) parser.Aliases = cmd.Alias.Split(',').ToArray();

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

    internal class CommandHandler
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

        public string[] GetResourceCommands(string resource)
        {
            if (Commands.ContainsKey(resource))
            {
                return Commands[resource].ResourceCommands.Select(cmds => cmds.Command.ToLower()).ToArray();
            }
            return new string[0];
        }

        public CommandInfo[] GetResourceCommandInfos(string resource)
        {
            if (Commands.ContainsKey(resource))
            {
                return Commands[resource].ResourceCommands.Select(cmds => cmds.PublicInfo).ToArray();
            }
            return new CommandInfo[0];
        }

        public CommandInfo GetCommandInfo(string resource, string command)
        {
            if (Commands.ContainsKey(resource))
            {
                return
                    Commands[resource].ResourceCommands.FirstOrDefault(
                        cmds => cmds.Command.ToLower() == command.ToLower())?.PublicInfo ?? new CommandInfo();
            }

            return default(CommandInfo);
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

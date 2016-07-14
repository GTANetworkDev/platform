using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace GTANetworkServer
{
    public class AccessControlList
    {
        private string _filepath;
        private ACLRoot _mainAcl;
        private List<Client> _loggedInClients;

        public enum ObjectType
        {
            resource,
            user,
        }

        public enum RightType
        {
            general,
            command,
            function
        }

        public enum LoginResult
        {
            NoAccountFound = 0,
            LoginSuccessfulNoPassword = 1,
            WrongPassword = 2,
            LoginSuccessful = 3,
            AlreadyLoggedIn = 4,
            ACLDisabled = 5,
        }

        public AccessControlList(string path)
        {
            _loggedInClients = new List<Client>();
            _filepath = path;
            LoadACL();
        }

        public static ACLRoot ParseXml(string path)
        {
            var ser = new XmlSerializer(typeof(ACLRoot));

            using (var stream = File.OpenRead(path)) return ((ACLRoot)ser.Deserialize(stream));
        }

        public static void SaveXml(string path, ACLRoot root)
        {
            var ser = new XmlSerializer(typeof(ACLRoot));

            using (var stream = new FileStream(path, FileMode.Truncate)) ser.Serialize(stream, root);
        }

        public void MergeACL(ACLRoot resourceAcl)
        {
            if (resourceAcl.Groups != null)
            foreach (var group in resourceAcl.Groups)
            {
                if (_mainAcl.Groups.Any(grp => grp.Name == group.Name))
                {
                    var ourGrp = _mainAcl.Groups.First(grp => grp.Name == group.Name);
                    
                    foreach (var right in group.ACLRights)
                    {
                        if (!ourGrp.ACLRights.Any(r => r.Name == right.Name))
                        {
                            ourGrp.ACLRights.Add(right);
                        }
                    }

                    /* // Disabled for security measures
                    foreach ( var obj in group.Objects)
                    {
                        if (!ourGrp.Objects.Any(o => o.Name == obj.Name))
                            ourGrp.Objects.Add(obj);
                    }*/
                }
                else
                {
                    _mainAcl.Groups.Add(group);
                }
            }

            if (resourceAcl.RightLists != null)
            foreach (var rights in resourceAcl.RightLists)
            {
                if (_mainAcl.RightLists.Any(r => r.Name == rights.Name))
                {
                    var ourList = _mainAcl.RightLists.First(r => r.Name == rights.Name);
                    foreach (var right in rights.Rights)
                    {
                        if (!ourList.Rights.Any(r => r.Name == right.Name)) ourList.Rights.Add(right);
                    }
                }
                else
                {
                    _mainAcl.RightLists.Add(rights);
                }
            }
        }

        public void LoadACL()
        {
            _mainAcl = ParseXml(_filepath);

            foreach (var aclGroup in _mainAcl.Groups)
                foreach (var obj in aclGroup.Objects)
                {
                    if (!string.IsNullOrWhiteSpace(obj.Password) && obj.Password.Length != 64)
                    {
                        obj.Password = Program.GetHashSHA256(obj.Password);
                    }
                }

            SaveXml(_filepath, _mainAcl);
        }

        private ACLGroup FindObjectGroup(string objName, ObjectType objType)
        {
            var grp = _mainAcl.Groups.FirstOrDefault(aclGroup => aclGroup.Objects.Any(o => o.Name == objType.ToString() + "." + objName));
            if (grp == null)
            {
                return
                    _mainAcl.Groups.FirstOrDefault(
                        aclGroup => aclGroup.Objects.Any(o => o.Name == objType.ToString() + ".*"));
            }

            return grp;
        }

        private ACLObject FindObjectByName(string objName, ObjectType type)
        {
            foreach (var grp in _mainAcl.Groups)
            foreach (var obj in grp.Objects)
            {
                if (obj.Name == type.ToString() + "." + objName)
                {
                    return obj;
                }
            }

            return null;
        }

        private bool DoesGroupHavePermissions(ACLGroup grp, string right, RightType type)
        {
            if (grp == null) return false;
            if (!DoesRightExist(right, type)) return true;

            var groupRights = grp.ACLRights.Select(g => _mainAcl.RightLists.FirstOrDefault(r => r.Name == g.Name)).ToList();
            groupRights.Add(_mainAcl.RightLists.FirstOrDefault(r => r.Name == _mainAcl.Groups.FirstOrDefault(g => g.Objects.Any(o => o.Name == "user.*")).ACLRights[0].Name));
            if (!groupRights.Any(rightsList => rightsList.Rights.Any(r => r.Name == type + "." + right))) return true;
            return groupRights.Any(rightsList => rightsList.Rights.Any(r => r.Name == type + "." + right && r.Access));
        }

        private bool DoesRightExist(string right, RightType type)
        {
            var exist = _mainAcl.RightLists.Any(grp => grp.Rights.Any(r => r.Name == type + "." + right));
            return exist;
        }

        public bool DoesResourceHaveAccessToFunction(string callingResource, string function)
        {
            var resGrp = FindObjectGroup(callingResource, ObjectType.resource);
            return DoesGroupHavePermissions(resGrp, function, RightType.function);
        }

        public bool DoesUserHaveAccessToCommand(Client client, string command)
        {
            if (!DoesRightExist(command, RightType.command)) return true;
            var userGrp = FindObjectGroup(client.SocialClubName, ObjectType.user);
            return (DoesGroupHavePermissions(userGrp, command, RightType.command) && IsPlayerLoggedIn(client));
        }

        public LoginResult TryLoginPlayer(Client player, string password)
        {
            if (_loggedInClients.Contains(player)) return LoginResult.AlreadyLoggedIn;
            var ourObj = FindObjectByName(player.SocialClubName, ObjectType.user);
            if (ourObj == null) return LoginResult.NoAccountFound;
            if (string.IsNullOrWhiteSpace(ourObj.Password))
            {
                if (!_loggedInClients.Contains(player)) _loggedInClients.Add(player);
                return LoginResult.LoginSuccessfulNoPassword;
            }
            var loginSuccessful = ourObj.Password == Program.GetHashSHA256(password) ? LoginResult.LoginSuccessful : LoginResult.WrongPassword;

            if (loginSuccessful == LoginResult.LoginSuccessful)
            {
                if (!_loggedInClients.Contains(player)) _loggedInClients.Add(player);
            }

            return loginSuccessful;
        }

        public bool IsPlayerLoggedIn(Client player)
        {
            return _loggedInClients.Contains(player);
        }

        public string GetPlayerGroup(Client player)
        {
            return FindObjectGroup(player.SocialClubName, ObjectType.user)?.Name;
        }

        public void LogOutClient(Client player)
        {
            _loggedInClients.Remove(player);
        }
        
    }


    [XmlRoot("acl")]
    public class ACLRoot
    {
        [XmlElement("group")]
        public List<ACLGroup> Groups { get; set; }

        [XmlElement("acl")]
        public List<ACLRightsList> RightLists { get; set; }
    }

    public class ACLGroup
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("acl")]
        public List<ACLRightsList> ACLRights { get; set; }

        [XmlElement("object")]
        public List<ACLObject> Objects { get; set; }
    }

    public class ACLObject
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("password")]
        public string Password { get; set; }
    }

    public class ACLRightsList
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("right")]
        public List<ACLRight> Rights { get; set; }
    }

    public class ACLRight
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("access")]
        public bool Access { get; set; }
    }
}
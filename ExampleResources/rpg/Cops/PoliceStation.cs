using GTANetworkServer;
using GTANetworkShared;

namespace RPGResource.Cops
{
    public class PoliceStation : Script
    {
        public PoliceStation()
        {
            API.onResourceStart += onResourceStart;
        }

        public readonly Vector3 PoliceStationPos = new Vector3(440.92f, -981.76f, 30.69f);
        public ColShape InfoShape;

        public void onResourceStart()
        {
            InfoShape = API.createCylinderColShape(PoliceStationPos, 2f, 3f);

            API.createMarker(1, PoliceStationPos - new Vector3(0, 0, 1f), new Vector3(), new Vector3(),
                new Vector3(1f, 1f, 1f), 100, 255, 255, 255);

            InfoShape.onEntityEnterColShape += (shape, entity) =>
            {
                Client player;
                if ((player = API.getPlayerFromHandle(entity)) != null)
                {
                    if (API.getEntityData(player, "IS_COP") == true)
                    {
                        API.sendChatMessageToPlayer(player, "Use /mission to start a mission!");
                    }
                    else
                    {
                        var fine = CopUtil.CalculatePlayerFine(player);

                        API.sendChatMessageToPlayer(player, "Use /payfine to pay your fine" + (fine > 0 ? " of $" + fine + "." : "."));
                        //API.sendChatMessageToPlayer(player, "Use /surrender to serve your sentence.");
                    }
                }
            };
        }

        public bool IsInPoliceStation(NetHandle entity)
        {
            return InfoShape.containsEntity(entity);
        }
    }
}
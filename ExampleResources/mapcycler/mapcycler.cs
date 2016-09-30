using System;
using GTANetworkServer;
using GTANetworkShared;

public class MapCycler : Script
{
    public void endRound()
    {
        endRoundEx(5000);
    }

    public void endRoundEx(int msDelay)
    {
        API.sendChatMessageToAll(string.Format("Starting a map vote in {0} seconds!", msDelay / 1000));

        API.delay(msDelay, true, () =>
        {
            API.consoleOutput("Starting vote map!");
            API.exported.votemanager.startMapVote("");
        });
    }
}

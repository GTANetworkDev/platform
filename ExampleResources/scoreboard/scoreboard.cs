using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GTANetworkServer;
using GTANetworkShared;
using System.Threading;

public class ScoreboardScript : Script
{
    public ScoreboardScript()
    {
        API.onResourceStop += stopResourceHandler;
        API.onResourceStart += startResourceHandler;
    }

    private void startResourceHandler()
    {
        API.setWorldSyncedData("scoreboard_column_names", new List<string>());
        API.setWorldSyncedData("scoreboard_column_friendlynames", new List<string>());
        API.setWorldSyncedData("scoreboard_column_widths", new List<int>());

        addScoreboardColumn("ping", "Ping", 60);
    }

    private void stopResourceHandler()
    {
        var players = API.getAllPlayers();

        foreach (var col in API.getWorldSyncedData("scoreboard_column_names"))
        {
            foreach (var player in players)
            {
                API.resetEntitySyncedData(player.handle, col);
            }
        }

        API.resetWorldSyncedData("scoreboard_column_names");
        API.resetWorldSyncedData("scoreboard_column_friendlynames");
        API.resetWorldSyncedData("scoreboard_column_widths");
    }


    /* EXPORTS */

    public void addScoreboardColumn(string name, string friendlyName, int width)
    {
        var currentNames = API.getWorldSyncedData("scoreboard_column_names");
        var currentFNames = API.getWorldSyncedData("scoreboard_column_friendlynames");
        var currentWidths = API.getWorldSyncedData("scoreboard_column_widths");

        if (!currentNames.Contains("scoreboard_" + name))
        {
            currentNames.Add("scoreboard_" + name);
            currentFNames.Add(friendlyName);
            currentWidths.Add(width);
        }

        API.setWorldSyncedData("scoreboard_column_names", currentNames);
        API.setWorldSyncedData("scoreboard_column_friendlynames", currentFNames);
        API.setWorldSyncedData("scoreboard_column_widths", currentWidths);
    }

    public void removeScoreboardColumn(string name)
    {
        var currentNames = API.getWorldSyncedData("scoreboard_column_names");
        var currentFNames = API.getWorldSyncedData("scoreboard_column_friendlynames");
        var currentWidths = API.getWorldSyncedData("scoreboard_column_widths");

        var indx = currentNames.IndexOf("scoreboard_" + name);

        if (indx != -1)
        {
            currentNames.RemoveAt(indx);
            currentFNames.RemoveAt(indx);
            currentWidths.RemoveAt(indx);

            API.setWorldSyncedData("scoreboard_column_names", currentNames);
            API.setWorldSyncedData("scoreboard_column_friendlynames", currentFNames);
            API.setWorldSyncedData("scoreboard_column_widths", currentWidths);
        }
    }

    public void setPlayerScoreboardData(Client player, string columnName, string data)
    {
        API.setEntitySyncedData(player.handle, "scoreboard_" + columnName, data);
    }

    public void resetColumnData(string columnName)
    {
        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            API.resetEntitySyncedData(player.handle, "scoreboard_" + columnName);
        }
    }

    public void resetAllColumns()
    {
        var players = API.getAllPlayers();

        foreach (var col in API.getWorldSyncedData("scoreboard_column_names"))
        {
            foreach (var player in players)
            {
                API.resetEntitySyncedData(player.handle, col);
            }
        }

        API.setWorldSyncedData("scoreboard_column_names", new List<string>());
        API.setWorldSyncedData("scoreboard_column_friendlynames", new List<string>());
        API.setWorldSyncedData("scoreboard_column_widths", new List<int>());
    }
}
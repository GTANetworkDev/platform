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

    private void startResourceHandler(object sender, EventArgs e)
    {
        API.setWorldData("scoreboard_column_names", new List<string>());
        API.setWorldData("scoreboard_column_friendlynames", new List<string>());
        API.setWorldData("scoreboard_column_widths", new List<int>());

        addScoreboardColumn("ping", "Ping", 30);
    }

    private void stopResourceHandler(object sender, EventArgs e)
    {
        var players = API.getAllPlayers();

        foreach (var col in API.getWorldData("scoreboard_column_names"))
        {
            foreach (var player in players)
            {
                API.resetEntityData(player.CharacterHandle, col);
            }
        }

        API.resetWorldData("scoreboard_column_names");
        API.resetWorldData("scoreboard_column_friendlynames");
        API.resetWorldData("scoreboard_column_widths");
    }


    /* EXPORTS */

    public void addScoreboardColumn(string name, string friendlyName, int width)
    {
        var currentNames = API.getWorldData("scoreboard_column_names");
        var currentFNames = API.getWorldData("scoreboard_column_friendlynames");
        var currentWidths = API.getWorldData("scoreboard_column_widths");

        currentNames.Add("scoreboard_" + name);
        currentFNames.Add(friendlyName);
        currentWidths.Add(width);

        API.setWorldData("scoreboard_column_names", currentNames);
        API.setWorldData("scoreboard_column_friendlynames", currentFNames);
        API.setWorldData("scoreboard_column_widths", currentWidths);
    }

    public void removeScoreboardColumn(string name)
    {
        var currentNames = API.getWorldData("scoreboard_column_names");
        var currentFNames = API.getWorldData("scoreboard_column_friendlynames");
        var currentWidths = API.getWorldData("scoreboard_column_widths");

        var indx = currentNames.IndexOf("scoreboard_" + name);

        if (indx != -1)
        {
            currentNames.RemoveAt(indx);
            currentFNames.RemoveAt(indx);
            currentWidths.RemoveAt(indx);

            API.setWorldData("scoreboard_column_names", currentNames);
            API.setWorldData("scoreboard_column_friendlynames", currentFNames);
            API.setWorldData("scoreboard_column_widths", currentWidths);
        }
    }

    public void setPlayerScoreboardData(Client player, string columnName, string data)
    {
        API.setEntityData(player.CharacterHandle, "scoreboard_" + columnName, data);
    }

    public void resetColumnData(string columnName)
    {
        var players = API.getAllPlayers();

        foreach (var player in players)
        {
            API.resetEntityData(player.CharacterHandle, "scoreboard_" + columnName);
        }
    }

    public void resetAllColumns()
    {
        var players = API.getAllPlayers();

        foreach (var col in API.getWorldData("scoreboard_column_names"))
        {
            foreach (var player in players)
            {
                API.resetEntityData(player.CharacterHandle, col);
            }
        }

        API.setWorldData("scoreboard_column_names", new List<string>());
        API.setWorldData("scoreboard_column_friendlynames", new List<string>());
        API.setWorldData("scoreboard_column_widths", new List<int>());
    }
}
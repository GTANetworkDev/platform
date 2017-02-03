using System.Drawing;
using System.Text;
using GTA;
using GTA.Native;
using NativeUI;

namespace GTANetwork.Util
{
    public static class Subtask
    {
        public static bool IsSubtaskActive(this Ped ped, ESubtask sub)
        {
            return Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, ped, (int) sub);
        }

        public static bool IsSubtaskActive(this Ped ped, int sub)
        {
            return Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, ped, sub);
        }

        public static bool IsSubtaskActive(int ped, ESubtask sub)
        {
            return Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, ped, (int)sub);
        }

        public static bool IsSubtaskActive(int ped, int sub)
        {
            return Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, ped, sub);
        }

        public static void Debug()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < 500; i++)
            {
                if (Game.Player.Character.IsSubtaskActive(i))
                {
                    sb.Append(i + ",");
                }
            }

            new UIResText(sb.ToString(), new Point(10, 10), 0.3f).Draw();
        }
    }

    public enum ESubtask
    {
        AIMED_SHOOTING_ON_FOOT = 4,
        GETTING_UP = 16,
        MOVING_ON_FOOT_NO_COMBAT = 35,
        MOVING_ON_FOOT_COMBAT = 38,
        USING_LADDER = 47,
        CLIMBING = 50,
        GETTING_OFF_SOMETHING = 51,
        SWAPPING_WEAPON = 56,
        REMOVING_HELMET = 92,
        DEAD = 97,
        SCENARIO = 118,
        MELEE_COMBAT = 130,
        HITTING_MELEE = 130,
        ANIMATION = 134,
        SITTING_IN_VEHICLE = 150,
        DRIVING_WANDERING = 151,
        EXITING_VEHICLE = 152,

        ENTERING_VEHICLE_GENERAL = 160,
        ENTERING_VEHICLE_BREAKING_WINDOW = 161,
        ENTERING_VEHICLE_OPENING_DOOR = 162,
        ENTERING_VEHICLE_ENTERING = 163,
        ENTERING_VEHICLE_CLOSING_DOOR = 164,

        EXIING_VEHICLE_OPENING_DOOR_EXITING = 167,
        EXITING_VEHICLE_CLOSING_DOOR = 168,
        DRIVING_GOING_TO_DESTINATION_OR_ESCORTING = 169,
        USING_MOUNTED_WEAPON = 199,
        DRIVE_BY = 200,
        AIMING_THROWABLE = 289,
        AIMING_GUN = 290,
        AIMING_PREVENTED_BY_OBSTACLE = 299,
        IN_COVER_GENERAL = 287,
        IN_COVER_FULLY_IN_COVER = 288,

        RELOADING = 298,

        RUNNING_TO_COVER = 300,
        IN_COVER_TRANSITION_TO_AIMING_FROM_COVER = 302,
        IN_COVER_TRANSITION_FROM_AIMING_FROM_COVER = 303,
        IN_COVER_BLIND_FIRE = 304,

        PARACHUTING = 334,
        PUTTING_OFF_PARACHUTE = 336,

        JUMPING_OR_CLIMBING_GENERAL = 420,
        JUMPING_AIR = 421,
        JUMPING_FINISHING_JUMP = 422,
    }
}
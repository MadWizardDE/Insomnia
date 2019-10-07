using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service
{
    internal static class InsomniaEventId
    {
        public static readonly EventId USER_LOGIN = new EventId(1, "User Login");
        public static readonly EventId USER_PRESENT = new EventId(2, "User present");
        public static readonly EventId USER_NOT_PRESENT = new EventId(3, "User not present");

        public static readonly EventId COMPUTER_BUSY = new EventId(5, "Computer busy");
        public static readonly EventId COMPUTER_IDLE = new EventId(6, "Computer idle");

        public static readonly EventId STANDBY_ENTER = new EventId(11, "Entering Standby");
        public static readonly EventId STANDBY_LEAVE = new EventId(12, "Resuming Operation");

        public static readonly EventId WAKE_ON_LAN = new EventId(31, "WOL");

        public static readonly EventId SESSION_MINION_STARTED = new EventId(81, "SessionMinion started");
        public static readonly EventId SESSION_MINION_STOPPED = new EventId(82, "SessionMinion stopped");
        public static readonly EventId SESSION_MINION_ERROR = new EventId(89, "SessionMinion Error");

        public static readonly EventId POWER_EVENT_INFO = new EventId(90, "Power Event Info");
        public static readonly EventId SESSION_CHANGE_INFO = new EventId(91, "Session Change Info");

        public static readonly EventId POWER_EVENT_ERROR = new EventId(99, "Power Event Error");
        public static readonly EventId SESSION_CHANGE_ERROR = new EventId(99, "Session Change Error");
    }
}
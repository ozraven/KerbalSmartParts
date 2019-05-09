﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Lib
{
    public class Log
    {
        public enum LEVEL
        {
            OFF = 0,
            ERROR = 1,
            WARNING = 2,
            INFO = 3,
            DETAIL = 4,
            TRACE = 5
        };
        string PREFIX = "";

        public void setTitle(string t)
        {
            PREFIX = t + ": ";
        }

        public static LEVEL level = LEVEL.INFO;



        public LEVEL GetLevel()
        {
            return level;
        }

        public void SetLevel(LEVEL level)
        {
            UnityEngine.Debug.Log("log level " + level);
            Log.level = level;
        }

        public LEVEL GetLogLevel()
        {
            return level;
        }

        private bool IsLevel(LEVEL level)
        {
            return Log.level == level;
        }

        public bool IsLogable(LEVEL level)
        {
            return Log.level <= level;
        }

        public void Trace(String msg)
        {
            if (IsLogable(LEVEL.TRACE))
            {
                UnityEngine.Debug.Log(PREFIX + msg);
            }
        }

        public void Detail(String msg)
        {
            if (IsLogable(LEVEL.DETAIL))
            {
                UnityEngine.Debug.Log(PREFIX + msg);
            }
        }

[ConditionalAttribute("DEBUG")]
        public void Info(String msg)
        {

            if (IsLogable(LEVEL.INFO))

            {
                UnityEngine.Debug.Log(PREFIX + msg);
            }
        }

[ConditionalAttribute("DEBUG")]
        public void Test(String msg)
        {
            //if (IsLogable(LEVEL.INFO))

            {
                UnityEngine.Debug.LogWarning(PREFIX + "TEST:" + msg);
            }
        }


        public void Warning(String msg)
        {
            if (IsLogable(LEVEL.WARNING))
            {
                UnityEngine.Debug.LogWarning(PREFIX + msg);
            }
        }

        public void Error(String msg)
        {
            if (IsLogable(LEVEL.ERROR))
            {
                UnityEngine.Debug.LogError(PREFIX + msg);
            }
        }

        public void Exception(Exception e)
        {
            Error("exception caught: " + e.GetType() + ": " + e.Message);
        }

    }
}

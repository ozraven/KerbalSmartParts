/*
 * Author: dtobi, Firov
 * This work is shared under CC BY-NC-ND 3.0 license.
 * Non commercial, no derivatives, attribution if shared unmodified.
 * You may distribute this code and the compiled .dll as is.
 * 
 * Exception from the no-deriviates clause in case of KSP updates:
 * In case of an update of KSP that breaks the code, you may change
 * this code to make it work again and redistribute it under a different
 * class name until the author publishes an updated version. After a
 * release by the author, the right to redistribute the changed code
 * vanishes.
 * 
 * You must keep this boilerplate in the file and give credit to the author
 * in the download file as well as on the webiste that links to the file.
 * 
 * Should you wish to change things in the code, contact me via the KSP forum.
 * Patches are welcome.
 *
 */

/* Action Groups Extended Interface
 * Author: Diazo
 * Action Groups Extended Mod info: http://forum.kerbalspaceprogram.com/threads/74195
 * More info on interface: http://forum.kerbalspaceprogram.com/threads/74199
 * 
 * This version of the interface is released as part of the Smart Parts mod
 * and is licensed the same as per the paragraph above.
 * 
 * Please visit the More info link above for a Public Domain version you can use as you see fit.
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSPAPIExtensions;
using System.Reflection;

namespace KM_Lib
{
    public class AGXInterface
    {
        public static bool AGExtInstalled() //is AGX installed on this KSP game?
        {
            try //try-catch is required as the below code returns a NullRef if AGX is not present.
            {
                Type calledType = Type.GetType("ActionGroupsExtended.AGExtExternal, AGExt");
                return (bool)calledType.InvokeMember("AGXInstalled", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, null, null);
            }
            catch
            {
                return false;
            }
        }

        public static bool AGX2VslToggleGroup(uint FlightID, int group) //toggle action group on specific ship. FlightID is FlightID of rootPart of ship, not of part with action to enable
        {
            Type calledType = Type.GetType("ActionGroupsExtended.AGExtExternal, AGExt");
            bool GroupAct = (bool)calledType.InvokeMember("AGX2VslToggleGroup", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null, null, new System.Object[] { FlightID, group });
            return GroupAct;
        }
    }
}

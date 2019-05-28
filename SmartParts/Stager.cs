/*
 * Author: dtobi, Firov
 * This work is shared under Creative Commons CC BY-NC-SA 3.0 license.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;


namespace Lib
{
    public class Stager : SmartSensorModuleBase
    {

        #region Fields

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Resource"),
            UI_ChooseOption(
                options = new string[] { "Empty" }
            )]
        public string monitoredResource = "Empty";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Resource")]
        public String resourceFlightDisplay = "Empty";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Trigger when")]
        public string triggerFlightDisplay = "Decreasing";

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Monitor")]
        public string monitorFlightDisplay = "Single Part";

        [KSPField]
        public bool forceSinglePart = true;

        [KSPField]
        public string resourceToMonitor = "";

        [KSPField(isPersistant = true)]
        public bool decreasing = true;

        [KSPEvent(guiActiveEditor = true, active = true, guiName = "Trigger when Decreasing")]
        public void setDecreasing()
        {
            decreasing = !decreasing;
            if (decreasing)
                Events["setDecreasing"].guiName = "Trigger when Decreasing";
            else
                Events["setDecreasing"].guiName = "Trigger when Increasing";
        }

        public enum monitoredParts { single, stage, vessel }


        [KSPField(isPersistant = true)]
        public monitoredParts singlePart = monitoredParts.single;

        [KSPEvent(guiActiveEditor = true, active = true, guiName = "Single Part")]
        public void setSinglePart()
        {
            switch (singlePart)
            {
                case monitoredParts.single:
                    singlePart = monitoredParts.stage;
                    Events["setSinglePart"].guiName = "Current Stage";
                    break;
                case monitoredParts.stage:
                    singlePart = monitoredParts.vessel;
                    Events["setSinglePart"].guiName = "Entire Ship";
                    break;
                case monitoredParts.vessel:
                    singlePart = monitoredParts.single;
                    Events["setSinglePart"].guiName = "Single Part";
                    break;
            }
        }

        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = true, guiName = "Percentage", guiFormat = "F0", guiUnits = "%"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 100f, incrementSlide = 1f)]
        public float activationPercentage = 0;

        [KSPAction("Activate Detection")]
        public void doActivateAG(KSPActionParam param)
        {
            isArmed = true;
        }

        [KSPAction("Deactivate Detection")]
        public void doDeActivateAG(KSPActionParam param)
        {
            isArmed = false;
        }

        #endregion

        #region Variables
        private Part observedPart = null;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.
        private double lastFill = -1; // save the last fill level when the tank drains
        private bool fireNextupdate = false;
        #endregion

        #region Overrides

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor)
            {
                this.part.OnEditorAttach += OnEditorAttach;
            }

            Log.setTitle("KM Stager");
            Log.Info("Started");

            //Force activation no matter which stage it's on
            this.part.force_activate();
            //Find which part we should be monitoring in flight
            if (HighLogic.LoadedSceneIsFlight)
            {
                findObservedPart();
                //Update static flight displays with correct values
                resourceFlightDisplay = monitoredResource;
                triggerFlightDisplay = decreasing ? "Decreasing" : "Increasing";
                switch (singlePart)
                {
                    case monitoredParts.single:
                        monitorFlightDisplay = "Single Part";
                        break;
                    case monitoredParts.stage:
                        monitorFlightDisplay = "Current Stage";
                        break;
                    case monitoredParts.vessel:
                        monitorFlightDisplay = "Entire Ship";
                        break;
                }
            }
            //Find which part we should be monitoring, and update the fuel list in the editor
            if (HighLogic.LoadedSceneIsEditor && this.part.parent != null)
            {
                findObservedPart();
                updateList();
            }
            updateButtons();
            initLight(true, "light-go");
        }

        public override void OnUpdate()
        {
            if (isArmed && illuminated)
            {
                lightsOff();
            }
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextupdate)
            {
                Log.Info(string.Format("Target percentage hit, resource level: {0}", triggerFlightDisplay));
                fireAction();
            }
        }

 

        double totalVesselAmount = 0;
        double maxVesselAmount = 0;

        //
        // The following 2 functions were copied from the KSPAlternateResourcePanel
        // and are covered by the MIT license
        //

        /// <summary>
        /// Worker to find the decoupled at value
        /// </summary>
        /// <returns>Stage at which part will be decoupled. Returns -1 if the part will never be decoupled from the vessel</returns>
        Int32 CalcDecoupleStage(Part pTest)
        {
            Int32 stageOut = -1;

            //Is this part an armed decoupler?
            if ((pTest.Modules.OfType<ModuleDecouple>().Count() > 0 && !pTest.Modules.GetModule<ModuleDecouple>().isDecoupled) ||
                (pTest.Modules.OfType<ModuleAnchoredDecoupler>().Count() > 0 && !pTest.Modules.GetModule<ModuleAnchoredDecoupler>().isDecoupled))
            {
                stageOut = pTest.inverseStage;
            }
            //if not look further up the vessel tree
            else if (pTest.parent != null)
            {
                stageOut = CalcDecoupleStage(pTest.parent);
            }
            return stageOut;
        }

        /// <summary>
        /// Should be self explanatory
        /// </summary>
        /// <param name="Parts"></param>
        /// <returns>Largest Stage # in Parts list</returns>
        Int32 GetLastStage(List<Part> Parts)
        {
            if (Parts.Count > 0)
                return Parts.Max(x => CalcDecoupleStage(x));
            else return -1;
        }

        void getVesselResource()
        {
            totalVesselAmount = 0;
            maxVesselAmount = 0;

            if (singlePart == monitoredParts.stage)
            {
                int LastStage = GetLastStage(vessel.Parts);
                foreach (var p in vessel.parts.Where(x => x.inverseStage == LastStage))
                {
                    PartResource pr = p.Resources.Where(i => i.resourceName == monitoredResource).FirstOrDefault();
                    if (pr != null)
                    {
                        totalVesselAmount += pr.amount;
                        maxVesselAmount += pr.maxAmount;
                    }
                }
            }
            else
            {
                foreach (var p in vessel.parts)
                {
                    PartResource pr = p.Resources.Where(i => i.resourceName == monitoredResource).FirstOrDefault();
                    if (pr != null)
                    {
                        totalVesselAmount += pr.amount;
                        maxVesselAmount += pr.maxAmount;
                    }
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if (isArmed && monitoredResource != "Empty")
            {
                Log.Info(string.Format("OnFixedUpdate, Monitor Mode: {0}, Trigger Mode: {1}", monitorFlightDisplay, triggerFlightDisplay));
                if (singlePart == monitoredParts.single && observedPart != null && observedPart.Resources[monitoredResource] != null)
                {
                    if (lastFill >= 0)
                    {
                        if (decreasing)
                        {
                            //Once target percentage is hit, fire the action
                            //Make sure it's decresing
                            if (observedPart.Resources[monitoredResource].amount < lastFill &&
                                ((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100.0) <= activationPercentage)
                            {
                                fireNextupdate = true;
                                isArmed = false;
                            }
                        }
                        else
                        {
                            //Once target percentage is hit, fire the action
                            //Make sure it's increasing
                            if (observedPart.Resources[monitoredResource].amount > lastFill &&
                                ((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100.0) >= activationPercentage)
                            {
                                fireNextupdate = true;
                                isArmed = false;
                            }
                        }
                    }
                    //Update last fill amount
                    lastFill = observedPart.Resources[monitoredResource].amount;
                }
                else
                {
                    getVesselResource();
                    if (lastFill >= 0)
                    {
                        if (decreasing)
                        {
                            //Once target percentage is hit, fire the action
                            //Make sure it's in decresing direction
                            if (totalVesselAmount < lastFill && ((totalVesselAmount / maxVesselAmount) * 100.0) <= activationPercentage)
                            {
                                fireNextupdate = true;
                                isArmed = false;
                            }
                        }
                        else
                        {
                            //Once target percentage is hit, fire the action
                            //Make sure it's in increasing direction
                            if (totalVesselAmount > lastFill && ((totalVesselAmount / maxVesselAmount) * 100.0) >= activationPercentage)
                            {
                                fireNextupdate = true;
                                isArmed = false;
                            }
                        }
                    }
                    //Update last fill amount
                    lastFill = totalVesselAmount;
                }
            }
        }

        public void Update()
        {
            //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
            if (agxGroupType == "1" & groupLastUpdate != "1" || agxGroupType != "1" & groupLastUpdate == "1") //AGX: Monitor group to see if we need to refresh window
            {
                updateButtons();
                refreshPartWindow();
                if (agxGroupType == "1")
                {
                    groupLastUpdate = "1";
                }
                else
                {
                    groupLastUpdate = "0";
                }
            }
        }
        public override string GetInfo()
        {
            return "Built-in auto staging smart part";
        }

        private void updateButtons()
        {
            if (forceSinglePart)
            {
                Events["setSinglePart"].active = false;
            }

            //Change to AGX buttons if AGX installed
            if (AGXInterface.AGExtInstalled())
            {
                Fields["group"].guiActiveEditor = false;
                Fields["group"].guiActive = false;
                Fields["agxGroupType"].guiActiveEditor = true;
                Fields["agxGroupType"].guiActive = true;
                //Fields["agxGroupNum"].guiActiveEditor = true;
                //Fields["agxGroupNum"].guiActive = true;
                if (agxGroupType == "1") //only show groups select slider when selecting action group
                {
                    Fields["agxGroupNum"].guiActiveEditor = true;
                    Fields["agxGroupNum"].guiActive = true;
                    //Fields["agxGroupNum"].guiName = "Group:";
                }
                else
                {
                    Fields["agxGroupNum"].guiActiveEditor = false;
                    Fields["agxGroupNum"].guiActive = false;
                    //Fields["agxGroupNum"].guiName = "N/A";
                    //agxGroupNum = 1;
                }
            }
            else //AGX not installed, leave at default
            {
                Fields["group"].guiActiveEditor = true;
                Fields["group"].guiActive = true;
                Fields["agxGroupType"].guiActiveEditor = false;
                Fields["agxGroupType"].guiActive = false;
                Fields["agxGroupNum"].guiActiveEditor = false;
                Fields["agxGroupNum"].guiActive = false;
            }

            //Hide auto reset button, since we don't need, we can reactivate in AG
            Fields["autoReset"].guiActive = false;
            Fields["autoReset"].guiActiveEditor = false;

            if (decreasing)
                Events["setDecreasing"].guiName = "Trigger when Decreasing";
            else
                Events["setDecreasing"].guiName = "Trigger when Increasing";

            switch (singlePart)
            {
                case monitoredParts.single:
                    Events["setSinglePart"].guiName = "Single Part";
                    break;
                case monitoredParts.stage:
                    Events["setSinglePart"].guiName = "Current Stage";
                    break;
                case monitoredParts.vessel:
                    Events["setSinglePart"].guiName = "Entire Ship";
                    break;
            }
        }

        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //Log.Info("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }

        #endregion

        #region Methods

        private void fireAction()
        {
            int groupToFire = 0; //AGX: need to send correct group
            if (AGXInterface.AGExtInstalled())
            {
                groupToFire = int.Parse(agxGroupType);
            }
            else
            {
                groupToFire = int.Parse(group);
            }
            Helper.fireEvent(this.part, groupToFire, (int)agxGroupNum);
            fireNextupdate = false;
            lightsOn();
        }

        private void findObservedPart()
        {
            //If this is a smart fuel tank, monitor self
            if (this.part.Resources.Count > 0)
            {
                Log.Info("Monitoring this part");
                observedPart = this.part;
            }
            //Otherwise monitor the parent part
            else
            {
                Log.Info("Monitoring parent part");
                observedPart = this.part.parent;
            }
            Log.Info("Set observed part to " + observedPart.partName + "Active is: " + isArmed);
        }

        private void updateList()
        {
            Log.Info("updateList, resource: " + resourceToMonitor);

            if (resourceToMonitor != "")
            {
                monitoredResource = resourceToMonitor;
                Fields["monitoredResource"].guiActiveEditor = false;
                return;
            }
            //Create temporary string list to collect resources
            List<string> resourceList = new List<string>();
            //Instantiate monitoredResource options so we can access its option array
            UI_ChooseOption resourceOptions = (UI_ChooseOption)Fields["monitoredResource"].uiControlEditor;
            if (observedPart != null)
            {
                if (observedPart.Resources.Count > 0)
                {
                    //Iterate through resources and add them to the resourceList
                    foreach (PartResource resource in observedPart.Resources)
                    {
                        resourceList.Add(resource.resourceName);
                    }
                    //Convert resource list to array and assign it to the monitoredResource options
                    resourceOptions.options = resourceList.ToArray<String>();
                    //If we already have selected a resource, don't reassign it to the default
                    monitoredResource = (resourceList.Contains(monitoredResource) ? monitoredResource : resourceList[0]);
                }
                else
                {
                    //If there are no resources in the monitored part, set monitoredResource to "Empty"
                    resourceOptions.options = new string[] { "Empty" };
                    monitoredResource = "Empty";
                }
            }
        }

        /*
                private void changeListener() {
                    Log.Info("Monitored part resoruces changed. Updating.");
                    findObservedPart();
                    updateList();
                }
        */

        private void OnEditorAttach()
        {
            findObservedPart();
            updateList();
        }

        #endregion
    }
}


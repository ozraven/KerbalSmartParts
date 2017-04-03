/*
 * Author: dtobi, Firov
 * This work is shared under Creative Commons CC BY-NC-SA 3.0 license.
 *
 */

using KSP.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using KSP.UI;

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
        private bool illuminated = false;

        #endregion

        #region Overrides

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor)
            {
                this.part.OnEditorAttach += OnEditorAttach;
            }
            Log.Info("KM Stager Started");



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
                fireAction();
                fireNextupdate = false;
            }
        }

        public override void OnAwake()
        {
        }

        double totalVesselAmount = 0;
        double maxVesselAmount = 0;

        //
        // The following 3 functions were copied from the KSPAlternateResourcePanel
        // and are covered by the MIT license
        //

        /// <summary>
        /// Worker to find the decoupled at value
        /// </summary>
        /// <returns>Stage at which part will be decoupled. Returns -1 if the part will never be decoupled from the vessel</returns>
        Int32 CalcDecoupleStage(Part pTest)
        {
            Int32 stageOut = -1;

            //Is this part a decoupler
            if (pTest.Modules.OfType<ModuleDecouple>().Count() > 0 || pTest.Modules.OfType<ModuleAnchoredDecoupler>().Count() > 0)
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
        /// Returns the Stage number at which this part will be separated from the vehicle.
        /// </summary>
        /// <param name="p">Part to Check</param>
        /// <returns>Stage at which part will be decoupled. Returns -1 if the part will never be decoupled from the vessel</returns>
        Int32 DecoupledAt(Part p)
        {
            return CalcDecoupleStage(p);
        }

        /// <summary>
        /// Should be self explanatory
        /// </summary>
        /// <param name="Parts"></param>
        /// <returns>Largest Stage # in Parts list</returns>
        Int32 GetLastStage(List<Part> Parts)
        {
            if (Parts.Count > 0)
                return Parts.Max(x => DecoupledAt(x));
            else return -1;
        }

        Int32 LastStage = 0;

        void getVesselResource()
        {
            totalVesselAmount = 0;
            maxVesselAmount = 0;

            if (singlePart == monitoredParts.stage)
                LastStage = GetLastStage(vessel.parts);

            foreach (var p in this.vessel.parts)
            {
                if (singlePart == monitoredParts.stage && DecoupledAt(p) != LastStage)
                    continue;
                PartResource pr = p.Resources.Where(i => i.resourceName == monitoredResource).FirstOrDefault();
                if (pr != null)
                {
                    totalVesselAmount += pr.amount;
                    maxVesselAmount += pr.maxAmount;
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if (isArmed && observedPart != null && monitoredResource != "Empty")
            {
                Log.Info("OnFixedUpdate, singlePart");
                if (lastFill >= 0)
                {
                    if (singlePart == monitoredParts.single)
                    {
                        if (decreasing)
                        {

                            //Check fuel percantage and compare it to target percentage
                            //If target is 0%, rounding errors can prevent firing. Run special check to prevent this
                            if (activationPercentage == 0 && (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) <= 1) && observedPart.Resources[monitoredResource].amount == lastFill)
                            {
                                fireNextupdate = true;
                            }
                            //Once target percentage is hit, fire the action
                            else if (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) <= activationPercentage)
                            {
                                fireNextupdate = true;
                            }
                        }
                        else
                        {
                            //Check fuel percantage and compare it to target percentage
                            //If target is 100%, rounding errors can prevent firing. Run special check to prevent this
                            if (activationPercentage == 100 && (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) >= 99) && observedPart.Resources[monitoredResource].amount == lastFill)
                            {
                                fireNextupdate = true;
                            }
                            //Once target percentage is hit, fire the action
                            else if (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) >= activationPercentage)
                            {
                                fireNextupdate = true;
                            }
                        }
                    }
                    //Update last fill amount
                    lastFill = observedPart.Resources[monitoredResource].amount;
                }
                else
                {
                    Log.Info("OnFixedUpdate, entire ship");
                    getVesselResource();
                    if (lastFill >= 0)
                    {
                        if (decreasing)
                        {
                            //Check fuel percantage and compare it to target percentage
                            //If target is 0%, rounding errors can prevent firing. Run special check to prevent this
                            if (activationPercentage == 0 && (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) <= 1) && observedPart.Resources[monitoredResource].amount == lastFill)
                            {
                                fireNextupdate = true;
                            }
                            //Once target percentage is hit, fire the action
                            else if (((totalVesselAmount / maxVesselAmount) * 100) <= activationPercentage)
                            {
                                fireNextupdate = true;
                            }
                        }
                        else
                        {
                            //Check fuel percantage and compare it to target percentage
                            //If target is 100%, rounding errors can prevent firing. Run special check to prevent this
                            if (activationPercentage == 100 && (((observedPart.Resources[monitoredResource].amount / observedPart.Resources[monitoredResource].maxAmount) * 100) >= 99) && observedPart.Resources[monitoredResource].amount == lastFill)
                            {
                                fireNextupdate = true;
                            }
                            //Once target percentage is hit, fire the action
                            else if (((totalVesselAmount / maxVesselAmount) * 100) >= activationPercentage)
                            {
                                fireNextupdate = true;
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
            lightsOn();
            Log.Info("KM Stager: Target percentage hit");
            isArmed = false;
        }

        private void findObservedPart()
        {
            //If this is a smart fuel tank, monitor self
            if (this.part.Resources.Count > 0)
            {
                Log.Info("KM Stager: Monitoring this part");
                observedPart = this.part;
            }
            //Otherwise monitor the parent part
            else
            {
                Log.Info("KM Stager: Monitoring parent part");
                observedPart = this.part.parent;
            }
            Log.Info("KM Stager: Set observed part to " + observedPart.partName + "Active is: " + isArmed);
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

        private void lightsOn()
        {
            //Switch on model lights
            Utility.switchEmissive(this, lightComponentOn, true);
            //Utility.switchLight(this.part, "light-go", true);
            Utility.playAnimationSetToPosition(this.part, "glow", 1);
            illuminated = true;
        }

        private void lightsOff()
        {
            //Switch off model lights
            Utility.switchEmissive(this, lightComponentOn, false);
            //Utility.switchLight(this.part, "light-go", false);
            Utility.playAnimationSetToPosition(this.part, "glow", 0);
            illuminated = false;
        }

        /*
                private void changeListener() {
                    Log.Info("KM Stager: Monitored part resoruces changed. Updating.");
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


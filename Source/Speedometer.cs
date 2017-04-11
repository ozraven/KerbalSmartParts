/*
 * Author: dtobi, Firov, dragonfi
 * This work is shared under Creative Commons CC BY-NC-SA 3.0 license.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
//using KSPAPIExtensions;

namespace Lib
{
    public class Speedometer : SmartSensorModuleBase
    {

        #region Fields
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Speed", guiFormat = "F0", guiUnits = "m/s"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = -1000f, maxValue = 1000f, incrementLarge = 100f, incrementSmall = 10f, incrementSlide = 1f)]
        public float meterPerSecondSpeed = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Trigger on"),
            UI_ChooseOption(options = new string[] { "All", "Increasing", "Decreasing" })]
        public string direction = "All";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Speed mode"),
            UI_ChooseOption(options = new string[] { "Surface", "Horizontal", "Vertical", "Orbital" })]
        public string speedMode = "Surface";

        [KSPField(guiActive = true, guiName = "Speed")]
        public double speed = 0;
        [KSPField(guiActive = true, guiName = "Last speed")]
        private double lastSpeed = 0;

        [KSPField(guiActive = true, guiName = "Speed increasing")]
        private Boolean isSpeedIncreasing = false;

        [KSPField(guiActive = true, guiName = "Passing threshold")]
        private Boolean isPassingThreshold = false;

        [KSPField(guiActive = true, guiName = "Fire next update")]
        private Boolean fireNextupdate = false;

        [KSPField(guiActive = true, guiName = "Group last update")]
        private string groupLastUpdate = "0";

        #endregion
        #region Events
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
        //private double speed = 0;
        //private double lastSpeed = 0;
        //private Boolean isSpeedIncreasing = false;
        //private Boolean isPassingThreshold = false;
        //private Boolean illuminated = false;
        //private Boolean fireNextupdate = false;
        //private string groupLastUpdate = "0";
        //AGX: What was our selected group last update frame? Top slider.
        #endregion
        #region Overrides
        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor)
            {
                this.part.OnEditorAttach += OnEditorAttach;
                this.part.OnEditorDetach += OnEditorDetach;
                this.part.OnEditorDestroy += OnEditorDestroy;
                OnEditorAttach();
            }

            Log.setTitle("KM Speedometer");
            Log.Info("Started");

            //Initial button layout
            updateButtons();
            //Force activation no matter which stage it's on
            this.part.force_activate();
            updateButtons();
            initLight(true, "light-go");
        }

        public override void OnUpdate()
        {
            Log.Info("onUpdate called");
            //Check to see if the device has been rearmed, if so, deactivate the lights
            if (isArmed && illuminated)
            {
                lightsOff();
            }
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextupdate)
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
            }
        }

        public override void OnFixedUpdate()
        {
            Log.Info("onFixedUpdate called");
            updateSpeed();
            // Only rearm device if it's outside the threshold.
            if (!this.isArmed && this.autoReset && !this.isPassingThreshold)
            {
                this.isArmed = true;
            }

            //If the device is armed, check for the trigger
            if (this.isArmed)
            {
                //Speed increasing
                if (direction != "Decreasing" && this.isSpeedIncreasing && this.isPassingThreshold)
                {
                    fireNextupdate = true;
                    lightsOn();
                    isArmed = false;
                }
                //We're descending. Trigger at or below target height
                else if (direction != "Increasing" && !this.isSpeedIncreasing && this.isPassingThreshold)
                {
                    fireNextupdate = true;
                    lightsOn();
                    isArmed = false;
                }
            }
        }

        public void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
        {
            if (agxGroupType == "1" & groupLastUpdate != "1" || agxGroupType != "1" & groupLastUpdate == "1")
            { //AGX: Monitor group to see if we need to refresh window
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

        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //print("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }
        #endregion
        #region Methods
        private void updateSpeed()
        {
            switch (speedMode)
            {
                case "Surface":
                default:
                    this.speed = Math.Round(vessel.srfSpeed, 2);
                    break;
                case "Orbital":
                    this.speed = Math.Round(vessel.obt_speed, 2);
                    break;
                case "Horizontal":
                    this.speed = Math.Round(vessel.horizontalSrfSpeed, 2);
                    break;
                case "Vertical":
                    this.speed = Math.Round(vessel.verticalSpeed, 2);
                    break;
            }

            if (Double.IsNaN(this.lastSpeed))
            {
                this.lastSpeed = this.speed;
                return;
            }

            this.isSpeedIncreasing = this.speed > this.lastSpeed;

            var lowerBound = Math.Min(this.speed, this.lastSpeed);
            var upperBound = Math.Max(this.speed, this.lastSpeed);
            this.isPassingThreshold = (
                lowerBound < this.meterPerSecondSpeed && this.meterPerSecondSpeed < upperBound);

            this.lastSpeed = this.speed;
        }

        private void updateButtons()
        {
            //Change to AGX buttons if AGX installed
            if (AGXInterface.AGExtInstalled())
            {
                Fields["group"].guiActiveEditor = false;
                Fields["group"].guiActive = false;
                Fields["agxGroupType"].guiActiveEditor = true;
                Fields["agxGroupType"].guiActive = true;
                //Fields["agxGroupNum"].guiActiveEditor = true;
                //Fields["agxGroupNum"].guiActive = true;
                if (agxGroupType == "1")
                { //only show groups select slider when selecting action group
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
            else
            { //AGX not installed, leave at default
                Fields["group"].guiActiveEditor = true;
                Fields["group"].guiActive = true;
                Fields["agxGroupType"].guiActiveEditor = false;
                Fields["agxGroupType"].guiActive = false;
                Fields["agxGroupNum"].guiActiveEditor = false;
                Fields["agxGroupNum"].guiActive = false;
            }
        }

        bool doUpdateEditor = false;

        private void OnEditorAttach()
        {
            doUpdateEditor = true;
            //            RenderingManager.AddToPostDrawQueue(99, updateEditor);
        }

        private void OnEditorDetach()
        {
            doUpdateEditor = false;
            //            RenderingManager.RemoveFromPostDrawQueue(99, updateEditor);
        }

        private void OnEditorDestroy()
        {
            doUpdateEditor = false;
            //            RenderingManager.RemoveFromPostDrawQueue(99, updateEditor);

        }

        private void OnGUI()
        {
            if (doUpdateEditor)
                updateEditor();
        }

        private void updateEditor()
        {
            //Update buttons
            updateButtons();
        }
        #endregion
    }
}

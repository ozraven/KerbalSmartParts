using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lib
{
    public class Apsimeter : SmartSensorModuleBase
    {
        // Fires when the desired apsis passes through or reaches the specified distance.

        #region Fields

        [KSPField(isPersistant = true, guiActive = true, guiName = "10K Kilometers", guiFormat = "F0", guiUnits = "10km"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 1000f, incrementLarge = 100f, incrementSmall = 25f, incrementSlide = 1f)]
        public float kilometerTenThousandsDistance = 0;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Kilometers", guiFormat = "F0", guiUnits = "km"),
            UI_FloatEdit(scene = UI_Scene.All, minValue = 0f, maxValue = 9999f, incrementLarge = 100f, incrementSmall = 10f, incrementSlide = 1f)]
        public float kilometerOnesDistance = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Trigger on"),
            UI_ChooseOption(options = new string[] { "Apoapsis", "Periapsis" })]
        public string whichApsis = "Apoapsis";

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

        private bool fireNextUpdate = false;
        private double apsis;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.

        #endregion

        #region Overrides

        public override void OnStart(StartState state)
        {
            //Initial button layout
            updateButtons();
            //Force activation no matter which stage it's on
            this.part.force_activate();
            print("KM Apsimeter Detector Started");
            updateButtons();
            initLight(true, "light-go");
        }

        public override void OnUpdate()
        {
            //Check to see if the device has been rearmed, if so, deactivate the lights
            if (isArmed && illuminated)
            {
                lightsOff();
            }
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextUpdate)
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
                fireNextUpdate = false;
            }
        }

        public override void OnFixedUpdate()
        {
            //Check current altitude
            updateApsis();

            //If the device is armed, check for the trigger apsis
            if (isArmed)
            {
                if(this.apsis > this.kilometerTenThousandsDistance * 10000 + this.kilometerOnesDistance)
                {
                    this.fireNextUpdate = true;
                    lightsOn();
                    this.isArmed = false;
                }
            }
        }

        public void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
        {
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

        private void updateApsis()
        {
            Orbit orbit = this.vessel.orbit;
            apsis = whichApsis.Substring(0, 1) == "A" ? orbit.ApA : orbit.PeA;
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

        private void onGUI()
        {
            //Update buttons
            updateButtons();
        }

        #endregion
    }
}

using System;
using System.Collections;
using UnityEngine;

namespace Lib
{
    class EmbeddedSmartSRB : SmartSensorModuleBase
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "SRB TWR %", guiFormat = "F0", guiUnits = "%"),
        UI_FloatEdit(scene = UI_Scene.All, minValue = 100f, maxValue = 150f, incrementSlide = 1f)]
        public float StagePercentageMass = 100;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Trigger on Flameout"), UI_Toggle()]
        public bool triggerOnFlameout = true;

        [KSPField(isPersistant = false, guiActive = true, guiName = "SRB TWR", guiFormat = "F2")]
        private double displayTWR = 0;

        [KSPField(guiActive = false, guiName = "Fire next update")]
        private Boolean fireNextupdate = false;

        #region Variables
        ModuleEngines engineModule;

        double maxTWR = 0;
        bool wasArmed = false;
        bool isRunning = false;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.

        #endregion


        //private bool enabled = true;
        public new void Awake()
        {
            Log.setTitle("SmartSRB");

            base.Awake();
            var ap = PartLoader.getPartInfoByName("km_smart_srb");

            if (ap != null && !ResearchAndDevelopment.PartModelPurchased(ap) || !ResearchAndDevelopment.PartTechAvailable(ap))
            {
                if (!ResearchAndDevelopment.PartModelPurchased(ap))
                    Log.Info("SmartSRB not available due to PartModel not being purchased");

                if (!ResearchAndDevelopment.PartTechAvailable(ap))
                    Log.Info("SmartSRB not available due to PartTech not being available");

                enabled = false;
                isEnabled = false;
                updateButtons();
                return;
            }
            GameEvents.onEngineActiveChange.Add(onEngineActiveChange);
            //StartCoroutine(GuiUpdate());
        }

        public override void OnStart(StartState state)
        {
            if (!enabled)
                return;

            //Initial button layout
            updateButtons();

            wasArmed = isArmed;
            FindEngine();
        }


        public override void OnUpdate()
        {
            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
                return;
            //In order for physics to take effect on jettisoned parts, the staging event has to be fired from OnUpdate
            if (fireNextupdate)
            {
                int groupToFire = 0;
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
                isArmed = false;
                wasArmed = false;
                maxTWR = 0; // prevents triggering right away if rearmed
            }

            double twr = GetTWR();
            displayTWR = twr;

            if (isArmed)
            {
                if (maxTWR > 0 && twr >= 0 && twr <= (StagePercentageMass / 100) && twr < maxTWR)
                {
                    fireNextupdate = true;
                    //Helper.fireEvent(this.part, 0, (int)0);
                }
                else if (maxTWR > 0 && twr < 0) // will get here if engine flames out with triggerOnFlameout = false
                {
                    isArmed = false;
                }
                maxTWR = Math.Max(maxTWR, twr);
            }

            if (wasArmed != isArmed) // toggled or flamed out with triggerOnFlameout = false
            {
                wasArmed = isArmed;
                maxTWR = 0;
            }
        }

        void onEngineActiveChange(ModuleEngines me)
        {
            if (enabled && me.part == this && !isArmed)
            {
                isArmed = true;
                wasArmed = isArmed;
                maxTWR = 1.1f;
                GameEvents.onEngineActiveChange.Remove(onEngineActiveChange);
            }
        }
        bool FindEngine()
        {
            engineModule = null;
            {
                foreach (ModuleEngines engine in this.part.FindModulesImplementing<ModuleEngines>())
                {
                    if (engine.throttleLocked)
                    {
                        engineModule = engine;
                        break;
                    }
                }
            }
            if (engineModule == null)
                Log.Error("FindEngine:  EngineModule not found");
            return engineModule != null;
        }

        void Destroy()
        {
            GameEvents.onEngineActiveChange.Remove(onEngineActiveChange);
        }

        public double GetTWR()
        {
            double twr = -1;
            if (engineModule != null)
            {
                double thrust = engineModule.GetCurrentThrust();
                if (thrust > 0)
                {
                    isRunning = true;
                    Part p = engineModule.part;
                    double partTotalMass = p.mass + p.GetModuleMass(p.mass) + p.GetResourceMass();
                    //double gravHeight = vessel.altitude + vessel.mainBody.Radius; //gravity force at this altitude (not in m/s^2)
                    //double gravForce = vessel.mainBody.gMagnitudeAtCenter / Math.Pow(gravHeight, 2); //accel down due to gravity in m/s^2

                    twr = thrust / (partTotalMass * vessel.graviticAcceleration.magnitude);
                }
                else if (triggerOnFlameout && isRunning) // engineModule.flameout not always set
                {
                    twr = 0;
                }
                isRunning = thrust > 0;
            }
            return twr;
        }

        private void updateButtons()
        {

            //Change to AGX buttons if AGX installed
            Fields["isArmed"].guiName = "Embedded SmartSRB Active:";

            Fields["StagePercentageMass"].guiActive = Fields["StagePercentageMass"].guiActiveEditor = isArmed;

            if (!isArmed)
            {
                Fields["group"].guiActiveEditor = false;
                Fields["group"].guiActive = false;
                Fields["agxGroupType"].guiActiveEditor = false;
                Fields["agxGroupType"].guiActive = false;
                Fields["agxGroupNum"].guiActiveEditor = false;
                Fields["agxGroupNum"].guiActive = false;

            }
            else
            {
                if (AGXInterface.AGExtInstalled())
                {
                    Fields["group"].guiActiveEditor = false;
                    Fields["group"].guiActive = false;
                    Fields["agxGroupType"].guiActiveEditor = true;
                    Fields["agxGroupType"].guiActive = true;
                    if (agxGroupType == "1") //only show groups select slider when selecting action group
                    {
                        Fields["agxGroupNum"].guiActiveEditor = true;
                        Fields["agxGroupNum"].guiActive = true;
                    }
                    else
                    {
                        Fields["agxGroupNum"].guiActiveEditor = false;
                        Fields["agxGroupNum"].guiActive = false;
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
        }
        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            foreach (UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }

        public void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
        {
            if (!enabled)
                return;


            updateButtons();

            if (agxGroupType == "1" & groupLastUpdate != "1" || agxGroupType != "1" & groupLastUpdate == "1") //AGX: Monitor group to see if we need to refresh window
            {
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
    }
}

using System;


namespace Lib
{
    class SmartSRBBase : SmartSensorModuleBase
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
        protected ModuleEngines engineModule;

        double maxTWR = 0;
        protected bool checkEngine = false;
        bool wasArmed = false;
        bool isRunning = false;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.

        #endregion

        #region Overrides

        public override void OnStart(StartState state)
        {
            Log.setTitle(this.ClassName);
            Log.Info("Started");

            CheckEngine();

            //Initial button layout
            updateButtons();
            //Force activation no matter which stage it's on
            //this.part.force_activate();

            wasArmed = isArmed;

            Fields["autoReset"].guiActiveEditor = false;
            Fields["autoReset"].guiActive = false;
            
            initLight(true, "light-go");
        }


        public override void OnUpdate()
        {
            if (!moduleIsEnabled)
                return;
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
                isArmed = false;
                wasArmed = false;
                maxTWR = 0; // prevents triggering right away if rearmed
                lightsOn();
            }


            if (checkEngine)
                CheckEngine(); // unreachable?

            double twr = GetTWR();
            displayTWR = twr;
           
            if (isArmed)
            {
                if (maxTWR > 0 && twr >= 0 && twr <= (StagePercentageMass / 100) && twr < maxTWR)
                {
                    Log.Info("fireNextupdate maxTWR: " + maxTWR.ToString("F2") + ", twr: " + twr.ToString("F2"));
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
            if (isArmed && illuminated)
                lightsOff();
        }
        #endregion

        protected bool FindEngine(Part p)
        {
            engineModule = null;
            if (p != null)
            {
                foreach (ModuleEngines engine in p.FindModulesImplementing<ModuleEngines>())
                {
                    if (engine.throttleLocked)
                    {
                        engineModule = engine;
                        break;
                    }
                }
            }
            return engineModule != null;
        }

        protected virtual bool FindEngine()
        {
            return FindEngine(part.parent);
        }

        protected void CheckEngine()
        {
            Log.Info("SmartSRBBase.CheckParentType");
            checkEngine = false;
            
            if (FindEngine())
            {
                Fields["isArmed"].guiActiveEditor = true;
                Fields["isArmed"].guiActive = true;
            }
            else
            {
                ScreenMessages.PostScreenMessage("SmartSRB only works on SRBs", 5f, ScreenMessageStyle.UPPER_CENTER);
                Fields["isArmed"].guiActiveEditor = false;
                Fields["isArmed"].guiActive = false;
            }
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

        protected void updateButtons()
        {
            if (!moduleIsEnabled)
            {
                // Hide entire GUI
                foreach(BaseEvent e in Events)
                {
                    e.guiActive = false;
                    e.guiActiveEditor = false;
                }
                foreach (BaseField f in Fields)
                {
                    f.guiActive = false;
                    f.guiActiveEditor = false;
                }
                foreach (BaseAction a in Actions)
                {
                    a.active = false;
                }
                return;
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
        protected void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //Log.Info("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }
        protected void Update() //AGX: The OnUpdate above only seems to run in flight mode, Update() here runs in all scenes
        {
            if (!moduleIsEnabled)
                return;
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
    }

    class SmartSRB : SmartSRBBase
    {

        public new void Awake()
        {
            base.Awake();
            GameEvents.onEditorPartPlaced.Add(OnEditorPartPlaced);
        }

        public void OnEditorPartPlaced(Part p)
        {
            if (this.part.parent == null)
            {
                engineModule = null;
                checkEngine = true;
            }
            else
                CheckEngine();
        }

        void Destroy()
        {
            GameEvents.onEditorPartPlaced.Remove(OnEditorPartPlaced);
        }
    }

    class EmbeddedSmartSRB : SmartSRBBase
    {
        [KSPField(isPersistant = false)]
        public string guiGroup = "EmbededSmartSRB";
        [KSPField(isPersistant = false)]
        public string guiGroupDisplayName = "Smart SRB";

        [KSPField(isPersistant = true)]
        public bool isResearched = false;

        [KSPField(isPersistant = false)]
        public string researchPartName = "km_smart_srb";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // Group our stuff in the engine's menu
            foreach (BaseField f in Fields)
            {
                f.group.name = guiGroup;
                f.group.displayName = guiGroupDisplayName;
            }
            foreach (BaseEvent e in Events)
            {
                e.group.name = guiGroup;
                e.group.displayName = guiGroupDisplayName;
            }

            if (state == StartState.Editor) // This is an upgrade, so don't give it to an already in flight vessel that doesn't have it
            {
                researchPartName = researchPartName.Replace("_", ".");
                var ap = PartLoader.getPartInfoByName(researchPartName);

                if (ap == null)
                {
                    Log.Error("researchPartName = " + researchPartName + "; Part not found.");
                    // set isResearched to false here?
                }
                else
                {
                    isResearched = ResearchAndDevelopment.PartModelPurchased(ap) && ResearchAndDevelopment.PartTechAvailable(ap);
                    if (!isResearched)
                    {
                        if (!ResearchAndDevelopment.PartModelPurchased(ap))
                            Log.Info("SmartSRB not available due to PartModel not being purchased");

                        if (!ResearchAndDevelopment.PartTechAvailable(ap))
                            Log.Info("SmartSRB not available due to PartTech not being available");

                    }
                }
                
            }
            moduleIsEnabled = isResearched;
            updateButtons();
            
            //GameEvents.onEngineActiveChange.Add(onEngineActiveChange);
            //StartCoroutine(GuiUpdate());
        }

/*
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
*/

        protected override bool FindEngine()
        {
            bool found = FindEngine(part);
            if (!found)
                Log.Error("FindEngine:  EngineModule not found on part");
            return found;
        }
    }
}

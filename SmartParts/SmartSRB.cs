using System;


namespace Lib
{
    class SmartSRB : SmartSensorModuleBase
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "SRB TWR %", guiFormat = "F0", guiUnits = "%"),
        UI_FloatEdit(scene = UI_Scene.All, minValue = 100f, maxValue = 150f, incrementSlide = 1f)]
        public float StagePercentageMass = 0;

        [KSPField(guiActive = false, guiName = "Fire next update")]
        private Boolean fireNextupdate = false;

        #region Variables
        ModuleEngines engineModule;
        ModuleEnginesFX engineModuleFX;

        private static Log Log = new Log();
        bool operational = false;
        double maxThrust = 0;
        bool checkParentType = false;
        private string groupLastUpdate = "0"; //AGX: What was our selected group last update frame? Top slider.

        #endregion

        #region Overrides

        public override void OnStart(StartState state)
        {
            //Initial button layout
            updateButtons();
            //Force activation no matter which stage it's on
            this.part.force_activate();
            Log.Info("SmartSRB Started");
            updateButtons();
        }


        public override void OnUpdate()
        {
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


            if (checkParentType)
                CheckParentType();
            double ti = GetThrustInfo(this.part.parent, this.vessel.altitude);
            
            if (maxThrust > 0 && ti >= 0 && ti < (StagePercentageMass / 100) && ti<maxThrust)
            {
                Log.Info("maxThrust: " + maxThrust.ToString("F2") + ", ti: " + ti.ToString("F2"));
                fireNextupdate = true;
                //Helper.fireEvent(this.part, 0, (int)0);
            }
            maxThrust = Math.Max(maxThrust, ti);
        }
        #endregion

        public new void Awake()
        {
            base.Awake();
            Log.Info("SmartSRB.Awake");
            GameEvents.onEditorPartPlaced.Add(OnEditorPartPlaced);
        }
  
        void OnEditorPartPlaced(Part p)
        {
            if (this.part.parent == null)
                checkParentType = true;
            else
                CheckParentType();
        }

        void CheckParentType()
        {
            Log.Info("SmartSRB.CheckParentType");
            checkParentType = false;
            var p = this.part.parent;
          
            if (p.Modules.Contains("ModuleEngines") || p.Modules.Contains("ModuleEnginesFX")) //is part an engine?
            {
                foreach (PartModule partModule in p.Modules) //change from part to partmodules
                {
                    if (partModule.moduleName == "ModuleEngines") //find partmodule engine on th epart
                    {
                        engineModule = partModule as ModuleEngines; 

                        if (engineModule.throttleLocked) // only check if this is an srb
                        {
                            return;
                        }
                    }
                    else if (partModule.moduleName == "ModuleEnginesFX") //find partmodule engine on th epart
                    {
                        engineModuleFX = partModule as ModuleEnginesFX; 

                        if (engineModuleFX.throttleLocked)
                        {
                            return;
                        }
                    }
                }
            }
            ScreenMessages.PostScreenMessage("SmartSRB only works on SRBs", 5f, ScreenMessageStyle.UPPER_CENTER);
        }

        void Destroy()
        {
            GameEvents.onEditorPartPlaced.Remove(OnEditorPartPlaced);
        }

        public double GetThrustInfo(Part part, double altitude)
        {
            Vessel activeVessel = FlightGlobals.ActiveVessel;
            double actualThrustLastFrame = 0;
            double staticPressure = activeVessel.mainBody.GetPressure(altitude) * PhysicsGlobals.KpaToAtmospheres;
            double mass = part.mass + part.GetModuleMass(part.mass) + part.GetResourceMass();

            if (part.Modules.Contains("ModuleEngines") || part.Modules.Contains("ModuleEnginesFX")) //is part an engine?
            {
                foreach (PartModule partModule in part.Modules) //change from part to partmodules
                {
                    if (partModule.moduleName == "ModuleEngines") //find partmodule engine on th epart
                    {
                        engineModule = partModule as ModuleEngines; //change from partmodules to moduleengines

                        if (engineModule.throttleLocked) // only check if this is an srb
                        {
                            if (engineModule.isOperational)//if throttlelocked is true, this is solid rocket booster. then check engine is operational. if the engine is flamedout, disabled via-right click or not yet activated via stage control, isOperational returns false
                            {
                                operational = true;
                            }

                            actualThrustLastFrame = (float)engineModule.finalThrust; // * (float)offsetMultiplier;
                        }
                    }
                    else if (partModule.moduleName == "ModuleEnginesFX") //find partmodule engine on th epart
                    {
                        engineModuleFX = partModule as ModuleEnginesFX; //change from partmodules to moduleengines

                        if (engineModuleFX.throttleLocked)
                        {
                            if (engineModuleFX.isOperational)//if throttlelocked is true, this is solid rocket booster. then check engine is operational. if the engine is flamedout, disabled via-right click or not yet activated via stage control, isOperational returns false
                            {
                                operational = true;
                            }

                            actualThrustLastFrame = (float)engineModuleFX.finalThrust;
                        }
                    }

                }

            }
            if (operational)
            {
                var gravHeight = (float)this.vessel.altitude + (float)this.vessel.mainBody.Radius; //gravity force at this altitude (not in m/s^2)
                var gravForce = (float)this.vessel.mainBody.gMagnitudeAtCenter / (float)Math.Pow(gravHeight, 2); //accel down due to gravity in m/s^2

                var twr = actualThrustLastFrame / (gravForce * mass);

                return twr;
            }
            return -1;
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
        private void refreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            //Log.Info("Wind count " + partWins.Count());
            foreach (UIPartActionWindow partWin in partWins)
            {
                partWin.displayDirty = true;
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
    }
}

using System;


namespace Lib
{
    class SmartSRB : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiName = "SRB TWR %", guiFormat = "F0", guiUnits = "%"),
        UI_FloatEdit(scene = UI_Scene.All, minValue = 100f, maxValue = 150f, incrementSlide = 1f)]
        public float StagePercentageMass = 0;

        #region Variables
        ModuleEngines engineModule;
        ModuleEnginesFX engineModuleFX;

        private static Log Log = new Log();
        bool operational = false;
        double maxThrust = 0;
        bool checkParentType = false;
        #endregion

        #region Overrides
        public override void OnUpdate()
        {
            if (checkParentType)
                CheckParentType();
            double ti = GetThrustInfo(this.part.parent, this.vessel.altitude);
            
            if (maxThrust > 0 && ti >= 0 && ti < (StagePercentageMass / 100) && ti<maxThrust)
            {
                Log.Info("maxThrust: " + maxThrust.ToString("F2") + ", ti: " + ti.ToString("F2"));
                Helper.fireEvent(this.part, 0, (int)0);
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
    }
}

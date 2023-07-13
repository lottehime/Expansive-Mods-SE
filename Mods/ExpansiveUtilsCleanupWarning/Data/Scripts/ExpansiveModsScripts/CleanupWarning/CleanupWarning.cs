using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRageMath;

namespace Expansive.CleanupWarning
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class CleanupWarning : MySessionComponentBase
    {
        private bool init = false;

        public void Init()
        {
            init = true;

            MyAPIGateway.Session.LocalHumanPlayer.Controller.ControlledEntityChanged += OnEnterGridCockpit;
            MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate));
        }

        public override void UpdateBeforeSimulation()
        {
            if (!init)
            {
                if (MyAPIGateway.Session?.LocalHumanPlayer?.Controller == null)
                    return;

                Init();
            }
        }
        
        private void OnEnterGridCockpit(VRage.Game.ModAPI.Interfaces.IMyControllableEntity ent1, VRage.Game.ModAPI.Interfaces.IMyControllableEntity ent2)
        {
        	var cockpitBlock = ent2 as MyCockpit;

            if (cockpitBlock != null)
            {
        		bool hasBeacon = false;
                bool printMsg = false;
                string warnString = "Warning: Grid will be cleaned up due to: \n";

        		foreach(MyCubeBlock block in cockpitBlock.CubeGrid.GetFatBlocks())
                {
                    if (!hasBeacon && block is IMyBeacon)
                    {
                        hasBeacon = true;
                        continue;
        		    }
        		}

                if (cockpitBlock.CubeGrid.BigOwners.Count == 0)
                {
                    warnString += "owned by nobody.\n";
                    printMsg = true;
                }

				if (cockpitBlock.CubeGrid.DisplayName.Contains("Grid") || 
                    cockpitBlock.CubeGrid.DisplayName.Contains("Static") || 
                    cockpitBlock.CubeGrid.DisplayName.Contains("Large") ||
                    cockpitBlock.CubeGrid.DisplayName.Contains("Small"))
                {
                    warnString += "name contains 'Static', 'Large', 'Small' or 'Grid'.\n";
                    printMsg = true;
                }

        		if (cockpitBlock.CubeGrid.BlocksCount <= 19)
                {
                    warnString += "size is <20 blocks.\n";
                    printMsg = true;
                }

                if (hasBeacon == false)
                {
                    warnString += "missing a beacon.";
                    printMsg = true;
                }

                if (printMsg == true)
                {
                    MyAPIGateway.Utilities.ShowNotification(warnString, 7500, "Red");
                }
        	}
    	}
    }
}
using Sandbox.Definitions;
using System;
using VRage.Game.Components;
using VRage.Utils;

namespace Expansive.SmoothPlanetDetails
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class SmoothPlanetDetails : MySessionComponentBase
    {
        public override void LoadData()
        {
            try
            {
                foreach (MyPlanetGeneratorDefinition planetObj in MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions())
                {
                    if(planetObj.Detail != null)
                    {
                        planetObj.Detail = null;
                    }
                }
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLine("Expansive Mods Smooth Planet Details: Failed to remove surface detail: " + e);
            }
        }
    }
}
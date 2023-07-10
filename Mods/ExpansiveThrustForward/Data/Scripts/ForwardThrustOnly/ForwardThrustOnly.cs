using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Expansive.ForwardThrustersOnly
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class ForwardThrustersOnly : MyGameLogicComponent
    {
        IMyThrust Thruster;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Thruster = Entity as IMyThrust;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (Thruster.CubeGrid.Physics != null)
            {
                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            if (Thruster.Enabled)
            {
                if (Thruster.GridThrustDirection != Vector3I.Zero && Thruster.GridThrustDirection != Vector3I.Backward)
                {
                    var name = Thruster.BlockDefinition.SubtypeId.ToString();

                    if (name == "LargeBlockLargeHydrogenThrust" || name == "LargeBlockLargeHydrogenThrustIndustrial")
                        Thruster.Enabled = false;

                    else if (name == "LargeBlockLargeThrust" || name == "LargeBlockLargeThrustSciFi" || name == "LargeBlockLargeModularThruster")
                        Thruster.Enabled = false;

                    else if (name == "SmallBlockLargeHydrogenThrust" || name == "SmallBlockLargeHydrogenThrustIndustrial")
                        Thruster.Enabled = false;

                    else if (name == "SmallBlockLargeThrust" || name == "SmallBlockLargeThrustSciFi" || name == "SmallBlockLargeModularThruster")
                        Thruster.Enabled = false;

                    else;
                }
            }
        }

        public override void Close()
        {
            Thruster = null;
        }
    }
}
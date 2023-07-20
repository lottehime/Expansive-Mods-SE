using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;
using System.Collections.Concurrent;
using Sandbox.Game.EntityComponents;

namespace Expansive.VanillaGuidedMissiles
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class VanillaGuidedMissileSys: MySessionComponentBase
	{
        Dictionary<long, MissileGuidanceSystem> missileGuidanceSystemList = new Dictionary<long, MissileGuidanceSystem>();

        public override void BeforeStart()
        {
            MyAPIGateway.Missiles.OnMissileAdded += OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved += OnMissileRemoved;
        }

        public override void UpdateBeforeSimulation()
        {
            foreach (var missileGuidanceSys in missileGuidanceSystemList)
            {
                missileGuidanceSys.Value.PhysicsUpdate();
            }
        }

        private void OnMissileAdded(IMyMissile missileEnt)
        {
            if(missileEnt == null) return;

            if (missileEnt.AmmoDefinition.Id.SubtypeName != "Missile") return;

            IMyEntity targetEnt = null;

            List<IMyPlayer> playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList, (player) => player.IdentityId == missileEnt.Owner || player.Controller?.ControlledEntity?.Entity?.EntityId == missileEnt.LauncherId);

            if(playerList.Count > 0 && targetEnt == null)
            {
                IMyPlayer player = playerList.First();

                targetEnt = player?.Character?.Components?.Get<MyTargetLockingComponent>()?.Target;
            }

            if (targetEnt == null)
            {
                IMyEntity missileLauncherEnt = MyAPIGateway.Entities.GetEntityById(missileEnt.LauncherId);
                if (missileLauncherEnt != null)
                {
                    IMyLargeTurretBase missileLauncher = missileLauncherEnt as IMyLargeTurretBase;

                    if(missileLauncher != null)
                    {
                        Sandbox.ModAPI.Ingame.MyDetectedEntityInfo missileLauncherEntInfo = missileLauncher.GetTargetedEntity();
                        if (!missileLauncherEntInfo.IsEmpty() && missileLauncherEntInfo.EntityId != 0)
                        {
                            targetEnt = MyAPIGateway.Entities.GetEntityById(missileLauncherEntInfo.EntityId);
                        }
                    }
                }
            }

            IMyCubeGrid targetGrid = targetEnt as IMyCubeGrid;
            if (targetGrid is IMyCubeGrid)
            {
                targetEnt = targetGrid.GetFatBlocks<IMyThrust>().Count() > 0 ? targetGrid.GetFatBlocks<IMyThrust>().First() : null;

                if(targetEnt == null)
                {
                    targetEnt = targetGrid.GetFatBlocks<IMyTerminalBlock>().Count() > 0 ? targetGrid.GetFatBlocks<IMyTerminalBlock>().First() : null;
                }
            }

            if(targetEnt != null)
            {
                missileEnt.Synchronized = true;
                missileGuidanceSystemList.Add(missileEnt.EntityId, new MissileGuidanceSystem(missileEnt, targetEnt));
            }
        }

        private void OnMissileRemoved(IMyMissile missileEnt)
        {
            if (missileGuidanceSystemList.ContainsKey(missileEnt.EntityId))
            {
                missileGuidanceSystemList.Remove(missileEnt.EntityId);
            }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Missiles.OnMissileAdded -= OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved -= OnMissileRemoved;
        }

        private class MissileGuidanceSystem
        {
            private const float ticktime = 1f / 60f;
            private float time = 0;
            private float seekAngleModMin = 0.075f;
            private float seekAngleModMax = -0.075f;
            private float missileSpeedPercent = 1f; //0.5f
            private Vector3 missilePos = Vector3.Zero;
            private Vector3 missileDir = Vector3.Zero;
            private Vector3 missileVel = Vector3.Zero;
            private Vector3 targetLastLeadPos = Vector3.Zero;
            private Vector3 targetLastPos = Vector3.Zero;
            public IMyMissile missileEnt;
            public IMyEntity targetEnt;

            public MissileGuidanceSystem(IMyMissile missileEnt, IMyEntity targetEnt)
            {
                this.missileEnt = missileEnt;
                this.targetEnt = targetEnt;

                missileDir = this.missileEnt.WorldMatrix.Forward;

                missilePos = this.missileEnt.GetPosition();

                targetLastPos = targetEnt.GetPosition();

                MissilePhysicsRefresh();
            }

            public void MissilePhysicsRefresh()
            {
                MyMissileAmmoDefinition missileAmmoDef = (MyMissileAmmoDefinition)missileEnt.AmmoDefinition;
                float missileSpeed = Math.Min(missileAmmoDef.MissileInitialSpeed + missileAmmoDef.MissileAcceleration * time, missileAmmoDef.DesiredSpeed);
                missileVel = missileDir * (missileSpeed * missileSpeedPercent);

                if (missileEnt.Physics != null)
                {
                    missileEnt.Physics.LinearVelocity = missileVel;
                }

                if (missileEnt.Physics == null)
                {
                    missilePos = missilePos + missileVel * ticktime;
                    missileEnt.SetPosition(missilePos);
                }
            }

            public void PhysicsUpdate()
            {
                Vector3 missilePosition = missileEnt.GetPosition();
                Vector3 missileVelocity = missileVel;

                MyPhysicsComponentBase targetPhysics = null;

                if (targetEnt.Physics != null)
                {
                    targetPhysics = targetEnt.Physics;
                }

                if (targetPhysics == null && targetEnt.Parent != null && targetEnt.Parent.Physics != null)
                {
                    targetPhysics = targetEnt.Parent.Physics;
                }

                Vector3 targetPosition = targetEnt.GetPosition();
                Vector3 targetVelocity;
                if (targetPhysics != null)
                {
                    targetVelocity = targetPhysics.LinearVelocity;
                }
                else
                {
                    targetVelocity = (targetPosition - targetLastPos) * 60;
                }

                Vector3 deltaPosition = targetPosition - missilePosition;
                Vector3 deltaVelocity = targetVelocity - missileVelocity;

                Vector3 leadPosition = targetPosition + (deltaPosition.Length() / deltaVelocity.Length()) * targetVelocity;

                targetLastLeadPos = leadPosition;
                targetLastPos = targetPosition;

                Vector3 predictionPosition = leadPosition;

                if (targetEnt.MarkedForClose)
                {
                    predictionPosition = targetLastLeadPos;
                }

                Vector3 predictionDirection = predictionPosition - missileEnt.GetPosition();
                predictionDirection.Normalize();

                double angle = Math.Acos(Math.Max(Math.Min(Vector3.Dot(missileDir, predictionDirection), 1f), -1f));

                Vector3 rotationAxis = Vector3.Cross(missileDir, predictionDirection);

                rotationAxis.Normalize();

                if (!targetEnt.MarkedForClose && targetEnt.Parent != null)
                {
                    Matrix rotationMatrix = Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(rotationAxis, (float)(Math.Max(Math.Min(angle, seekAngleModMin), seekAngleModMax))));
                    missileDir = Vector3.Transform(missileDir, rotationMatrix);
                    missileDir.Normalize();
                }

                MissilePhysicsRefresh();

                MatrixD worldMatrix = missileEnt.WorldMatrix;
                worldMatrix.Forward = missileDir;
                missileEnt.WorldMatrix = worldMatrix;

                time += ticktime;
            }
        }
    }
}
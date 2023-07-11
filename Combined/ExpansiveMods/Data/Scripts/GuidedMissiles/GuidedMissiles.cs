using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.Game.EntityComponents;
using VRageMath;
using VRage;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Expansive.GuidedMissiles
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.AfterSimulation | MyUpdateOrder.Simulation)]
	public class GuidedMissileSystem: MySessionComponentBase
	{
        private class MissileGuider
        {
            public IMyMissile missile;
            public IMyEntity target;
            private Vector3 lastLeadPosition = Vector3.Zero;
            private float time = 0;
            private float seekAngleModMin = 0.075f;
            private float seekAngleModMax = -0.075f;
            private float missileSpeedPercent = 0.5f;
            private const float timeTick = 1f / 60f;
            Vector3 direction = Vector3.Zero;
            Vector3 velocity = Vector3.Zero;
            Vector3 lastTargetPosition = Vector3.Zero;
            Vector3 position = Vector3.Zero;

            public MissileGuider(IMyMissile missile, IMyEntity target)
            {
                this.missile = missile;
                this.target = target;
                direction = this.missile.WorldMatrix.Forward;
                position = this.missile.GetPosition();
                lastTargetPosition = target.GetPosition();

                UpdateVelocity();
            }

            public Vector3 GetTargetPrediction()
            {
                if (target.MarkedForClose)
                {
                    return lastLeadPosition;
                }

                Vector3 missilePosition = missile.GetPosition();
                Vector3 missileVelocity = velocity;
                MyPhysicsComponentBase targetPhysics = null;

                if (target.Physics != null)
                    targetPhysics = target.Physics;

                if (targetPhysics == null && target.Parent != null && target.Parent.Physics != null)
                    targetPhysics = target.Parent.Physics;

                Vector3 targetPosition = target.GetPosition();
                Vector3 targetVelocity;

                if (targetPhysics != null)
                {
                    targetVelocity = targetPhysics.LinearVelocity;
                }
                else
                {
                    targetVelocity = (targetPosition - lastTargetPosition) * 60;
                }

                Vector3 deltaPosition = targetPosition - missilePosition;
                Vector3 deltaVelocity = targetVelocity - missileVelocity;
                Vector3 leadPosition = targetPosition + (deltaPosition.Length() / deltaVelocity.Length()) * targetVelocity;
                lastLeadPosition = leadPosition;
                lastTargetPosition = targetPosition;

                return leadPosition;
            }

            public void Update()
            {
                UpdateDirection();

                UpdateVelocity();

                UpdateTransform();

                time += timeTick;
            }

            public void UpdateVelocity()
            {
                MyMissileAmmoDefinition ammo = (MyMissileAmmoDefinition)missile.AmmoDefinition;
                float speed = Math.Min(ammo.MissileInitialSpeed + ammo.MissileAcceleration * time, ammo.DesiredSpeed);
                velocity = direction * (speed * missileSpeedPercent);

                if (missile.Physics != null)
                    missile.Physics.LinearVelocity = velocity;
            }

            public void UpdateDirection()
            {
                Vector3 predictionPosition = GetTargetPrediction();
                Vector3 predictionDirection = predictionPosition - missile.GetPosition();

                predictionDirection.Normalize();

                double angle = Math.Acos(Math.Max(Math.Min(Vector3.Dot(direction, predictionDirection), 1f), -1f));

                Vector3 rotationAxis = Vector3.Cross(direction, predictionDirection);

                rotationAxis.Normalize();

                if (!target.MarkedForClose && target.Parent != null)
                {
                    Matrix rotationMatrix = Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(rotationAxis, (float)(Math.Max(Math.Min(angle, seekAngleModMin), seekAngleModMax))));
                    direction = Vector3.Transform(direction, rotationMatrix);
                    direction.Normalize();
                }
            }

            public void UpdateTransform()
            {
                if (missile.Physics == null)
                {
                    position = position + velocity * timeTick;
                    missile.SetPosition(position);
                }

                MatrixD worldMatrix = missile.WorldMatrix;
                worldMatrix.Forward = direction;
                missile.WorldMatrix = worldMatrix;
            }
        }

        Dictionary<long, MissileGuider> missileGuiders = new Dictionary<long, MissileGuider>();

        public override void BeforeStart()
        {
            MyAPIGateway.Missiles.OnMissileAdded += OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved += OnMissileRemoved;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Missiles.OnMissileAdded -= OnMissileAdded;
            MyAPIGateway.Missiles.OnMissileRemoved -= OnMissileRemoved;
        }

        private void OnMissileAdded(IMyMissile missile)
        {
            if(missile == null)
                return;

            if (!IsMissile(missile))
                return;

            IMyEntity target = null;

            List<IMyPlayer> myPlayers = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(myPlayers, (player) => player.IdentityId == missile.Owner || player.Controller?.ControlledEntity?.Entity?.EntityId == missile.LauncherId);

            if(myPlayers.Count > 0 && target == null)
            {
                IMyPlayer player = myPlayers.First();

                target = player?.Character?.Components?.Get<MyTargetLockingComponent>()?.Target;
            }

            if (target == null)
            {
                IMyEntity entity = MyAPIGateway.Entities.GetEntityById(missile.LauncherId);

                if (entity != null)
                {
                    IMyLargeTurretBase launcher = entity as IMyLargeTurretBase;

                    if(launcher != null)
                    {
                        Sandbox.ModAPI.Ingame.MyDetectedEntityInfo info = launcher.GetTargetedEntity();
                        if (!info.IsEmpty() && info.EntityId != 0)
                        {
                            target = MyAPIGateway.Entities.GetEntityById(info.EntityId);
                        }
                    }
                }
                
            }

            IMyCubeGrid targetGrid = target as IMyCubeGrid;
            
            if (targetGrid is IMyCubeGrid)
            {
                target = targetGrid.GetFatBlocks<IMyThrust>().Count() > 0 ? targetGrid.GetFatBlocks<IMyThrust>().First() : null;

                if(target == null)
                    target = targetGrid.GetFatBlocks<IMyTerminalBlock>().Count() > 0 ? targetGrid.GetFatBlocks<IMyTerminalBlock>().First() : null;
            }

            if(target != null)
            {
                missile.Synchronized = true;
                missileGuiders.Add(missile.EntityId, new MissileGuider(missile, target));
            }
        }

        private bool IsMissile(IMyMissile missile)
        {
            switch(missile.AmmoDefinition.Id.SubtypeName)
            {
                case "Missile":
                    return true;
            }

            return false;
        }

        private void OnMissileRemoved(IMyMissile missile)
        {
            if (missileGuiders.ContainsKey(missile.EntityId))
                missileGuiders.Remove(missile.EntityId);
        }

        public override void UpdateBeforeSimulation()
        {
            foreach (var missileGuider in missileGuiders)
            {
                missileGuider.Value.Update();
            }
        }
    }
}


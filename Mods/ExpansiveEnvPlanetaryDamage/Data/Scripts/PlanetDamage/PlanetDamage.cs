using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using Sandbox.Game.Components;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using VRage.Game.Entity;

//This script is based on the atmospheric damage script by Rexxar who is long gone from the SE modding scene.
namespace Expansive.PlanetDamage
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Planet), false)]
    public class PlanetComponent : MyGameLogicComponent
    {
        readonly List<MyEntity> QueryResult = new List<MyEntity>();
        //readonly List<string> subtypes = new List<string> { "Jupiter", "Saturn", "Uranus", "Planet-26", "Mars_E" };
        readonly List<string> subtypes = new List<string> { "ExpansiveMods_Test1", "ExpansiveMods_Test1" };
        readonly MyStringHash DamageSource = MyStringHash.GetOrCompute("Harmful atmosphere.");

        private MyPlanet Planet;
        Vector3D PlanetCenter;
        BoundingSphereD CheckSphere; //notification displayed to player
        double DamageRadius;        //grid starts exploding

        MyExplosionInfo explosionInfo;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        //Planet might not be fully initialized at Init, so do these on the first frame
        public override void UpdateOnceBeforeFrame()
        {
            Planet = Entity as MyPlanet;
            if (Planet == null)
            {
                MyLog.Default.WriteLine($"Atmospheric Damage - ERROR: {Planet.Generator.Id.SubtypeName} was null. This probably should't happen.");
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }
            else if (!subtypes.Contains(Planet.Generator.Id.SubtypeName))
            {
                NeedsUpdate = MyEntityUpdateEnum.NONE;
                return;
            }

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            MyLog.Default.WriteLine($"Atmospheric Damage - Enabling damaging atmosphere for {Planet.Generator.Id.SubtypeName}");

            PlanetCenter = Planet.PositionComp.WorldMatrixRef.Translation; // Position of the center of the planet.
            double gravityElevation = Planet.MaximumRadius * Math.Pow(Planet.Generator.SurfaceGravity / 0.05, 1.0 / Planet.Generator.GravityFalloffPower); // Distance from a planets center that its gravity affects.
            CheckSphere = new BoundingSphereD(PlanetCenter, (gravityElevation + (gravityElevation * 0.10)));  //Bounding sphere determining the area to check for entities. any players inside will get the notification.
            DamageRadius = (Planet.AverageRadius + Planet.AtmosphereAltitude) * (Planet.AverageRadius + Planet.AtmosphereAltitude); // distance squared of atmo/space border
            MyLog.Default.WriteLine($"Damage Radius for {Planet.Generator.Id.SubtypeName} is : {DamageRadius}");
            MyLog.Default.WriteLine($"Gravity Elevation for {Planet.Generator.Id.SubtypeName} is : {gravityElevation}");
            MyLog.Default.WriteLine($"Notification Radius for {Planet.Generator.Id.SubtypeName} is : {CheckSphere.Radius}");
        }

        public override void UpdateBeforeSimulation100()
        {
            base.UpdateBeforeSimulation100();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref CheckSphere, QueryResult, MyEntityQueryType.Dynamic);
            int i = 0;
            while (i < QueryResult.Count)
            {
                MyEntity entity = QueryResult[i];
                if (!(entity is IMyCharacter || entity is IMyCubeGrid) || entity.Physics == null || entity.IsPreview)
                {
                    QueryResult.RemoveAtFast(i);
                }
                else
                {
                    i++;
                }
            }

            foreach (MyEntity myEntity in QueryResult)
            {
                IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(myEntity);
                /*if (player != null && player.PromoteLevel < MyPromoteLevel.SpaceMaster)
                {
                    continue;
                }*/
                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    if (player != null && !player.Character.IsDead && player.IdentityId == MyAPIGateway.Session.Player.IdentityId)
                    {
                        MyAPIGateway.Utilities.ShowNotification("This place is not safe! Turn around before it's too late!", 3000, "Red");
                    }
                }
                MyCubeGrid myCubeGrid = myEntity as MyCubeGrid;
                IMyCharacter character = myEntity as IMyCharacter;
                if (myCubeGrid != null)
                {
                    var pos = myCubeGrid.PositionComp.WorldMatrixRef.Translation;
                    if (Vector3D.Distance(Planet.GetClosestSurfacePointGlobal(pos), pos) <= 200) // 200m above the surface, delete grid to prevent lag caused by voxel interactions
                    {
                        CloseEntity(myCubeGrid);
                    }
                    if (Vector3D.DistanceSquared(pos, PlanetCenter) <= DamageRadius)
                    {
                        DamageGrid(myCubeGrid);
                    }
                }
                if (character != null && !character.IsDead)
                {
                    //MyDamageInformation info = new MyDamageInformation(false, 10f, DamageSource, Planet.EntityId);
                    character.DoDamage(10f, DamageSource, true);
                }
            }
            QueryResult.Clear();
        }

        void CloseEntity(MyEntity entity)
        {
            if (!entity.Closed)
            {
                if (entity is IMyCharacter)
                {
                    MyDamageInformation info = new MyDamageInformation(false, float.MaxValue, DamageSource, Planet.EntityId);
                    (entity as IMyCharacter).Kill(info);
                }
                else
                {
                    var grid = entity as MyCubeGrid;
                    if (grid != null)
                    {
                        if (grid.OccupiedBlocks.Count > 0)
                        {
                            MyLog.Default.WriteLine($"Ejecting {grid.OccupiedBlocks.Count} players from ship {entity.DisplayName}");
                            grid.DismountAllCockpits();
                        }
                        MyLog.Default.WriteLine($"Removing grid that is too close to {Planet.DisplayName}: {entity.DisplayName}");
                        explosionInfo = new MyExplosionInfo
                        {
                            PlayerDamage = 0f,
                            Damage = 0f,
                            ExplosionType = MyExplosionTypeEnum.GRID_DESTRUCTION,
                            LifespanMiliseconds = 800,
                            ParticleScale = 1f,
                            ExplosionFlags = MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.CREATE_DEBRIS | MyExplosionFlags.CREATE_DECALS | MyExplosionFlags.CREATE_SHRAPNELS,
                            PlaySound = true,
                            ApplyForceAndDamage = false,
                            KeepAffectedBlocks = true,
                            ExplosionSphere = new BoundingSphereD(grid.PositionComp.WorldAABB.Center, 50),
                            HitEntity = grid,
                            OwnerEntity = grid,
                            Direction = default(Vector3D),
                            Velocity = default(Vector3D)
                        };
                        MyExplosions.AddExplosion(ref explosionInfo);
                        entity.Close();
                    }
                }
            }
        }

        void DamageGrid(MyCubeGrid grid)
        {
            if (grid == null || grid.MarkedForClose || grid.Closed)
            {
                return;
            }
            int count = 0;
            foreach (var block in grid.GetFatBlocks())
            {
                explosionInfo = new MyExplosionInfo
                {
                    PlayerDamage = 10f,
                    Damage = 2000,
                    ExplosionType = MyExplosionTypeEnum.GRID_DESTRUCTION,
                    LifespanMiliseconds = 800,
                    ParticleScale = 1f,
                    ExplosionFlags = MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.CREATE_DEBRIS | MyExplosionFlags.CREATE_DECALS | MyExplosionFlags.CREATE_SHRAPNELS | MyExplosionFlags.APPLY_DEFORMATION,
                    PlaySound = true,
                    ApplyForceAndDamage = true,
                    KeepAffectedBlocks = true,
                    ExplosionSphere = new BoundingSphereD(block.PositionComp.WorldAABB.Center, 20),
                    HitEntity = block,
                    OwnerEntity = grid,
                    Direction = grid.Physics.LinearVelocity,
                    Velocity = grid.Physics.LinearVelocity,
                    StrengthImpulse = 500f
                };
                MyExplosions.AddExplosion(ref explosionInfo);
                count++;
                if (count >= 3) return; 
            }
        }
    }
}
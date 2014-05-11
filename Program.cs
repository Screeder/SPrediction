using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using SharpDX;
using Color = System.Drawing.Color;

//Ring calc doenst work properly atm
//Crashs somewhere without exception

namespace SPrediction
{
    public class Prediction
    {
        public enum SpellType
        {
            LINE = 0,
            CIRCULAR = 1,
            RING = 2
        }

        public enum SpellTypeAOE
        {
            LINE = 0,
            CIRCULAR = 1,
            CONE = 2,
            RING = 3
        }

        private static readonly Dictionary<String, float> projectileSpeeds = new Dictionary<String, float>();
        private static readonly Dictionary<String, float> dashes = new Dictionary<String, float>();
        private static readonly Dictionary<String, float> spells = new Dictionary<String, float>();
        private static readonly Dictionary<String, float> blackList = new Dictionary<String, float>();
        private static readonly Dictionary<String, Blinks> blinks = new Dictionary<String, Blinks>();

        private static readonly ConcurrentDictionary<int, float> targetsImmobile = new ConcurrentDictionary<int, float>();

        private static readonly ConcurrentDictionary<int, float> targetsSlowed = new ConcurrentDictionary<int, float>();

        private static readonly ConcurrentDictionary<int, Dashes> targetsDashing = new ConcurrentDictionary<int, Dashes>();
                                                        //Partly used because OnDash missing->Maybe look in Packetfile

        private static readonly ConcurrentDictionary<int, float> dontShoot = new ConcurrentDictionary<int, float>();
        private static readonly ConcurrentDictionary<int, float> dontShoot2 = new ConcurrentDictionary<int, float>();

        private static readonly List<ActiveAttacks> activeAttacks = new List<ActiveAttacks>();

        private static Vector3 castPositionDraw;
        private static Vector3 positionDraw;

        private static Vector3 positionLineDraw;
        private static Vector3 positionCircularDraw;
        private static Vector3 positionConeDraw;
        private static Vector3 positionRingDraw;
        private static Vector3 positionRingDraw1;
        private static Vector3 positionRingDraw2;

        private static List<Obj_AI_Minion> drawMinions = new List<Obj_AI_Minion>();
        private static List<Vector3> drawMinionCircles = new List<Vector3>();

        static Prediction()
        {
            InitProjectileSpeeds();
            InitDashes();
            InitSpells();
            InitBlinks();
            InitBlackList();
            //Obj_AI_Base.OnProcessSpellCast += CollisionProcessSpell; <---Activate when the requierments are fulfilled like getting totaldamage and checking everything
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Game.OnGameUpdate += Game_OnGameUpdate;
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                BuffInstance[] buffs = hero.Buffs;
                foreach (BuffInstance buff in buffs)
                {
                    if (buff.Type == BuffType.Stun || buff.Type == BuffType.Suppression || buff.Type == BuffType.Knockup || buff.Type == BuffType.Sleep || buff.Type == BuffType.Snare)
                    {
                        UpdateDictionaries(targetsImmobile, hero.NetworkId, buff.EndTime);
                    }
                    else if (buff.Type == BuffType.Slow || buff.Type == BuffType.Charm || buff.Type == BuffType.Fear || buff.Type == BuffType.Taunt)
                    {
                        UpdateDictionaries(targetsSlowed, hero.NetworkId, buff.EndTime);
                    }

                    if (buff.Type == BuffType.Knockback)
                    {
                        UpdateDictionaries(dontShoot, hero.NetworkId, Game.Time + 1);
                    }
                }
            }     
        }

        protected static void DebugMode()
        {
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            float[] test1 = Drawing.WorldToScreen(ObjectManager.Player.Position);
            float[] test2 = Drawing.WorldToScreen(castPositionDraw);
            float[] test3 = Drawing.WorldToScreen(positionDraw);
            Drawing.DrawLine(test1[0], test1[1], test2[0], test2[1], 5.5f, Color.Yellow);
            Drawing.DrawLine(test1[0], test1[1], test3[0], test3[1], 5.5f, Color.Red);
            Drawing.DrawCircle(castPositionDraw, 200, Color.Aqua);
            Drawing.DrawCircle(positionDraw, 200, Color.Green);
            Drawing.DrawCircle(positionLineDraw, 200, Color.Brown);
            Drawing.DrawCircle(positionCircularDraw, 200, Color.Bisque);
            Drawing.DrawCircle(positionConeDraw, 200, Color.BlueViolet);
            Drawing.DrawCircle(positionRingDraw, 200, Color.Gray);
            Drawing.DrawCircle(positionRingDraw1, 200, Color.Brown);
            Drawing.DrawCircle(positionRingDraw2, 200, Color.Bisque);

            int index = 0;
            foreach (Obj_AI_Minion minion in drawMinions)
            {
                if (minion.IsValid)
                {
                    float[] minionPos = Drawing.WorldToScreen(minion.Position);
                    Drawing.DrawText(minionPos[0], minionPos[1], Color.White, "{0} {1}", "test", index);
                }
                index++;
            }

            foreach (Vector3 drawMinionCircle in drawMinionCircles)
            {
                Drawing.DrawCircle(drawMinionCircle, 100, Color.Black);
            }
        }

        public static float GetHitBox(Obj_AI_Base object1)
        {
            return object1.BoundingRadius;
        }

        private static void UpdateDictionaries<T>(T dictionary, object key, object value)
        {
            if (dictionary is ConcurrentDictionary<string, float>)
            {
                float temp;
                ConcurrentDictionary<String, float> dic = dictionary as ConcurrentDictionary<String, float>;
                String nKey = key as String;
                float nValue = (float)(value as Object);
                if (nKey != null && Utils.IsValidFloat(nValue))
                {
                    dic.TryRemove(nKey, out temp);
                    dic.TryAdd(nKey, nValue);
                }
            }
            else if (dictionary is ConcurrentDictionary<String, Blinks>)
            {
                Blinks temp;
                ConcurrentDictionary<String, Blinks> dic = dictionary as ConcurrentDictionary<String, Blinks>;
                String nKey = key as String;
                Blinks nValue = value as Blinks;
                if (nKey != null && nValue != null)
                {
                    dic.TryRemove(nKey, out temp);
                    dic.TryAdd(nKey, nValue);
                }
            }
            else if (dictionary is ConcurrentDictionary<int, float>)
            {
                float temp;
                ConcurrentDictionary<int, float> dic = dictionary as ConcurrentDictionary<int, float>;
                int nKey = (int)(key as Object);
                float nValue = (float)(value as Object);
                if (nKey != 0 && Utils.IsValidFloat(nValue))
                {
                    dic.TryRemove(nKey, out temp);
                    dic.TryAdd(nKey, nValue);
                }
            }
            else if (dictionary is ConcurrentDictionary<int, Dashes>)
            {
                Dashes temp;
                ConcurrentDictionary<int, Dashes> dic = dictionary as ConcurrentDictionary<int, Dashes>;
                int nKey = (int)(key as Object);
                Dashes nValue = value as Dashes;
                if (nKey != 0 && nValue != null)
                {
                    dic.TryRemove(nKey, out temp);
                    dic.TryAdd(nKey, nValue);
                }
            }    
        }

        public static Object[] IsImmobile(Obj_AI_Base unit, float delay, float radius, float speed, Vector3 from,
                                          SpellType spellType)
        {
            if (targetsImmobile.ContainsKey(unit.NetworkId))
            {
                float extraDelay = speed.CompareTo(float.MaxValue) == 0
                                       ? 0
                                       : (float) Utils.GetDistance(from, unit.ServerPosition)/speed;
                float immobileTime;
                targetsImmobile.TryGetValue(unit.NetworkId, out immobileTime);
                if (immobileTime > (Game.Time + delay + extraDelay) && spellType == SpellType.CIRCULAR)
                {
                    Vector3 temp = from - unit.ServerPosition;
                    temp.Normalize();
                    Vector3.Multiply(temp, radius);
                    return new Object[] {true, unit.ServerPosition, unit.ServerPosition + temp}; //Test if it's correct
                }
                else if (immobileTime + (radius/unit.MoveSpeed) > (Game.Time + delay + extraDelay))
                {
                    return new Object[] {true, unit.ServerPosition, unit.ServerPosition};
                }
            }
            return new Object[] {false, unit.ServerPosition, unit.ServerPosition};
        }

        public static bool IsSlowed(Obj_AI_Base unit, float delay, float speed, Vector3 from)
        {

            if (targetsSlowed.ContainsKey(unit.NetworkId))
            {
                float slowTime;
                targetsSlowed.TryGetValue(unit.NetworkId, out slowTime);
                if (slowTime > (Game.Time + delay + Utils.GetDistance(unit.ServerPosition, from) / speed))
                {
                    return true;
                }
            }
            return false;
        }

        public static Object[] IsDashing(Obj_AI_Base unit, float delay, float radius, float speed, Vector3 from)
        {
            bool isDashing = false;
            bool canHit = false;
            Vector3 newPos = new Vector3();
            if (targetsDashing.ContainsKey(unit.NetworkId))
            {
                Dashes dash;              
                targetsDashing.TryGetValue(unit.NetworkId, out dash);
                if (dash != null && dash.endT >= Game.Time)
                {
                    isDashing = true;
                    if (dash.isBlink)
                    {
                        if ((dash.endT - Game.Time) <= (delay + Utils.GetDistance(from, dash.endPos)/speed))
                        {
                            newPos = new Vector3(dash.endPos.X, dash.endPos.Y, dash.endPos.Z);
                            canHit = (unit.MoveSpeed*
                                      (delay + Utils.GetDistance(from, dash.endPos)/speed - (dash.endT2 - Game.Time))) <
                                     radius;
                        }


                        if (((dash.endT - Game.Time) >= (delay + Utils.GetDistance(from, dash.startPos)/speed)) &&
                            !canHit)
                        {
                            newPos = new Vector3(dash.startPos.X, dash.startPos.Y, dash.startPos.Z);
                            canHit = true;
                        }    
                    }
                    else
                    {
                        newPos = new Vector3(dash.endPos.X, dash.endPos.Y, dash.endPos.Z); // Need calcs
                    }
                }
            }
            return new Object[] { isDashing, canHit, newPos };
        }

        private static float MaxAngle(Obj_AI_Base unit, Vector3 currentWaypoint, Vector3 from)
        {
            List<Vector3> waypoints = GetWaypoints(unit);
            if (waypoints == null)
                return 0.0f;
            float Max = 0;
            Vector3 CV = new Vector3(currentWaypoint.X, 0, currentWaypoint.Y) - unit.ServerPosition;
            foreach (Vector3 waypoint in waypoints)
            {
                float angle = Utils.AngleBetween(new Vector3(0, 0, 0), CV,
                                                 new Vector3(waypoint.X, 0, waypoint.Y) -
                                                 new Vector3(unit.ServerPosition.X, 0, unit.ServerPosition.Y));
                if (angle > Max)
                    Max = angle;
            }
            return Max;
        }

        private static List<Vector3> GetWaypoints(Obj_AI_Base unit)
        {
            var pathes = new List<Vector3>();
            var pathes2 = new List<Vector3>();
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.NetworkId == unit.NetworkId)
                {
                    pathes.AddRange(hero.Path);
                    break;
                }
            }
            pathes2.Add(unit.ServerPosition);
            pathes2.AddRange(pathes);
            return pathes2;
        }

        private static int CountWaypoints(Obj_AI_Base unit)
        {
            try
            {
                return GetWaypoints(unit).Count();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static double GetWayPointsLength(List<Vector3> waypoints)
        {
            double result = 0f;
            for (int i = 0; i < waypoints.Count() - 1; i++)
            {
                Vector3 distance1 = waypoints[i];
                Vector3 distance2 = waypoints[i + 1];
                double distance = Utils.GetDistance(distance1, distance2);
                result = result + distance;
            }
            return result;
        }

        private static List<Vector3> CutWayPoints(List<Vector3> waypoints, float distance)
        {
            var result = new List<Vector3>();

            double remaining = distance;
            if (distance > 0)
            {
                for (int i = 0; i < waypoints.Count(); i++)
                {
                    Vector3 waypoint1 = waypoints[i];
                    Vector3 waypoint2 = waypoints[i + 1];
                    double nDistance = Utils.GetDistance(waypoint1, waypoint2);
                    if (nDistance >= remaining)
                    {
                        Vector3 temp = (waypoint2 - waypoint1);
                        temp.Normalize();
                        Vector3.Multiply(ref temp, (float) remaining, out temp);
                        result.Add(waypoint1 + temp);


                        for (int j = i; j < waypoints.Count() - 1; j++)
                        {
                            result.Add(waypoints[j]);
                        }
                        remaining = 0;
                        break;
                    }
                    else
                    {
                        remaining = remaining - nDistance;
                    }
                }
            }
            else
            {
                if (waypoints == null || waypoints.Count() == 0)
                    return result;
                Vector3 waypoint1 = waypoints[0];
                Vector3 waypoint2 = waypoints[0 + 1];
                result = waypoints;
                Vector3 temp = (waypoint2 - waypoint1);
                temp.Normalize();
                Vector3.Multiply(ref temp, (float) remaining, out temp);
                result.Add(waypoint1 + temp);
            }
            return result;
        }

        private static Vector3 CalculateTargetPosition(Obj_AI_Base hero, float delay, float radius,
                                                       float speed, Vector3 from)
        {
            var castPosition = new Vector3();
            List<Vector3> waypoints = GetWaypoints(hero);
            if (waypoints == null)
                return new Vector3();
            double wayPointsLength = GetWayPointsLength(waypoints);
            if (waypoints.Count() == 1)
            {
                castPosition = new Vector3(waypoints[0].X, waypoints[0].Y, waypoints[0].Z);
            }
            else if (wayPointsLength - delay*hero.MoveSpeed + radius >= 0)
            {
                waypoints = CutWayPoints(waypoints, delay*hero.MoveSpeed - radius);
                if (!float.IsNaN(speed) && speed.CompareTo(float.MaxValue) != 0)
                {
                    for (int i = 0; i < waypoints.Count() - 1; i++)
                    {
                        Vector3 waypoint1 = waypoints[i];
                        Vector3 waypoint2 = waypoints[i + 1];
                        if (!Utils.IsValidVector3(waypoint1) || !Utils.IsValidVector3(waypoint2))
                            continue;
                        castPosition = waypoint1;
                    }
                }
                else
                {
                    castPosition = new Vector3(waypoints[waypoints.Count() - 1].X, waypoints[waypoints.Count() - 1].Y,
                                               waypoints[waypoints.Count() - 1].Z);
                }
            }
            else if (hero.Type != ObjectManager.Player.Type)
            {
                castPosition = new Vector3(waypoints[waypoints.Count() - 1].X, waypoints[waypoints.Count() - 1].Y,
                                           waypoints[waypoints.Count() - 1].Z);
            }

            return castPosition;
        }

        private static Object[] WayPointAnalysis(Obj_AI_Base unit, float delay, float radius, float speed,
                                                 Vector3 from, float range, SpellType spellType)
        {
            Vector3 castPosition;
            int hitChance = 1;
            List<Vector3> waypoints = GetWaypoints(unit);

            Vector3 vec3 = CalculateTargetPosition(unit, delay, radius, speed, from);
            if (waypoints == null || waypoints.Count() == 0 || !Utils.IsValidVector3(vec3))
                return null;
            castPosition = vec3;

            if (CountWaypoints(unit) >= 1)
                hitChance = 2;


            float angle = MaxAngle(unit, waypoints[waypoints.Count() - 1], unit.ServerPosition);
            if (angle > 90)
                hitChance = 1;
            else if (angle < 30 && CountWaypoints(unit) >= 1)
                hitChance = 2;

            if (CountWaypoints(unit) == 0)
                hitChance = 2;

            hitChance = 2; //<-----MENU ENTRY LATER

            if (Utils.IsValidVector3(castPosition) &&
                (radius/unit.MoveSpeed >= delay + Utils.GetDistance(from, castPosition)/speed))
                hitChance = 3;

            if (
                Utils.AngleBetween(new Vector3(from.X, from.Y, from.Z),
                                   new Vector3(unit.ServerPosition.X, unit.ServerPosition.Y, unit.ServerPosition.Z),
                                   castPosition) > 60)
                hitChance = 1;

            if (dontShoot.ContainsKey(unit.NetworkId))
            {
                float dontShootTime;
                dontShoot.TryGetValue(unit.NetworkId, out dontShootTime);
                if (dontShootTime > Game.Time)
                {
                    hitChance = 0;
                }
            }

            if (dontShoot2.ContainsKey(unit.NetworkId))
            {
                float dontShoot2Time;
                dontShoot2.TryGetValue(unit.NetworkId, out dontShoot2Time);
                if (dontShoot2Time > Game.Time)
                {
                    castPosition = unit.ServerPosition;
                    hitChance = 1;
                }
            }

            if (IsSlowed(unit, delay, speed, from))
            {
                hitChance = 2;
            }

            Object[] immobile = IsImmobile(unit, delay, radius, speed, from, spellType);
            bool bImmobile = (bool)immobile[0];
            if (bImmobile)
            {
                castPosition = (Vector3)immobile[2];
                hitChance = 4;
            }

            Object[] dashing = IsDashing(unit, delay, radius, speed, from);
            bool bDashing = (bool)dashing[0];
            if (bDashing)
            {
                if ((bool)dashing[1])
                {
                    hitChance = 5;
                }
                else
                {
                    hitChance = 0;
                }
                castPosition = (Vector3)dashing[2];
            }

            if (Utils.GetDistance(ObjectManager.Player.ServerPosition, unit.ServerPosition) < 250)
            {
                hitChance = 2;
                Vector3 object2 = CalculateTargetPosition(unit, delay*0.5f, radius, speed*2, from);
                if (object2 == null)
                    return null;
                castPosition = object2;
            }

            return new Object[2] {castPosition, hitChance};
        }

        public static BestPrediction GetBestPosition(Obj_AI_Base unit, float delay, float radius, float speed,
                                                     Vector3 from, float range, bool collision,
                                                     SpellType spelltype)
        {
            if (unit == null || unit.IsDead || !unit.IsVisible) //Remove !unit.IsVisible later for fow calc
            {
                return new BestPrediction();
            }
            if (Utils.IsValidFloat(range))
            {
                range = range - 10;
            }
            else
            {
                range = float.MaxValue;
            }
            if (radius.CompareTo(0) == 0)
            {
                radius = 1;
            }
            else
            {
                radius = radius + GetHitBox(unit) - 4;
            }
            if (!Utils.IsValidFloat(speed))
            {
                speed = float.MaxValue;
            }
            if (Utils.IsValidVector3(from) == false)
            {
                from = ObjectManager.Player.ServerPosition;
            }

            bool fromMyHero;
            if (Utils.GetDistanceSqr(from, ObjectManager.Player.ServerPosition) < 50*50)
            {
                fromMyHero = true;
            }
            else
            {
                fromMyHero = false;
            }
            delay = delay + ((float) 0.07 + Game.Ping/2000);
            Vector3 position = new Vector3(), castPosition = new Vector3();
            int hitChance = -1;
            if (unit.Type != ObjectManager.Player.Type)
            {
                Vector3 vec3 = CalculateTargetPosition(unit, delay, radius, speed, from);
                if (Utils.IsValidVector3(vec3) == false)
                {
                    return new BestPrediction();
                }
                castPosition = vec3;
                hitChance = 2;
            }
            else
            {
                Object[] object2 = WayPointAnalysis(unit, delay, radius, speed, from, range, spelltype);
                if (object2 != null)
                {
                    castPosition = (Vector3) object2[0];
                    hitChance = (int) object2[1];
                }

                if (fromMyHero)
                {
                    if (spelltype == SpellType.LINE && Utils.GetDistanceSqr(from, castPosition) >= range*range)
                    {
                        hitChance = 0;
                    }
                    else if (spelltype == SpellType.CIRCULAR &&
                             (Utils.GetDistanceSqr(from, castPosition) >= Math.Pow(range + radius, 2)))
                    {
                        hitChance = 0;
                    }
                    else if (spelltype == SpellType.RING) //buggy calc
                    {
                        Vector3[] nVec = CalcRing(castPosition, castPosition, radius);
                        if (Utils.GetDistance(nVec[0], from) <= range)
                        {
                            castPosition = nVec[0];
                        }
                        else if (Utils.GetDistance(nVec[1], from) <= range)
                        {
                            castPosition = nVec[1];
                        }
                        else
                        {
                            hitChance = 0;
                        }
                        //Vector3 dVector = from - castPosition;
                        //dVector.Normalize();
                        //dVector = Vector3.Multiply(dVector, radius);
                        //castPosition = new Vector3(castPosition.X + dVector.X, castPosition.Y + dVector.Y, castPosition.Z + dVector.Z);
                        //if (Utils.GetDistance(fromn, castPosition) >= range)
                        //{
                        //    hitChance = 0;
                        //}
                    }
                    else if (Utils.GetDistanceSqr(from, castPosition) > Math.Pow(range, 2))
                    {
                        hitChance = 0;
                    }
                }
            }

            Vector3 pos;
            if (Utils.IsValidVector3(castPosition) && Utils.GetDistance(castPosition, from) < range &&
                unit.IsMoving)
            {
                pos = castPosition;
                castPositionDraw = castPosition;
            }
            else if (Utils.IsValidVector3(unit.Position) && Utils.GetDistance(unit.Position, from) < range &&
                     !unit.IsMoving && spelltype != SpellType.RING)
            {
                pos = unit.ServerPosition;
                positionDraw = pos;
            }
            else
            {
                return new BestPrediction();
            }

            radius = radius - GetHitBox(unit) + 10;
            if (collision && hitChance > 0)
            {
                if (CheckMinionCollision(unit, castPosition, delay, radius, speed, from, range))
                {
                    hitChance = -1;
                }
            }

            return new BestPrediction(pos, hitChance);
        }

        private static bool CheckCollision(Obj_AI_Base unit, List<Obj_AI_Minion> minions, float delay, float radius,
                                           float speed,
                                           Vector3 from, float range)
        {
            List<Vector3> minionCircles = new List<Vector3>();
            for (int i = 0; i < minions.Count(); i++)
            {
                Obj_AI_Minion minion = minions[i];
                if (minion.IsValid)
                {
                    List<Vector3> waypoints = GetWaypoints(minion);
                    Vector3 castPosition = CalculateTargetPosition(minion, delay, radius, speed, from);
                    Vector3 castPositionHero = CalculateTargetPosition(unit, delay, radius, speed, from);

                    minionCircles.Add(castPosition);
                    drawMinionCircles = minionCircles;

                    if (Utils.GetDistanceSqr(from, castPosition) <= Math.Pow(range, 2) &&
                        Utils.GetDistanceSqr(from, minion.ServerPosition) <= Math.Pow(range + 100, 2))
                    {
                        Vector3 temp = new Vector3();
                        Vector3 pos = new Vector3();
                        if (minion.IsMoving)
                        {
                            pos = castPosition;
                        }
                        else
                        {
                            pos = minion.ServerPosition;
                        }
                        Vector3 posHero = new Vector3();
                        if (unit.IsMoving)
                        {
                            posHero = castPositionHero;
                        }
                        else
                        {
                            posHero = unit.ServerPosition;
                        }
                        if (waypoints.Count > 1 && Utils.IsValidVector3(waypoints[0]) &&
                            Utils.IsValidVector3(waypoints[1]))
                        {
                            Object[] objects1 = Utils.VectorPointProjectionOnLineSegment(from, posHero, pos);
                            Vector3 pointSegment1 = (Vector3) objects1[0];
                            Vector3 pointLine1 = (Vector3) objects1[1];
                            bool isOnSegment1 = (bool) objects1[2];
                            if (isOnSegment1 &&
                                (Utils.GetDistanceSqr(pos, pointSegment1) <=
                                 Math.Pow(GetHitBox(minion) + radius + 20, 2)))
                            {
                                return true;
                            }
                        }
                        Object[] objects = Utils.VectorPointProjectionOnLineSegment(from, posHero, pos);
                        Vector3 pointSegment = (Vector3) objects[0];
                        Vector3 pointLine = (Vector3) objects[1];
                        bool isOnSegment = (bool) objects[2];
                        if (isOnSegment &&
                            (Utils.GetDistanceSqr(pos, pointSegment) <= Math.Pow(GetHitBox(minion) + radius + 20, 2)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        private static bool CheckMinionCollision(Obj_AI_Base unit, Vector3 Pos, float delay, float radius, float speed,
                                                 Vector3 from, float range)
        {
            List<Obj_AI_Minion> minions = new List<Obj_AI_Minion>();
            foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>())
            {
                if (minion.Team != ObjectManager.Player.Team &&
                    Utils.GetDistance(from, minion.ServerPosition) < range + 500*(delay + range/speed))
                {
                    if (minion.IsValid)
                        minions.Add(minion);
                }
            }

            drawMinions = minions;
            if (CheckCollision(unit, minions, delay, radius, speed, from, range))
            {
                return true;
            }
            return false;
        }

        public static BestPredictionAOE GetBestAOEPosition(Obj_AI_Base unit, float delay, float radius, float speed,
                                                           Vector3 from, float range, bool collision,
                                                           SpellTypeAOE spelltype, float width = 10)
        {
            if (!unit.IsVisible)
                return new BestPredictionAOE();
            BestPredictionAOE objects = new BestPredictionAOE();
            switch (spelltype)
            {
                case SpellTypeAOE.LINE:
                    objects = GetLineAOECastPosition(unit, delay, radius, speed, from, range, collision);
                    break;

                case SpellTypeAOE.CIRCULAR:
                    objects = GetCircularAOECastPosition(unit, delay, radius, speed, from, range, collision);
                    break;

                case SpellTypeAOE.CONE:
                    objects = GetConeAOECastPosition(unit, delay, radius, speed, from, range, collision);
                    break;

                case SpellTypeAOE.RING:
                    objects = GetRingAOECastPosition(unit, delay, radius, speed, from, range, collision, width);
                    break;
            }

            return objects;
        }

        private static List<PointF> Vector3ToPointF(List<Vector3> vector3s)
        {
            List<PointF> pointFs = new List<PointF>();
            foreach (Vector3 vector3 in vector3s)
            {
                pointFs.Add(new PointF(vector3.X, vector3.Y));
            }
            return pointFs;
        }

        private static List<Vector3> PointFToVector3(List<PointF> pointFs)
        {
            List<Vector3> vector3s = new List<Vector3>();
            foreach (PointF pointF in pointFs)
            {
                vector3s.Add(new Vector3(pointF.X, pointF.Y, 0));
            }
            return vector3s;
        }

        private static Vector3[] CalcRing(Vector3 pos1, Vector3 pos2, float radius)
        {
            Vector3 centerPoint = new Vector3((pos2.X + pos1.X)/2, (pos2.Y + pos1.Y)/2,
                                              (pos2.Z + pos1.Z)/2);
            Vector3 perpendicular = new Vector3(pos2.X - pos1.X, pos2.Y - pos1.Y, pos2.Z - pos1.Z);
            perpendicular.Normalize();
            perpendicular = Utils.perpendicular(perpendicular); //maybe need change with line below

            float distance = (float) Utils.GetDistance(pos2, pos1)/2;
            float a = (float) Math.Sqrt(radius*radius - distance*distance);
            Vector3 sPos1 = centerPoint + a*perpendicular;
            Vector3 sPos2 = centerPoint - a*perpendicular;
            return new[] {sPos1, sPos2};
        }

        private static BestPredictionAOE GetRingAOECastPosition(Obj_AI_Base unit, float delay, float radius, float speed,
                                                                Vector3 from, float range, bool collision, float width)
        {
            if (range.CompareTo(0) != 0)
            {
                range = range - 4;
            }
            else
            {
                range = 20000;
            }
            BestPrediction objects1 = GetBestPosition(unit, delay, width, speed, from, range, collision, SpellType.RING);
            if (objects1 == null || !objects1.IsValid())
            {
                return new BestPredictionAOE();
            }
            Vector3 mainCastPosition = objects1.castPosition;
            int mainHitChance = objects1.hitChance;
            List<Vector3> points = new List<Vector3>();
            points.Add(mainCastPosition);
            //points.Add(from);
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy && hero.NetworkId != unit.NetworkId && !hero.IsDead && hero.IsValid &&
                    Utils.GetDistanceSqr(hero.ServerPosition, ObjectManager.Player.ServerPosition) <=
                    (range*1.5)*(range*1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, width, speed, from, range, collision,
                                                              SpellType.RING);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistanceSqr(from, castPosition2) <= (range*radius))
                    {
                        points.Add(castPosition2);
                        //points.Add(hero.ServerPosition);
                    }
                }
            }

            if (points.Count > 2)
            {
                points.RemoveRange(2, points.Count - 2);
            }

            if (points.Count == 2)
            {
                Vector3 pos1 = points[0];
                Vector3 pos2 = points[1];
                if (Utils.GetDistance(pos1, pos2) <= radius*2 && Utils.GetDistance(pos1, pos2).CompareTo(0) != 0)
                {
                    Vector3[] nVec = CalcRing(pos2, pos1, radius);
                    Vector3 sPos1 = nVec[0];
                    Vector3 sPos2 = nVec[1];
                    positionRingDraw1 = sPos1;
                    positionRingDraw2 = sPos2;
                    if (Utils.GetDistance(sPos1, from) <= range)
                    {
                        positionRingDraw = sPos1;
                        return new BestPredictionAOE(sPos1, mainHitChance, points.Count, points);
                    }
                    else if (Utils.GetDistance(sPos2, from) <= range)
                    {
                        positionRingDraw = sPos2;
                        return new BestPredictionAOE(sPos2, mainHitChance, points.Count, points);
                    }
                }
                points.RemoveAt(1);
            }

            objects1 = GetBestPosition(unit, delay, radius, speed, from, range, collision, SpellType.RING);
            if (objects1 == null || !objects1.IsValid())
            {
                return new BestPredictionAOE();
            }
            mainCastPosition = objects1.castPosition;
            mainHitChance = objects1.hitChance;
            return new BestPredictionAOE(mainCastPosition, mainHitChance, points.Count, points);
        }

        private static BestPredictionAOE GetCircularAOECastPosition(Obj_AI_Base unit, float delay, float radius,
                                                                    float speed,
                                                                    Vector3 from, float range, bool collision)
        {
            BestPrediction objects1 = GetBestPosition(unit, delay, radius, speed, from, range, collision,
                                                      SpellType.CIRCULAR);
            if (objects1 == null || !objects1.IsValid())
            {
                return new BestPredictionAOE();
            }
            Vector3 mainCastPosition = objects1.castPosition;
            int mainHitChance = objects1.hitChance;
            List<Vector3> points = new List<Vector3>();
            points.Add(mainCastPosition);

            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy && hero.NetworkId != unit.NetworkId && !hero.IsDead && hero.IsValid &&
                    Utils.GetDistanceSqr(hero.ServerPosition, ObjectManager.Player.ServerPosition) <=
                    (range*1.5)*(range*1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision,
                                                              SpellType.CIRCULAR);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistance(from, castPosition2) < (range + radius))
                    {
                        points.Add(castPosition2);
                    }
                }
            }

            while (points.Count > 1)
            {
                Object[] objects2 = Utils.ComputeMEC(points);
                Vector3 center2 = (Vector3) objects2[0];
                float radius2 = (float) objects2[1];
                Vector3 radiusPoint2 = (Vector3) objects2[2];

                if (radius2 <= radius + GetHitBox(unit) - 8)
                {
                    positionCircularDraw = center2;
                    return new BestPredictionAOE(center2, mainHitChance, points.Count, points);
                }

                float maxdist = -1;
                int maxdistindex = 0;

                for (int i = 1; i < points.Count - 1; i++)
                {
                    float distance = (float) Utils.GetDistanceSqr(points[i], points[0]);
                    if (distance > maxdist || maxdist.CompareTo(-1) == 0)
                    {
                        maxdistindex = i;
                        maxdist = distance;
                    }
                }

                points.RemoveAt(maxdistindex);
            }
            positionCircularDraw = mainCastPosition;
            return new BestPredictionAOE(mainCastPosition, mainHitChance, points.Count, points);
        }

        private static Vector3[] GetPossiblePoints(Vector3 from, Vector3 pos, float width, float range)
        {
            Vector3 middlePoint = (from + pos)/2;
            Vector3[] vectors = Utils.CircleCircleIntersection(from, middlePoint, width,
                                                               (float) Utils.GetDistance(middlePoint, from));
            Vector3 P1 = vectors[0];
            Vector3 P2 = vectors[1];

            Vector3 V1 = (P1 - from);
            Vector3 V2 = (P2 - from);

            V1 = (pos - V1 - from);
            V1.Normalize();
            Vector3.Multiply(V1, range);
            V1 = V1 + from;
            V2 = (pos - V2 - from);
            V2.Normalize();
            Vector3.Multiply(V2, range);
            V2 = V2 + from;
            return new[] {V1, V2};
        }

        private static Object[] CountHits(Vector3 P1, Vector3 P2, float width, List<Vector3> points)
        {
            int hits = 0;
            List<Vector3> nPoints = new List<Vector3>();
            width = width + 2;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                Object[] objects = Utils.VectorPointProjectionOnLineSegment(P1, P2, point);
                Vector3 pointSegment = (Vector3) objects[0];
                bool isOnSegment = (bool) objects[2];
                if (isOnSegment && Utils.GetDistanceSqr(pointSegment, point) <= width*width)
                {
                    hits = hits + 1;
                    nPoints.Add(point);
                }
                else if (i == 0)
                {
                    return new Object[] {hits, nPoints};
                }
            }
            return new Object[] {hits, nPoints};
        }

        private static BestPredictionAOE GetLineAOECastPosition(Obj_AI_Base unit, float delay, float radius, float speed,
                                                                Vector3 from, float range, bool collision)
        {
            BestPrediction objects1 = GetBestPosition(unit, delay, radius, speed, from, range, collision, SpellType.LINE);
            if (objects1 == null || !objects1.IsValid())
            {
                return new BestPredictionAOE();
            }
            Vector3 mainCastPosition = objects1.castPosition;
            int mainHitChance = objects1.hitChance;
            List<Vector3> points = new List<Vector3>();
            points.Add(mainCastPosition);

            if (range.CompareTo(0) != 0)
            {
                range = range - 4;
            }
            else
            {
                range = 20000;
            }

            if (radius.CompareTo(0) == 0)
            {
                radius = radius + GetHitBox(unit) - 4;
            }
            else
            {
                radius = 1;
            }
            if (!Utils.IsValidVector3(from))
            {
                from = ObjectManager.Player.ServerPosition;
            }

            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy && hero.NetworkId != unit.NetworkId && !hero.IsDead && hero.IsValid &&
                    Utils.GetDistanceSqr(hero.ServerPosition, ObjectManager.Player.ServerPosition) <=
                    (range*1.5)*(range*1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision,
                                                              SpellType.LINE);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistance(from, castPosition2) < (range + radius))
                    {
                        points.Add(castPosition2);
                    }
                }
            }

            int maxHit = 1;
            Vector3 maxHitPos = new Vector3();
            List<Vector3> maxHitPoints = new List<Vector3>();

            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3[] possiblePoints = GetPossiblePoints(from, points[i], radius - 20, range);
                    Vector3 C1 = possiblePoints[0], C2 = possiblePoints[1];
                    Object[] countHits1 = CountHits(from, C1, radius, points);
                    Object[] countHits2 = CountHits(from, C2, radius, points);
                    if ((int) countHits1[0] >= maxHit)
                    {
                        maxHitPos = C1;
                        maxHit = (int) countHits1[0];
                        maxHitPoints = (List<Vector3>) countHits1[1];
                    }
                    if ((int) countHits2[0] >= maxHit)
                    {
                        maxHitPos = C2;
                        maxHit = (int) countHits2[0];
                        maxHitPoints = (List<Vector3>) countHits2[1];
                    }
                }
            }

            if (maxHit > 1)
            {
                float maxDistance = -1;
                Vector3 p1 = new Vector3(), p2 = new Vector3();
                for (int i = 0; i < maxHitPoints.Count; i++)
                {
                    for (int j = 0; j < maxHitPoints.Count; j++)
                    {
                        Vector3 startP = from;
                        Vector3 endP = (maxHitPoints[i] + maxHitPoints[j])/2;
                        Object[] objects01 = Utils.VectorPointProjectionOnLineSegment(startP, endP, maxHitPoints[i]);
                        Vector3 pointSegment01 = (Vector3) objects01[0];
                        Vector3 pointLine01 = (Vector3) objects01[1];
                        bool isOnSegment01 = (bool) objects01[2];
                        Object[] objects02 = Utils.VectorPointProjectionOnLineSegment(startP, endP, maxHitPoints[j]);
                        Vector3 pointSegment02 = (Vector3) objects02[0];
                        Vector3 pointLine02 = (Vector3) objects02[1];
                        bool isOnSegment02 = (bool) objects02[2];
                        float dist =
                            (float)
                            (Utils.GetDistanceSqr(maxHitPoints[i], pointLine01) +
                             Utils.GetDistanceSqr(maxHitPoints[j], pointLine02));
                        if (dist >= maxDistance)
                        {
                            maxDistance = dist;
                            p1 = maxHitPoints[i];
                            p2 = maxHitPoints[j];
                        }
                    }
                }
                positionLineDraw = (p1 + p2)/2;
                return new BestPredictionAOE((p1 + p2)/2, mainHitChance, maxHit, points);
            }
            positionLineDraw = mainCastPosition;
            return new BestPredictionAOE(mainCastPosition, mainHitChance, 1, points);
        }

        private static Object[] CountVectorBetween(Vector3 V1, Vector3 V2, List<Vector3> points)
        {
            int result = 0;
            List<Vector3> hitpoints = new List<Vector3>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 t = points[i];
                Vector3 NVector = Utils.Vector3CrossP(V1, t);
                Vector3 NVector2 = Utils.Vector3CrossP(t, V2);
                if (NVector.Y >= 0 && NVector2.Y >= 0)
                {
                    result = result + 1;
                    hitpoints.Add(t);
                }
                else if (i == 0)
                {
                    return new object[] {-1, hitpoints};
                }
            }
            return new object[] {result, hitpoints};
        }

        private static Object[] CheckHit(Vector3 position, float angle, List<Vector3> points)
        {
            Vector3 direction = new Vector3();
            Vector3.Normalize(ref position, out direction);
            Vector3 v1 = Utils.Vector3Rotate(position, 0, -angle/2, 0);
            Vector3 v2 = Utils.Vector3Rotate(position, 0, angle/2, 0);
            return CountVectorBetween(v1, v2, points);
        }

        private static BestPredictionAOE GetConeAOECastPosition(Obj_AI_Base unit, float delay, float angle, float speed,
                                                                Vector3 from, float range, bool collision)
        {
            if (range.CompareTo(0) != 0)
            {
                range = range - 4;
            }
            else
            {
                range = 20000;
            }
            float radius = 1;
            if (!(angle < Math.PI*2))
            {
                angle = (float) (angle*Math.PI/180);
            }
            BestPrediction objects1 = GetBestPosition(unit, delay, radius, speed, from, range, collision, SpellType.LINE);
            if (objects1 == null || !objects1.IsValid())
            {
                return new BestPredictionAOE();
            }
            Vector3 mainCastPosition = objects1.castPosition;
            int mainHitChance = objects1.hitChance;
            List<Vector3> points = new List<Vector3>();
            points.Add(mainCastPosition);
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy && hero.NetworkId != unit.NetworkId && !hero.IsDead && hero.IsValid &&
                    Utils.GetDistanceSqr(hero.ServerPosition, ObjectManager.Player.ServerPosition) <=
                    (range*1.5)*(range*1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision,
                                                              SpellType.LINE);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistanceSqr(from, castPosition2) < (range*range))
                    {
                        points.Add(castPosition2);
                    }
                }
            }

            int maxHit = 1;
            Vector3 maxHitPos = new Vector3();
            List<Vector3> maxHitPoints = new List<Vector3>();

            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 point = points[i];
                    Vector3 pos1 = Utils.Vector3Rotate(point, 0, angle/2, 0);
                    Vector3 pos2 = Utils.Vector3Rotate(point, 0, -angle/2, 0);

                    Object[] objects3 = CheckHit(pos1, angle, points);
                    int hits3 = (int) objects3[0];
                    List<Vector3> points3 = (List<Vector3>) objects3[1];
                    Object[] objects4 = CheckHit(pos2, angle, points);
                    int hits4 = (int) objects4[0];
                    List<Vector3> points4 = (List<Vector3>) objects4[1];

                    if (hits3 >= maxHit)
                    {
                        maxHitPos = pos1;
                        maxHit = hits3;
                        maxHitPoints = points3;
                    }
                    if (hits4 >= maxHit)
                    {
                        maxHitPos = pos2;
                        maxHit = hits4;
                        maxHitPoints = points4;
                    }
                }
            }

            if (maxHit > 1)
            {
                float maxangle = -1;
                Vector3 p1 = new Vector3();
                Vector3 p2 = new Vector3();
                for (int i = 0; i < maxHitPoints.Count; i++)
                {
                    Vector3 hitp = maxHitPoints[i];
                    for (int j = 0; j < maxHitPoints.Count; j++)
                    {
                        Vector3 hitp2 = maxHitPoints[j];
                        float cangle = Utils.AngleBetween(new Vector3(), hitp2, hitp);
                        if (cangle > maxangle)
                        {
                            maxangle = cangle;
                            p1 = hitp;
                            p2 = hitp2;
                        }
                    }
                }
                Vector3 temp = (((p1) + (p2))/2);
                positionConeDraw = temp;
                return new BestPredictionAOE(temp, mainHitChance, maxHit, points);
            }
            else
            {
                return new BestPredictionAOE(mainCastPosition, mainHitChance, 1, points);
            }
        }

        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs spell)
        {
            if (unit.Type == ObjectManager.Player.Type)
            {
                foreach (KeyValuePair<string, float> spell1 in spells)
                {
                    if (spell1.Key.Contains(spell.SData.Name.ToLower()))
                    {
                        UpdateDictionaries(targetsImmobile, unit.NetworkId, Game.Time + spell1.Value);
                        return;
                    }
                }

                foreach (KeyValuePair<string, Blinks> spell1 in blinks)
                {
                    Vector3 landingPos = Utils.GetDistance(unit.ServerPosition, spell.End) < spell1.Value.range
                                             ? spell.End
                                             : unit.ServerPosition;
                    if (spell1.Key.Contains(spell.SData.Name.ToLower()) /*&& !IsWall(spell1.End.X, spell1.End.Y, spell1.End.Z)*/)
                        //Need IsWall
                    {
                        UpdateDictionaries(targetsDashing, unit.NetworkId,
                                           new Dashes(true, spell1.Value.delay, Game.Time + spell1.Value.delay,
                                                      spell1.Value.delay2, unit.ServerPosition, landingPos));
                        return;
                    }
                }

                foreach (KeyValuePair<string, float> spell1 in blackList)
                {
                    if (spell1.Key.Contains(spell.SData.Name.ToLower()))
                    {
                        UpdateDictionaries(dontShoot, unit.NetworkId, Game.Time + spell1.Value);
                        return;
                    }
                }

                foreach (KeyValuePair<string, float> spell1 in dashes)
                {
                    if (spell1.Key.Contains(spell.SData.Name.ToLower()))
                    {
                        UpdateDictionaries(dontShoot2, unit.NetworkId, Game.Time + spell1.Value);
                        UpdateDictionaries(targetsDashing, unit.NetworkId, new Dashes(false, (float)Utils.GetDistance(spell.End, spell.Start) / spell.SData.MissileSpeed, Game.Time + (float)Utils.GetDistance(spell.End, spell.Start) / spell.SData.MissileSpeed, 0, spell.Start, spell.End));
                        return;
                    }
                }
            }
        }

        private static float GetProjectileSpeed(Obj_AI_Base unit)
        {
            foreach (KeyValuePair<string, float> projectileSpeed in projectileSpeeds)
            {
                if (projectileSpeed.Key.Contains(unit.SkinName))
                {
                    return projectileSpeed.Value;
                }
            }
            return float.MaxValue;
        }

        private static void CollisionProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs spell)
        {
            Obj_AI_Minion target = new Obj_AI_Minion();
            foreach (Obj_AI_Minion minion in ObjectManager.Get<Obj_AI_Minion>())
            {
                if (spell.End == minion.ServerPosition)
                {
                    target = minion;
                    break;
                }
            }
            if (unit.IsValid && unit.Type != ObjectManager.Player.Type && target.IsValid &&
                unit.Team == ObjectManager.Player.Team &&
                (spell.SData.Name.Contains("attack") || spell.SData.Name.Contains("frostarrow")))
            {
                if (Utils.GetDistanceSqr(unit.ServerPosition, ObjectManager.Player.ServerPosition) < 4000000)
                {
                    float time = Game.Time + spell.TimeTillPlayAnimation +
                                 (float) Utils.GetDistance(target.ServerPosition, unit.ServerPosition)/
                                 GetProjectileSpeed(unit) - Game.Ping/2000;
                    int i = 0;
                    while (i <= activeAttacks.Count())
                    {
                        if ((activeAttacks[i].attacker.IsValid && activeAttacks[i].attacker.NetworkId == unit.NetworkId) ||
                            ((activeAttacks[i].hitTime + 3) < Game.Time))
                        {
                            activeAttacks.RemoveAt(i);
                        }
                        else
                        {
                            i = i + 1;
                        }
                    }
                    activeAttacks.Add(new ActiveAttacks(unit, target, Game.Time - Game.Ping/2000,
                                                        spell.TimeTillPlayAnimation, time, unit.ServerPosition,
                                                        GetProjectileSpeed(unit),
                                                        CalcDamageOfAttack(unit, target, spell, 0), spell.AnimateCast));
                }
            }
        }

        private static float CalcDamageOfAttack(Obj_AI_Base source, Obj_AI_Base target,
                                                GameObjectProcessSpellCastEventArgs spell, float additionalDamage)
        {
            float armorPenPercent = source.PercentArmorPenetrationMod;
            float armorPen = source.FlatArmorPenetrationMod;
            //float totalDamage = source.totalDamage + additionalDamage.CompareTo(0) != 0 <------Change TotalDamage when DamageLib is rdy
            //                        ? additionalDamage
            //                        : 0;
            float damageMultiplier = spell.SData.Name.Contains("CritAttack") ? 2 : 1;

            if (source.Type == GameObjectType.obj_AI_Minion)
            {
                armorPenPercent = 1;
            }
            else if (source.Type == GameObjectType.obj_AI_Turret)
            {
                armorPenPercent = 0.7f;
            }


            if (target.Type == GameObjectType.obj_AI_Turret)
            {
                armorPenPercent = 1;
                armorPen = 0;
                damageMultiplier = 1;
            }


            float targetArmor = (target.Armor*armorPenPercent) - armorPen;
            if (targetArmor < 0)
            {
                damageMultiplier = 1*damageMultiplier;
            }
            else
            {
                damageMultiplier = 100/(100 + targetArmor)*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Hero && target.Type == GameObjectType.obj_AI_Turret)
            {
                //totalDamage = Math.Max(source.totalDamage, source.BaseAttackDamage + 0.4*source.BaseAbilityDamage); <------Change TotalDamage when DamageLib is rdy
            }

            if (source.Type == GameObjectType.obj_AI_Minion && target.Type == GameObjectType.obj_AI_Hero &&
                source.Team != GameObjectTeam.Neutral)
            {
                damageMultiplier = 0.60f*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Hero && target.Type == GameObjectType.obj_AI_Turret)
            {
                damageMultiplier = 0.95f*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Minion && target.Type == GameObjectType.obj_AI_Turret)
            {
                damageMultiplier = 0.475f*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Turret &&
                (target.SkinName == "Red_Minion_MechCannon" || target.SkinName == "Blue_Minion_MechCannon"))
            {
                damageMultiplier = 0.8f*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Turret &&
                (target.SkinName == "Red_Minion_Wizard" || target.SkinName == "Blue_Minion_Wizard" ||
                 target.SkinName == "Red_Minion_Basic" || target.SkinName == "Blue_Minion_Basic"))
            {
                damageMultiplier = (1/0.875f)*damageMultiplier;
            }

            if (source.Type == GameObjectType.obj_AI_Turret)
            {
                damageMultiplier = 1.05f*damageMultiplier;
            }

            return damageMultiplier /**totalDamage*/; //After TotalDamage fix return totalDamage
        }

        private static Object[] GetPredictedHealth(Obj_AI_Base unit, float time, float delay)
        {
            float incDamage = 0;
            int i = 0;
            float maxDamage = 0;
            int count = 0;
            if (delay.CompareTo(0) == 0)
            {
                delay = 0.07f;
            }
            while (i <= activeAttacks.Count())
            {
                if (activeAttacks[i].attacker.IsValid && !activeAttacks[i].attacker.IsDead &&
                    activeAttacks[i].target.IsValid && !activeAttacks[i].target.IsDead &&
                    activeAttacks[i].target.NetworkId == unit.NetworkId)
                {
                    float hitTime =
                        (float)
                        (activeAttacks[i].startTime + activeAttacks[i].windUpTime +
                         (Utils.GetDistance(activeAttacks[i].pos, unit.ServerPosition))/activeAttacks[i].projectileSpeed +
                         delay);
                    if (Game.Time < hitTime - delay && hitTime < Game.Time + time)
                    {
                        incDamage = incDamage + activeAttacks[i].damage;
                        count = count + 1;
                        if (activeAttacks[i].damage > maxDamage)
                        {
                            maxDamage = activeAttacks[i].damage;
                        }
                    }
                }
                i = i + 1;
            }
            return new Object[] {unit.Health - incDamage, maxDamage, count};
        }

        private static float GetPredictedHealth2(Obj_AI_Base unit, float t)
        {
            float damage = 0;
            int i = 0;
            while (i <= activeAttacks.Count())
            {
                int n = 0;
                if ((Game.Time - 0.1) <= activeAttacks[i].startTime + activeAttacks[i].animationTime &&
                    activeAttacks[i].target.IsValid && !activeAttacks[i].target.IsDead &&
                    activeAttacks[i].target.NetworkId == unit.NetworkId && activeAttacks[i].attacker.IsValid &&
                    !activeAttacks[i].attacker.IsDead)
                {
                    float fromT = activeAttacks[i].startTime;
                    float toT = t + Game.Time;
                    while (fromT < toT)
                    {
                        if (fromT >= Game.Time &&
                            (fromT +
                             (activeAttacks[i].windUpTime +
                              Utils.GetDistance(unit.ServerPosition, activeAttacks[i].pos)/
                              activeAttacks[i].projectileSpeed)) < toT)
                        {
                            n = n + 1;
                        }
                        fromT = fromT + activeAttacks[i].animationTime;
                    }
                }
                damage = damage + n*activeAttacks[i].damage;
                i = i + 1;
            }
            return unit.Health - damage;
        }

        private static void InitBlackList()
        {
            blackList.Add("aatroxq", 0.75f);
        }

        private static void InitDashes()
        {
            dashes.Add("ahritumble", 0.25f); //ahri's r
            dashes.Add("akalishadowdance", 0.25f); //akali r
            dashes.Add("headbutt", 0.25f); //alistar w
            dashes.Add("caitlynentrapment", 0.25f); //caitlyn e
            dashes.Add("carpetbomb", 0.25f); //corki w
            dashes.Add("dianateleport", 0.25f); //diana r
            dashes.Add("fizzpiercingstrike", 0.25f); //fizz q
            dashes.Add("fizzjump", 0.25f); //fizz e
            dashes.Add("gragasbodyslam", 0.25f); //gragas e
            dashes.Add("gravesmove", 0.25f); //graves e
            dashes.Add("ireliagatotsu", 0.25f); //irelia q
            dashes.Add("jarvanivdragonstrike", 0.25f); //jarvan q
            dashes.Add("jaxleapstrike", 0.25f); //jax q
            dashes.Add("khazixe", 0.25f); //khazix e and e evolved
            dashes.Add("leblancslide", 0.25f); //leblanc w
            dashes.Add("leblancslidem", 0.25f); //leblanc w (r)
            dashes.Add("blindmonkqtwo", 0.25f); //lee sin q
            dashes.Add("blindmonkwone", 0.25f); //lee sin w
            dashes.Add("luciane", 0.25f); //lucian e
            dashes.Add("maokaiunstablegrowth", 0.25f); //maokai w
            dashes.Add("nocturneparanoia2", 0.25f); //nocturne r
            dashes.Add("pantheon_leapbash", 0.25f); //pantheon e?
            dashes.Add("renektonsliceanddice", 0.25f); //renekton e                 
            dashes.Add("riventricleave", 0.25f); //riven q          
            dashes.Add("rivenfeint", 0.25f); //riven e      
            dashes.Add("sejuaniarcticassault", 0.25f); //sejuani q
            dashes.Add("shenshadowdash", 0.25f); //shen e
            dashes.Add("shyvanatransformcast", 0.25f); //shyvana r
            dashes.Add("rocketjump", 0.25f); //tristana w
            dashes.Add("slashcast", 0.25f); //tryndamere e
            dashes.Add("vaynetumble", 0.25f); //vayne q
            dashes.Add("viq", 0.25f); //vi q
            dashes.Add("monkeykingnimbus", 0.25f); //wukong q
            dashes.Add("xenzhaosweep", 0.25f); //xin xhao q
            dashes.Add("yasuodashwrapper", 0.25f); //yasuo e
        }

        private static void InitSpells()
        {
            spells.Add("katarinar", 1f); //Katarinas R
            spells.Add("drain", 1f); //Fiddle W
            spells.Add("crowstorm", 1f); //Fiddle R
            spells.Add("consume", 0.5f); //Nunu Q
            spells.Add("absolutezero", 1f); //Nunu R
            spells.Add("rocketgrab", 0.5f); //Blitzcrank Q
            spells.Add("staticfield", 0.5f); //Blitzcrank R
            spells.Add("cassiopeiapetrifyinggaze", 0.5f); //Cassio's R
            spells.Add("ezrealtrueshotbarrage", 1f); //Ezreal's R
            spells.Add("galioidolofdurand", 1f); //Galio's ?
            spells.Add("gragasdrunkenrage", 1f); //Gragas W
            spells.Add("luxmalicecannon", 1f); //Lux R
            spells.Add("reapthewhirlwind", 1f); //Jannas R
            spells.Add("jinxw", 0.6f); //jinxW
            spells.Add("jinxr", 0.6f); //jinxR
            spells.Add("missfortunebullettime", 1f); //MissFortuneR
            spells.Add("shenstandunited", 1f); //ShenR
            spells.Add("threshe", 0.4f); //ThreshE
            spells.Add("threshrpenta", 0.75f); //ThreshR
            spells.Add("infiniteduress", 1f); //Warwick R
            spells.Add("meditate", 1f); //yi W
        }

        private static void InitBlinks()
        {
            blinks.Add("ezrealarcaneshift", new Blinks(475f, 0.25f, 0.8f)); //Ezreals E
            blinks.Add("deceive", new Blinks(400f, 0.25f, 0.8f)); //Shacos Q
            blinks.Add("riftwalk", new Blinks(700f, 0.25f, 0.8f)); //KassadinR
            blinks.Add("gate", new Blinks(5500f, 1.5f, 1.5f)); //Twisted fate R
            blinks.Add("katarinae", new Blinks(float.MaxValue, 0.25f, 0.8f)); //Katarinas E
            blinks.Add("elisespideredescent", new Blinks(float.MaxValue, 0.25f, 0.8f)); //Elise E
            blinks.Add("elisespidere", new Blinks(float.MaxValue, 0.25f, 0.8f)); //Elise insta E
        }

        private static void InitProjectileSpeeds()
        {
            projectileSpeeds.Add("Velkoz", 2000f);
            projectileSpeeds.Add("TeemoMushroom", float.MaxValue);
            projectileSpeeds.Add("TestCubeRender", float.MaxValue);
            projectileSpeeds.Add("Xerath", 2000.0000f);
            projectileSpeeds.Add("Kassadin", float.MaxValue);
            projectileSpeeds.Add("Rengar", float.MaxValue);
            projectileSpeeds.Add("Thresh", 1000.0000f);
            projectileSpeeds.Add("Ziggs", 1500.0000f);
            projectileSpeeds.Add("ZyraPassive", 1500.0000f);
            projectileSpeeds.Add("ZyraThornPlant", 1500.0000f);
            projectileSpeeds.Add("KogMaw", 1800.0000f);
            projectileSpeeds.Add("HeimerTBlue", 1599.3999f);
            projectileSpeeds.Add("EliseSpider", 500.0000f);
            projectileSpeeds.Add("Skarner", 500.0000f);
            projectileSpeeds.Add("ChaosNexus", 500.0000f);
            projectileSpeeds.Add("Katarina", 467.0000f);
            projectileSpeeds.Add("Riven", 347.79999f);
            projectileSpeeds.Add("SightWard", 347.79999f);
            projectileSpeeds.Add("HeimerTYellow", 1599.3999f);
            projectileSpeeds.Add("Ashe", 2000.0000f);
            projectileSpeeds.Add("VisionWard", 2000.0000f);
            projectileSpeeds.Add("TT_NGolem2", float.MaxValue);
            projectileSpeeds.Add("ThreshLantern", float.MaxValue);
            projectileSpeeds.Add("TT_Spiderboss", float.MaxValue);
            projectileSpeeds.Add("OrderNexus", float.MaxValue);
            projectileSpeeds.Add("Soraka", 1000.0000f);
            projectileSpeeds.Add("Jinx", 2750.0000f);
            projectileSpeeds.Add("TestCubeRenderwCollision", 2750.0000f);
            projectileSpeeds.Add("Red_Minion_Wizard", 650.0000f);
            projectileSpeeds.Add("JarvanIV", 20.0000f);
            projectileSpeeds.Add("Blue_Minion_Wizard", 650.0000f);
            projectileSpeeds.Add("TT_ChaosTurret2", 1200.0000f);
            projectileSpeeds.Add("TT_ChaosTurret3", 1200.0000f);
            projectileSpeeds.Add("TT_ChaosTurret1", 1200.0000f);
            projectileSpeeds.Add("ChaosTurretGiant", 1200.0000f);
            projectileSpeeds.Add("Dragon", 1200.0000f);
            projectileSpeeds.Add("LuluSnowman", 1200.0000f);
            projectileSpeeds.Add("Worm", 1200.0000f);
            projectileSpeeds.Add("ChaosTurretWorm", 1200.0000f);
            projectileSpeeds.Add("TT_ChaosInhibitor", 1200.0000f);
            projectileSpeeds.Add("ChaosTurretNormal", 1200.0000f);
            projectileSpeeds.Add("AncientGolem", 500.0000f);
            projectileSpeeds.Add("ZyraGraspingPlant", 500.0000f);
            projectileSpeeds.Add("HA_AP_OrderTurret3", 1200.0000f);
            projectileSpeeds.Add("HA_AP_OrderTurret2", 1200.0000f);
            projectileSpeeds.Add("Tryndamere", 347.79999f);
            projectileSpeeds.Add("OrderTurretNormal2", 1200.0000f);
            projectileSpeeds.Add("Singed", 700.0000f);
            projectileSpeeds.Add("OrderInhibitor", 700.0000f);
            projectileSpeeds.Add("Diana", 347.79999f);
            projectileSpeeds.Add("HA_FB_HealthRelic", 347.79999f);
            projectileSpeeds.Add("TT_OrderInhibitor", 347.79999f);
            projectileSpeeds.Add("GreatWraith", 750.0000f);
            projectileSpeeds.Add("Yasuo", 347.79999f);
            projectileSpeeds.Add("OrderTurretDragon", 1200.0000f);
            projectileSpeeds.Add("OrderTurretNormal", 1200.0000f);
            projectileSpeeds.Add("LizardElder", 500.0000f);
            projectileSpeeds.Add("HA_AP_ChaosTurret", 1200.0000f);
            projectileSpeeds.Add("Ahri", 1750.0000f);
            projectileSpeeds.Add("Lulu", 1450.0000f);
            projectileSpeeds.Add("ChaosInhibitor", 1450.0000f);
            projectileSpeeds.Add("HA_AP_ChaosTurret3", 1200.0000f);
            projectileSpeeds.Add("HA_AP_ChaosTurret2", 1200.0000f);
            projectileSpeeds.Add("ChaosTurretWorm2", 1200.0000f);
            projectileSpeeds.Add("TT_OrderTurret1", 1200.0000f);
            projectileSpeeds.Add("TT_OrderTurret2", 1200.0000f);
            projectileSpeeds.Add("TT_OrderTurret3", 1200.0000f);
            projectileSpeeds.Add("LuluFaerie", 1200.0000f);
            projectileSpeeds.Add("HA_AP_OrderTurret", 1200.0000f);
            projectileSpeeds.Add("OrderTurretAngel", 1200.0000f);
            projectileSpeeds.Add("YellowTrinketUpgrade", 1200.0000f);
            projectileSpeeds.Add("MasterYi", float.MaxValue);
            projectileSpeeds.Add("Lissandra", 2000.0000f);
            projectileSpeeds.Add("ARAMOrderTurretNexus", 1200.0000f);
            projectileSpeeds.Add("Draven", 1700.0000f);
            projectileSpeeds.Add("FiddleSticks", 1750.0000f);
            projectileSpeeds.Add("SmallGolem", float.MaxValue);
            projectileSpeeds.Add("ARAMOrderTurretFront", 1200.0000f);
            projectileSpeeds.Add("ChaosTurretTutorial", 1200.0000f);
            projectileSpeeds.Add("NasusUlt", 1200.0000f);
            projectileSpeeds.Add("Maokai", float.MaxValue);
            projectileSpeeds.Add("Wraith", 750.0000f);
            projectileSpeeds.Add("Wolf", float.MaxValue);
            projectileSpeeds.Add("Sivir", 1750.0000f);
            projectileSpeeds.Add("Corki", 2000.0000f);
            projectileSpeeds.Add("Janna", 1200.0000f);
            projectileSpeeds.Add("Nasus", float.MaxValue);
            projectileSpeeds.Add("Golem", float.MaxValue);
            projectileSpeeds.Add("ARAMChaosTurretFront", 1200.0000f);
            projectileSpeeds.Add("ARAMOrderTurretInhib", 1200.0000f);
            projectileSpeeds.Add("LeeSin", float.MaxValue);
            projectileSpeeds.Add("HA_AP_ChaosTurretTutorial", 1200.0000f);
            projectileSpeeds.Add("GiantWolf", float.MaxValue);
            projectileSpeeds.Add("HA_AP_OrderTurretTutorial", 1200.0000f);
            projectileSpeeds.Add("YoungLizard", 750.0000f);
            projectileSpeeds.Add("Jax", 400.0000f);
            projectileSpeeds.Add("LesserWraith", float.MaxValue);
            projectileSpeeds.Add("Blitzcrank", float.MaxValue);
            projectileSpeeds.Add("ARAMChaosTurretInhib", 1200.0000f);
            projectileSpeeds.Add("Shen", 400.0000f);
            projectileSpeeds.Add("Nocturne", float.MaxValue);
            projectileSpeeds.Add("Sona", 1500.0000f);
            projectileSpeeds.Add("ARAMChaosTurretNexus", 1200.0000f);
            projectileSpeeds.Add("YellowTrinket", 1200.0000f);
            projectileSpeeds.Add("OrderTurretTutorial", 1200.0000f);
            projectileSpeeds.Add("Caitlyn", 2500.0000f);
            projectileSpeeds.Add("Trundle", 347.79999f);
            projectileSpeeds.Add("Malphite", 1000.0000f);
            projectileSpeeds.Add("Mordekaiser", float.MaxValue);
            projectileSpeeds.Add("ZyraSeed", float.MaxValue);
            projectileSpeeds.Add("Vi", 1000.0000f);
            projectileSpeeds.Add("Tutorial_Red_Minion_Wizard", 650.0000f);
            projectileSpeeds.Add("Renekton", float.MaxValue);
            projectileSpeeds.Add("Anivia", 1400.0000f);
            projectileSpeeds.Add("Fizz", float.MaxValue);
            projectileSpeeds.Add("Heimerdinger", 1500.0000f);
            projectileSpeeds.Add("Evelynn", 467.0000f);
            projectileSpeeds.Add("Rumble", 347.79999f);
            projectileSpeeds.Add("Leblanc", 1700.0000f);
            projectileSpeeds.Add("Darius", float.MaxValue);
            projectileSpeeds.Add("OlafAxe", float.MaxValue);
            projectileSpeeds.Add("Viktor", 2300.0000f);
            projectileSpeeds.Add("XinZhao", 20.0000f);
            projectileSpeeds.Add("Orianna", 1450.0000f);
            projectileSpeeds.Add("Vladimir", 1400.0000f);
            projectileSpeeds.Add("Nidalee", 1750.0000f);
            projectileSpeeds.Add("Tutorial_Red_Minion_Basic", float.MaxValue);
            projectileSpeeds.Add("ZedShadow", 467.0000f);
            projectileSpeeds.Add("Syndra", 1800.0000f);
            projectileSpeeds.Add("Zac", 1000.0000f);
            projectileSpeeds.Add("Olaf", 347.79999f);
            projectileSpeeds.Add("Veigar", 1100.0000f);
            projectileSpeeds.Add("Twitch", 2500.0000f);
            projectileSpeeds.Add("Alistar", float.MaxValue);
            projectileSpeeds.Add("Akali", 467.0000f);
            projectileSpeeds.Add("Urgot", 1300.0000f);
            projectileSpeeds.Add("Leona", 347.79999f);
            projectileSpeeds.Add("Talon", float.MaxValue);
            projectileSpeeds.Add("Karma", 1500.0000f);
            projectileSpeeds.Add("Jayce", 347.79999f);
            projectileSpeeds.Add("Galio", 1000.0000f);
            projectileSpeeds.Add("Shaco", float.MaxValue);
            projectileSpeeds.Add("Taric", float.MaxValue);
            projectileSpeeds.Add("TwistedFate", 1500.0000f);
            projectileSpeeds.Add("Varus", 2000.0000f);
            projectileSpeeds.Add("Garen", 347.79999f);
            projectileSpeeds.Add("Swain", 1600.0000f);
            projectileSpeeds.Add("Vayne", 2000.0000f);
            projectileSpeeds.Add("Fiora", 467.0000f);
            projectileSpeeds.Add("Quinn", 2000.0000f);
            projectileSpeeds.Add("Kayle", float.MaxValue);
            projectileSpeeds.Add("Blue_Minion_Basic", float.MaxValue);
            projectileSpeeds.Add("Brand", 2000.0000f);
            projectileSpeeds.Add("Teemo", 1300.0000f);
            projectileSpeeds.Add("Amumu", 500.0000f);
            projectileSpeeds.Add("Annie", 1200.0000f);
            projectileSpeeds.Add("Odin_Blue_Minion_caster", 1200.0000f);
            projectileSpeeds.Add("Elise", 1600.0000f);
            projectileSpeeds.Add("Nami", 1500.0000f);
            projectileSpeeds.Add("Poppy", 500.0000f);
            projectileSpeeds.Add("AniviaEgg", 500.0000f);
            projectileSpeeds.Add("Tristana", 2250.0000f);
            projectileSpeeds.Add("Graves", 3000.0000f);
            projectileSpeeds.Add("Morgana", 1600.0000f);
            projectileSpeeds.Add("Gragas", float.MaxValue);
            projectileSpeeds.Add("MissFortune", 2000.0000f);
            projectileSpeeds.Add("Warwick", float.MaxValue);
            projectileSpeeds.Add("Cassiopeia", 1200.0000f);
            projectileSpeeds.Add("Tutorial_Blue_Minion_Wizard", 650.0000f);
            projectileSpeeds.Add("DrMundo", float.MaxValue);
            projectileSpeeds.Add("Volibear", 467.0000f);
            projectileSpeeds.Add("Irelia", 467.0000f);
            projectileSpeeds.Add("Odin_Red_Minion_Caster", 650.0000f);
            projectileSpeeds.Add("Lucian", 2800.0000f);
            projectileSpeeds.Add("Yorick", float.MaxValue);
            projectileSpeeds.Add("RammusPB", float.MaxValue);
            projectileSpeeds.Add("Red_Minion_Basic", float.MaxValue);
            projectileSpeeds.Add("Udyr", 467.0000f);
            projectileSpeeds.Add("MonkeyKing", 20.0000f);
            projectileSpeeds.Add("Tutorial_Blue_Minion_Basic", float.MaxValue);
            projectileSpeeds.Add("Kennen", 1600.0000f);
            projectileSpeeds.Add("Nunu", 500.0000f);
            projectileSpeeds.Add("Ryze", 2400.0000f);
            projectileSpeeds.Add("Zed", 467.0000f);
            projectileSpeeds.Add("Nautilus", 1000.0000f);
            projectileSpeeds.Add("Gangplank", 1000.0000f);
            projectileSpeeds.Add("Lux", 1600.0000f);
            projectileSpeeds.Add("Sejuani", 500.0000f);
            projectileSpeeds.Add("Ezreal", 2000.0000f);
            projectileSpeeds.Add("OdinNeutralGuardian", 1800.0000f);
            projectileSpeeds.Add("Khazix", 500.0000f);
            projectileSpeeds.Add("Sion", float.MaxValue);
            projectileSpeeds.Add("Aatrox", 347.79999f);
            projectileSpeeds.Add("Hecarim", 500.0000f);
            projectileSpeeds.Add("Pantheon", 20.0000f);
            projectileSpeeds.Add("Shyvana", 467.0000f);
            projectileSpeeds.Add("Zyra", 1700.0000f);
            projectileSpeeds.Add("Karthus", 1200.0000f);
            projectileSpeeds.Add("Rammus", float.MaxValue);
            projectileSpeeds.Add("Zilean", 1200.0000f);
            projectileSpeeds.Add("Chogath", 500.0000f);
            projectileSpeeds.Add("Malzahar", 2000.0000f);
            projectileSpeeds.Add("YorickRavenousGhoul", 347.79999f);
            projectileSpeeds.Add("YorickSpectralGhoul", 347.79999f);
            projectileSpeeds.Add("JinxMine", 347.79999f);
            projectileSpeeds.Add("YorickDecayedGhoul", 347.79999f);
            projectileSpeeds.Add("XerathArcaneBarrageLauncher", 347.79999f);
            projectileSpeeds.Add("Odin_SOG_Order_Crystal", 347.79999f);
            projectileSpeeds.Add("TestCube", 347.79999f);
            projectileSpeeds.Add("ShyvanaDragon", float.MaxValue);
            projectileSpeeds.Add("FizzBait", float.MaxValue);
            projectileSpeeds.Add("Blue_Minion_MechMelee", float.MaxValue);
            projectileSpeeds.Add("OdinQuestBuff", float.MaxValue);
            projectileSpeeds.Add("TT_Buffplat_L", float.MaxValue);
            projectileSpeeds.Add("TT_Buffplat_R", float.MaxValue);
            projectileSpeeds.Add("KogMawDead", float.MaxValue);
            projectileSpeeds.Add("TempMovableChar", float.MaxValue);
            projectileSpeeds.Add("Lizard", 500.0000f);
            projectileSpeeds.Add("GolemOdin", float.MaxValue);
            projectileSpeeds.Add("OdinOpeningBarrier", float.MaxValue);
            projectileSpeeds.Add("TT_ChaosTurret4", 500.0000f);
            projectileSpeeds.Add("TT_Flytrap_A", 500.0000f);
            projectileSpeeds.Add("TT_NWolf", float.MaxValue);
            projectileSpeeds.Add("OdinShieldRelic", float.MaxValue);
            projectileSpeeds.Add("LuluSquill", float.MaxValue);
            projectileSpeeds.Add("redDragon", float.MaxValue);
            projectileSpeeds.Add("MonkeyKingClone", float.MaxValue);
            projectileSpeeds.Add("Odin_skeleton", float.MaxValue);
            projectileSpeeds.Add("OdinChaosTurretShrine", 500.0000f);
            projectileSpeeds.Add("Cassiopeia_Death", 500.0000f);
            projectileSpeeds.Add("OdinCenterRelic", 500.0000f);
            projectileSpeeds.Add("OdinRedSuperminion", float.MaxValue);
            projectileSpeeds.Add("JarvanIVWall", float.MaxValue);
            projectileSpeeds.Add("ARAMOrderNexus", float.MaxValue);
            projectileSpeeds.Add("Red_Minion_MechCannon", 1200.0000f);
            projectileSpeeds.Add("OdinBlueSuperminion", float.MaxValue);
            projectileSpeeds.Add("SyndraOrbs", float.MaxValue);
            projectileSpeeds.Add("LuluKitty", float.MaxValue);
            projectileSpeeds.Add("SwainNoBird", float.MaxValue);
            projectileSpeeds.Add("LuluLadybug", float.MaxValue);
            projectileSpeeds.Add("CaitlynTrap", float.MaxValue);
            projectileSpeeds.Add("TT_Shroom_A", float.MaxValue);
            projectileSpeeds.Add("ARAMChaosTurretShrine", 500.0000f);
            projectileSpeeds.Add("Odin_Windmill_Propellers", 500.0000f);
            projectileSpeeds.Add("TT_NWolf2", float.MaxValue);
            projectileSpeeds.Add("OdinMinionGraveyardPortal", float.MaxValue);
            projectileSpeeds.Add("SwainBeam", float.MaxValue);
            projectileSpeeds.Add("Summoner_Rider_Order", float.MaxValue);
            projectileSpeeds.Add("TT_Relic", float.MaxValue);
            projectileSpeeds.Add("odin_lifts_crystal", float.MaxValue);
            projectileSpeeds.Add("OdinOrderTurretShrine", 500.0000f);
            projectileSpeeds.Add("SpellBook1", 500.0000f);
            projectileSpeeds.Add("Blue_Minion_MechCannon", 1200.0000f);
            projectileSpeeds.Add("TT_ChaosInhibitor_D", 1200.0000f);
            projectileSpeeds.Add("Odin_SoG_Chaos", 1200.0000f);
            projectileSpeeds.Add("TrundleWall", 1200.0000f);
            projectileSpeeds.Add("HA_AP_HealthRelic", 1200.0000f);
            projectileSpeeds.Add("OrderTurretShrine", 500.0000f);
            projectileSpeeds.Add("OriannaBall", 500.0000f);
            projectileSpeeds.Add("ChaosTurretShrine", 500.0000f);
            projectileSpeeds.Add("LuluCupcake", 500.0000f);
            projectileSpeeds.Add("HA_AP_ChaosTurretShrine", 500.0000f);
            projectileSpeeds.Add("TT_NWraith2", 750.0000f);
            projectileSpeeds.Add("TT_Tree_A", 750.0000f);
            projectileSpeeds.Add("SummonerBeacon", 750.0000f);
            projectileSpeeds.Add("Odin_Drill", 750.0000f);
            projectileSpeeds.Add("TT_NGolem", float.MaxValue);
            projectileSpeeds.Add("AramSpeedShrine", float.MaxValue);
            projectileSpeeds.Add("OriannaNoBall", float.MaxValue);
            projectileSpeeds.Add("Odin_Minecart", float.MaxValue);
            projectileSpeeds.Add("Summoner_Rider_Chaos", float.MaxValue);
            projectileSpeeds.Add("OdinSpeedShrine", float.MaxValue);
            projectileSpeeds.Add("TT_SpeedShrine", float.MaxValue);
            projectileSpeeds.Add("odin_lifts_buckets", float.MaxValue);
            projectileSpeeds.Add("OdinRockSaw", float.MaxValue);
            projectileSpeeds.Add("OdinMinionSpawnPortal", float.MaxValue);
            projectileSpeeds.Add("SyndraSphere", float.MaxValue);
            projectileSpeeds.Add("Red_Minion_MechMelee", float.MaxValue);
            projectileSpeeds.Add("SwainRaven", float.MaxValue);
            projectileSpeeds.Add("crystal_platform", float.MaxValue);
            projectileSpeeds.Add("MaokaiSproutling", float.MaxValue);
            projectileSpeeds.Add("Urf", float.MaxValue);
            projectileSpeeds.Add("TestCubeRender10Vision", float.MaxValue);
            projectileSpeeds.Add("MalzaharVoidling", 500.0000f);
            projectileSpeeds.Add("GhostWard", 500.0000f);
            projectileSpeeds.Add("MonkeyKingFlying", 500.0000f);
            projectileSpeeds.Add("LuluPig", 500.0000f);
            projectileSpeeds.Add("AniviaIceBlock", 500.0000f);
            projectileSpeeds.Add("TT_OrderInhibitor_D", 500.0000f);
            projectileSpeeds.Add("Odin_SoG_Order", 500.0000f);
            projectileSpeeds.Add("RammusDBC", 500.0000f);
            projectileSpeeds.Add("FizzShark", 500.0000f);
            projectileSpeeds.Add("LuluDragon", 500.0000f);
            projectileSpeeds.Add("OdinTestCubeRender", 500.0000f);
            projectileSpeeds.Add("TT_Tree1", 500.0000f);
            projectileSpeeds.Add("ARAMOrderTurretShrine", 500.0000f);
            projectileSpeeds.Add("Odin_Windmill_Gears", 500.0000f);
            projectileSpeeds.Add("ARAMChaosNexus", 500.0000f);
            projectileSpeeds.Add("TT_NWraith", 750.0000f);
            projectileSpeeds.Add("TT_OrderTurret4", 500.0000f);
            projectileSpeeds.Add("Odin_SOG_Chaos_Crystal", 500.0000f);
            projectileSpeeds.Add("OdinQuestIndicator", 500.0000f);
            projectileSpeeds.Add("JarvanIVStandard", 500.0000f);
            projectileSpeeds.Add("TT_DummyPusher", 500.0000f);
            projectileSpeeds.Add("OdinClaw", 500.0000f);
            projectileSpeeds.Add("EliseSpiderling", 2000.0000f);
            projectileSpeeds.Add("QuinnValor", float.MaxValue);
            projectileSpeeds.Add("UdyrTigerUlt", float.MaxValue);
            projectileSpeeds.Add("UdyrTurtleUlt", float.MaxValue);
            projectileSpeeds.Add("UdyrUlt", float.MaxValue);
            projectileSpeeds.Add("UdyrPhoenixUlt", float.MaxValue);
            projectileSpeeds.Add("ShacoBox", 1500.0000f);
            projectileSpeeds.Add("HA_AP_Poro", 1500.0000f);
            projectileSpeeds.Add("AnnieTibbers", float.MaxValue);
            projectileSpeeds.Add("UdyrPhoenix", float.MaxValue);
            projectileSpeeds.Add("UdyrTurtle", float.MaxValue);
            projectileSpeeds.Add("UdyrTiger", float.MaxValue);
            projectileSpeeds.Add("HA_AP_OrderShrineTurret", 500.0000f);
            projectileSpeeds.Add("HA_AP_Chains_Long", 500.0000f);
            projectileSpeeds.Add("HA_AP_BridgeLaneStatue", 500.0000f);
            projectileSpeeds.Add("HA_AP_ChaosTurretRubble", 500.0000f);
            projectileSpeeds.Add("HA_AP_PoroSpawner", 500.0000f);
            projectileSpeeds.Add("HA_AP_Cutaway", 500.0000f);
            projectileSpeeds.Add("HA_AP_Chains", 500.0000f);
            projectileSpeeds.Add("ChaosInhibitor_D", 500.0000f);
            projectileSpeeds.Add("ZacRebirthBloblet", 500.0000f);
            projectileSpeeds.Add("OrderInhibitor_D", 500.0000f);
            projectileSpeeds.Add("Nidalee_Spear", 500.0000f);
            projectileSpeeds.Add("Nidalee_Cougar", 500.0000f);
            projectileSpeeds.Add("TT_Buffplat_Chain", 500.0000f);
            projectileSpeeds.Add("WriggleLantern", 500.0000f);
            projectileSpeeds.Add("TwistedLizardElder", 500.0000f);
            projectileSpeeds.Add("RabidWolf", float.MaxValue);
            projectileSpeeds.Add("HeimerTGreen", 1599.3999f);
            projectileSpeeds.Add("HeimerTRed", 1599.3999f);
            projectileSpeeds.Add("ViktorFF", 1599.3999f);
            projectileSpeeds.Add("TwistedGolem", float.MaxValue);
            projectileSpeeds.Add("TwistedSmallWolf", float.MaxValue);
            projectileSpeeds.Add("TwistedGiantWolf", float.MaxValue);
            projectileSpeeds.Add("TwistedTinyWraith", 750.0000f);
            projectileSpeeds.Add("TwistedBlueWraith", 750.0000f);
            projectileSpeeds.Add("TwistedYoungLizard", 750.0000f);
            projectileSpeeds.Add("Red_Minion_Melee", float.MaxValue);
            projectileSpeeds.Add("Blue_Minion_Melee", float.MaxValue);
            projectileSpeeds.Add("Blue_Minion_Healer", 1000.0000f);
            projectileSpeeds.Add("Ghast", 750.0000f);
            projectileSpeeds.Add("blueDragon", 800.0000f);
            projectileSpeeds.Add("Red_Minion_MechRange", 3000.0000f);
        }

        private class ActiveAttacks
        {
            public readonly float animationTime;
            public readonly Obj_AI_Base attacker;
            public readonly float damage;
            public readonly float hitTime;
            public readonly Vector3 pos;
            public readonly float projectileSpeed;
            public readonly float startTime;
            public readonly Obj_AI_Base target;
            public readonly float windUpTime;

            public ActiveAttacks(Obj_AI_Base attacker, Obj_AI_Base target, float startTime, float windUpTime,
                                 float hitTime, Vector3 pos, float projectileSpeed, float damage, float animationTime)
            {
                this.attacker = attacker;
                this.target = target;
                this.startTime = startTime;
                this.windUpTime = windUpTime;
                this.hitTime = hitTime;
                this.pos = pos;
                this.projectileSpeed = projectileSpeed;
                this.damage = damage;
                this.animationTime = animationTime;
            }
        }

        public class BestPrediction
        {
            private readonly bool valid;
            public Vector3 castPosition;
            public int hitChance;

            public BestPrediction()
            {
                valid = false;
            }

            public BestPrediction(Vector3 castPosition, int hitChance)
            {
                this.castPosition = castPosition;
                this.hitChance = hitChance;
                valid = true;
            }

            public bool IsValid()
            {
                return valid;
            }
        }

        public class BestPredictionAOE
        {
            private readonly bool valid;
            public Vector3 castPosition;
            public int hitChance;
            public int maxHits;
            public List<Vector3> positions;

            public BestPredictionAOE()
            {
                valid = false;
            }

            public BestPredictionAOE(Vector3 castPosition, int hitChance, int maxHits, List<Vector3> positions)
            {
                this.castPosition = castPosition;
                this.hitChance = hitChance;
                this.maxHits = maxHits;
                this.positions = positions;
                valid = true;
            }

            public bool IsValid()
            {
                return valid;
            }
        }

        private class Blinks
        {
            public readonly float delay;
            public readonly float delay2;
            public readonly float range;

            public Blinks(float range, float delay, float delay2)
            {
                this.range = range;
                this.delay = delay;
                this.delay2 = delay2;
            }
        }

        private class Dashes
        {
            public float duration;
            public Vector3 endPos;
            public float endT;
            public float endT2;
            public bool isBlink;
            public Vector3 startPos;

            public Dashes(bool isBlink, float duration, float endT, float endT2, Vector3 startPos, Vector3 endPos)
            {
                this.isBlink = isBlink;
                this.duration = duration;
                this.endT = endT;
                this.endT2 = endT2;
                this.startPos = startPos;
                this.endPos = endPos;
            }
        }
    }
}
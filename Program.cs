using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using SharpDX;
using Color = System.Drawing.Color;

//CONE BUGGY ATM TEST OR NEW FUNCTION NEEDED

namespace SPrediction
{

    public class Prediction
    {
        private static Dictionary<String, double> hitboxes = new Dictionary<String, double>();
        private static bool bHitBoxes = false;

        private static Vector3 castPositionDraw;
        private static Vector3 positionDraw;

        static List<Obj_AI_Minion> drawMinions = new List<Obj_AI_Minion>();
        static List<Vector3> drawMinionCircles = new List<Vector3>();

        public class BestPrediction
        {
            public Vector3 castPosition;
            public int hitChance;
            private bool valid;

            public BestPrediction()
            {
                this.valid = false;
            }

            public BestPrediction(Vector3 castPosition, int hitChance)
            {
                this.castPosition = castPosition;
                this.hitChance = hitChance;
                this.valid = true;
            }

            public bool IsValid()
            {
                return this.valid;
            }
        }

        public class BestPredictionAOE
        {
            public Vector3 castPosition;
            public int hitChance;
            public int maxHits;
            public List<Vector3> positions;
            private bool valid;

            public BestPredictionAOE()
            {
                this.valid = false;
            }

            public BestPredictionAOE(Vector3 castPosition, int hitChance, int maxHits, List<Vector3> positions)
            {
                this.castPosition = castPosition;
                this.hitChance = hitChance;
                this.maxHits = maxHits;
                this.positions = positions;
                this.valid = true;
            }

            public bool IsValid()
            {
                return this.valid;
            }
        }

        public enum SpellType
        {
            LINE = 0,
            CIRCULAR = 1
        }

        public enum SpellTypeAOE
        {
            LINE = 0,
            CIRCULAR = 1,
            CONE = 2
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
                Drawing.DrawCircle((Vector3) drawMinionCircle, 100, Color.Black);
            }
        }

        private static float GetHitBox(Obj_AI_Base object1)
        {
            if (object1.Type == ObjectManager.Player.Type)
            {
                return 0;
            }
            if (hitboxes.ContainsKey(object1.BaseSkinName))
            {
                double value;
                hitboxes.TryGetValue(object1.BaseSkinName, out value);
                return (float) value;
            }
            else
            {
                return 65;
            }
        }

        private static float MaxAngle(Obj_AI_Base unit, Vector3 currentWaypoint, Vector3 from)
        {
            List<Vector3> waypoints = GetWaypoints(unit);
            if (waypoints == null)
                return 0.0f;
            float Max = 0;
            Vector3 CV = new Vector3(currentWaypoint.X, 0, currentWaypoint.Y) - unit.Position;
            foreach (Vector3 waypoint in waypoints)
            {
                float angle = Utils.AngleBetween(new Vector3(0, 0, 0), CV,
                                           new Vector3(waypoint.X, 0, waypoint.Y) -
                                           new Vector3(unit.Position.X, 0, unit.Position.Y));
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
            pathes2.Add(unit.Position);
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
                                                 Vector3 from, float range)
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


            float angle = MaxAngle(unit, waypoints[waypoints.Count() - 1], unit.Position);
            if (angle > 90)
                hitChance = 1;
            else if (angle < 30 && CountWaypoints(unit) >= 1)
                hitChance = 2;

            if (CountWaypoints(unit) == 0)
                hitChance = 2;

            hitChance = 2; //<-----MENU ENTRY LATER

            if (Utils.IsValidVector3(castPosition) &&
                (radius / unit.MoveSpeed >= delay + Utils.GetDistance(from, castPosition) / speed))
                hitChance = 3;

            if (
                Utils.AngleBetween(new Vector3(from.X, from.Y, from.Z),
                             new Vector3(unit.Position.X, unit.Position.Y, unit.Position.Z), castPosition) > 60)
                hitChance = 1;

            if (Utils.GetDistance(ObjectManager.Player.Position, unit.Position) < 250)
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
            InitHitboxes();
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
            if (Utils.IsValidFloat(speed))
            {
                speed = speed;
            }
            else
            {
                speed = float.MaxValue;
            }
            if (from != null)
            {
                from = from;
            }
            else
            {
                from = ObjectManager.Player.Position;
            }

            bool fromMyHero;
            if (Utils.GetDistanceSqr(from, ObjectManager.Player.Position) < 50 * 50)
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
                if (vec3 == null)
                {
                    return new BestPrediction();
                }
                castPosition = vec3;
                hitChance = 2;
            }
            else
            {
                Object[] object2 = WayPointAnalysis(unit, delay, radius, speed, from, range);
                if (object2 != null)
                {
                    castPosition = (Vector3) object2[0];
                    hitChance = (int) object2[1];
                }

                if (fromMyHero)
                {
                    if (spelltype == SpellType.LINE && Utils.GetDistanceSqr(from, castPosition) >= range * range)
                        hitChance = 0;
                    if (spelltype == SpellType.CIRCULAR &&
                        (Utils.GetDistanceSqr(from, castPosition) >= Math.Pow(range + radius, 2)))
                        hitChance = 0;
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
            else if (Utils.IsValidVector3(unit.Position) && Utils.GetDistance(unit.Position, from) < range && !unit.IsMoving)
            {
                pos = unit.Position;
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
                    hitChance = -1;
            }

            return new BestPrediction(pos, hitChance);
        }

        private static bool CheckCollision(Obj_AI_Base unit, List<Obj_AI_Minion> minions, float delay, float radius, float speed,
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
                        Utils.GetDistanceSqr(from, minion.Position) <= Math.Pow(range + 100, 2))
                    {
                        Vector3 temp = new Vector3();
                        Vector3 pos = new Vector3();
                        if (minion.IsMoving)
                        {
                            pos = castPosition;
                        }
                        else
                        {
                            pos = minion.Position;
                        }
                        Vector3 posHero = new Vector3();
                        if (unit.IsMoving)
                        {
                            posHero = castPositionHero;
                        }
                        else
                        {
                            posHero = unit.Position;
                        }
                        if (waypoints.Count > 1 && Utils.IsValidVector3(waypoints[0]) && Utils.IsValidVector3(waypoints[1]))
                        {
                            Object[] objects1 = Utils.VectorPointProjectionOnLineSegment(from, posHero, pos);
                            Vector3 pointSegment1 = (Vector3)objects1[0];
                            Vector3 pointLine1 = (Vector3)objects1[1];
                            bool isOnSegment1 = (bool)objects1[2];
                            if (isOnSegment1 &&
                                (Utils.GetDistanceSqr(pos, pointSegment1) <= Math.Pow(GetHitBox(minion) + radius + 20, 2)))
                            {
                                return true;
                            }
                        }
                        Object[] objects = Utils.VectorPointProjectionOnLineSegment(from, posHero, pos);
                        Vector3 pointSegment = (Vector3)objects[0];
                        Vector3 pointLine = (Vector3)objects[1];
                        bool isOnSegment = (bool)objects[2];
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
                if (minion.Team != ObjectManager.Player.Team && Utils.GetDistance(from, minion.Position) < range + 500 * (delay + range / speed))
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
                                               SpellTypeAOE spelltype)
        {
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
            }

            return objects;
        }

        private static BestPredictionAOE GetCircularAOECastPosition(Obj_AI_Base unit, float delay, float radius, float speed,
                                               Vector3 from, float range, bool collision)
        {
            BestPrediction objects1 = GetBestPosition(unit, delay, radius, speed, from, range, collision, SpellType.CIRCULAR);
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
                    Utils.GetDistanceSqr(hero.Position, ObjectManager.Player.Position) <= (range * 1.5) * (range * 1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision, SpellType.CIRCULAR);
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
                Vector3 center2 = (Vector3)objects2[0];
                float radius2 = (float)objects2[1];
                Vector3 radiusPoint2 = (Vector3)objects2[2];

                if (radius2 <= radius + GetHitBox(unit) - 8)
                {
                    return new BestPredictionAOE(center2, mainHitChance, points.Count, points);
                }

                float maxdist = -1;
                int maxdistindex = 0;

                for (int i = 1; i < points.Count - 1; i++)
                {
                    float distance = (float)Utils.GetDistanceSqr(points[i], points[0]);
                    if (distance > maxdist || maxdist.CompareTo(-1) == 0)
                    {
                        maxdistindex = i;
                        maxdist = distance;
                    }
                }

                points.RemoveAt(maxdistindex);
            }

            return new BestPredictionAOE(mainCastPosition, mainHitChance, points.Count, points);
        }

        private static Vector3[] GetPossiblePoints(Vector3 from, Vector3 pos, float width, float range)
        {
            Vector3 middlePoint = (from + pos) / 2;
            Vector3[] vectors = Utils.CircleCircleIntersection(from, middlePoint, width, (float)Utils.GetDistance(middlePoint, from));
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
            return new Vector3[]{V1, V2};
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
                Vector3 pointSegment = (Vector3)objects[0];
                bool isOnSegment = (bool)objects[2];
                if (isOnSegment && Utils.GetDistanceSqr(pointSegment, point) <= width * width)
                {
                    hits = hits + 1;
                    nPoints.Add(point);
                }
                else if (i == 0)
                {
                    return new Object[] {hits, nPoints};
                }
            }
            return new Object[] { hits, nPoints };
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
                from = ObjectManager.Player.Position;
            }

            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (hero.IsEnemy && hero.NetworkId != unit.NetworkId && !hero.IsDead && hero.IsValid &&
                    Utils.GetDistanceSqr(hero.Position, ObjectManager.Player.Position) <= (range * 1.5) * (range * 1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision, SpellType.LINE);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistance(from, castPosition2) < (range + radius))
                    {
                        points.Add(castPosition2);
                        //positions.Add(new object[] { hero, hitChance2, castPosition2 });
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
                        maxHitPoints = (List<Vector3>)countHits1[1];
                    }
                    if ((int)countHits2[0] >= maxHit)
                    {
                        maxHitPos = C2;
                        maxHit = (int)countHits2[0];
                        maxHitPoints = (List<Vector3>)countHits2[1];
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
                        Vector3 pointSegment01 = (Vector3)objects01[0];
                        Vector3 pointLine01 = (Vector3)objects01[1];
                        bool isOnSegment01 = (bool)objects01[2];
                        Object[] objects02 = Utils.VectorPointProjectionOnLineSegment(startP, endP, maxHitPoints[j]);
                        Vector3 pointSegment02 = (Vector3)objects02[0];
                        Vector3 pointLine02 = (Vector3)objects02[1];
                        bool isOnSegment02 = (bool)objects02[2];
                        float dist = (float)(Utils.GetDistanceSqr(maxHitPoints[i], pointLine01) + Utils.GetDistanceSqr(maxHitPoints[j], pointLine02));
                        if (dist >= maxDistance)
                        {
                            maxDistance = dist;
                            p1 = maxHitPoints[i];
                            p2 = maxHitPoints[j];
                        }
                    }
                }
                return new BestPredictionAOE((p1 + p2) / 2, mainHitChance, maxHit, points);
            }

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
                else if (i == 1)
                {
                    return new object[] { result, hitpoints };
                }
            }
            return new object[]{result, hitpoints};
        }

        private static Object[] CheckHit(Vector3 position, float angle, List<Vector3> points)
        {
            Vector3 direction = new Vector3();
            Vector3.Normalize(ref position, out direction);
            Vector3 v1 = Utils.Vector3Rotate(position, 0, -angle / 2, 0);
            Vector3 v2 = Utils.Vector3Rotate(position, 0, angle / 2, 0);
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
                angle = (float)(angle*Math.PI/180);
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
                    Utils.GetDistanceSqr(hero.Position, ObjectManager.Player.Position) <= (range * 1.5) * (range * 1.5))
                {
                    BestPrediction objects2 = GetBestPosition(hero, delay, radius, speed, from, range, collision, SpellType.LINE);
                    if (objects2 == null || !objects2.IsValid())
                    {
                        continue;
                    }
                    Vector3 castPosition2 = objects2.castPosition;
                    int hitChance2 = objects2.hitChance;
                    if (Utils.GetDistanceSqr(from, castPosition2) < (range * range))
                    {
                        points.Add(castPosition2 - from);
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
                    Vector3 pos1 = Utils.Vector3Rotate(point, 0, angle / 2, 0);
                    Vector3 pos2 = Utils.Vector3Rotate(point, 0, -angle / 2, 0);

                    Object[] objects3 = CheckHit(pos1, angle, points);
                    int hits3 = (int) objects3[0];
                    List<Vector3> points3 = (List<Vector3>) objects3[1];
                    Object[] objects4 = CheckHit(pos2, angle, points);
                    int hits4 = (int)objects4[0];
                    List<Vector3> points4 = (List<Vector3>)objects4[1];

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
                p1 = p1 + from;
                p2 = p2 + from;

                Vector3 temp = (((p1 + p2) / 2) - from);
                temp.Normalize();
                Vector3.Multiply(temp, range);
                temp = Utils.Vector3Rotate(temp, 0, angle / 2, 0);

                return new BestPredictionAOE(from + temp, mainHitChance, maxHit, points);
            }
            else
            {
                return new BestPredictionAOE(mainCastPosition, mainHitChance, 1, points);
            }
        }

        private static void InitHitboxes()
        {
            if (bHitBoxes)
            {
                return;
            }
            hitboxes.Add("RecItemsCLASSIC", 65);
            hitboxes.Add("TeemoMushroom", 50);
            hitboxes.Add("TestCubeRender", 65);
            hitboxes.Add("Xerath", 65);
            hitboxes.Add("Kassadin", 65);
            hitboxes.Add("Rengar", 65);
            hitboxes.Add("Thresh", 55);
            hitboxes.Add("RecItemsTUTORIAL", 65);
            hitboxes.Add("Ziggs", 55);
            hitboxes.Add("ZyraPassive", 20);
            hitboxes.Add("ZyraThornPlant", 20);
            hitboxes.Add("KogMaw", 65);
            hitboxes.Add("HeimerTBlue", 35);
            hitboxes.Add("EliseSpider", 65);
            hitboxes.Add("Skarner", 80);
            hitboxes.Add("ChaosNexus", 65);
            hitboxes.Add("Katarina", 65);
            hitboxes.Add("Riven", 65);
            hitboxes.Add("SightWard", 1);
            hitboxes.Add("HeimerTYellow", 35);
            hitboxes.Add("Ashe", 65);
            hitboxes.Add("VisionWard", 1);
            hitboxes.Add("TT_NGolem2", 80);
            hitboxes.Add("ThreshLantern", 65);
            hitboxes.Add("RecItemsCLASSICMap10", 65);
            hitboxes.Add("RecItemsODIN", 65);
            hitboxes.Add("TT_Spiderboss", 200);
            hitboxes.Add("RecItemsARAM", 65);
            hitboxes.Add("OrderNexus", 65);
            hitboxes.Add("Soraka", 65);
            hitboxes.Add("Jinx", 65);
            hitboxes.Add("TestCubeRenderwCollision", 65);
            hitboxes.Add("Red_Minion_Wizard", 48);
            hitboxes.Add("JarvanIV", 65);
            hitboxes.Add("Blue_Minion_Wizard", 48);
            hitboxes.Add("TT_ChaosTurret2", 88.4);
            hitboxes.Add("TT_ChaosTurret3", 88.4);
            hitboxes.Add("TT_ChaosTurret1", 88.4);
            hitboxes.Add("ChaosTurretGiant", 88.4);
            hitboxes.Add("Dragon", 100);
            hitboxes.Add("LuluSnowman", 50);
            hitboxes.Add("Worm", 100);
            hitboxes.Add("ChaosTurretWorm", 88.4);
            hitboxes.Add("TT_ChaosInhibitor", 65);
            hitboxes.Add("ChaosTurretNormal", 88.4);
            hitboxes.Add("AncientGolem", 100);
            hitboxes.Add("ZyraGraspingPlant", 20);
            hitboxes.Add("HA_AP_OrderTurret3", 88.4);
            hitboxes.Add("HA_AP_OrderTurret2", 88.4);
            hitboxes.Add("Tryndamere", 65);
            hitboxes.Add("OrderTurretNormal2", 88.4);
            hitboxes.Add("Singed", 65);
            hitboxes.Add("OrderInhibitor", 65);
            hitboxes.Add("Diana", 65);
            hitboxes.Add("HA_FB_HealthRelic", 65);
            hitboxes.Add("TT_OrderInhibitor", 65);
            hitboxes.Add("GreatWraith", 80);
            hitboxes.Add("Yasuo", 65);
            hitboxes.Add("OrderTurretDragon", 88.4);
            hitboxes.Add("OrderTurretNormal", 88.4);
            hitboxes.Add("LizardElder", 65);
            hitboxes.Add("HA_AP_ChaosTurret", 88.4);
            hitboxes.Add("Ahri", 65);
            hitboxes.Add("Lulu", 65);
            hitboxes.Add("ChaosInhibitor", 65);
            hitboxes.Add("HA_AP_ChaosTurret3", 88.4);
            hitboxes.Add("HA_AP_ChaosTurret2", 88.4);
            hitboxes.Add("ChaosTurretWorm2", 88.4);
            hitboxes.Add("TT_OrderTurret1", 88.4);
            hitboxes.Add("TT_OrderTurret2", 88.4);
            hitboxes.Add("TT_OrderTurret3", 88.4);
            hitboxes.Add("LuluFaerie", 65);
            hitboxes.Add("HA_AP_OrderTurret", 88.4);
            hitboxes.Add("OrderTurretAngel", 88.4);
            hitboxes.Add("YellowTrinketUpgrade", 1);
            hitboxes.Add("MasterYi", 65);
            hitboxes.Add("Lissandra", 65);
            hitboxes.Add("ARAMOrderTurretNexus", 88.4);
            hitboxes.Add("Draven", 65);
            hitboxes.Add("FiddleSticks", 65);
            hitboxes.Add("SmallGolem", 80);
            hitboxes.Add("ARAMOrderTurretFront", 88.4);
            hitboxes.Add("ChaosTurretTutorial", 88.4);
            hitboxes.Add("NasusUlt", 80);
            hitboxes.Add("Maokai", 80);
            hitboxes.Add("Wraith", 50);
            hitboxes.Add("Wolf", 50);
            hitboxes.Add("Sivir", 65);
            hitboxes.Add("Corki", 65);
            hitboxes.Add("Janna", 65);
            hitboxes.Add("Nasus", 80);
            hitboxes.Add("Golem", 80);
            hitboxes.Add("ARAMChaosTurretFront", 88.4);
            hitboxes.Add("ARAMOrderTurretInhib", 88.4);
            hitboxes.Add("LeeSin", 65);
            hitboxes.Add("HA_AP_ChaosTurretTutorial", 88.4);
            hitboxes.Add("GiantWolf", 65);
            hitboxes.Add("HA_AP_OrderTurretTutorial", 88.4);
            hitboxes.Add("YoungLizard", 50);
            hitboxes.Add("Jax", 65);
            hitboxes.Add("LesserWraith", 50);
            hitboxes.Add("Blitzcrank", 80);
            hitboxes.Add("brush_D_SR", 65);
            hitboxes.Add("brush_E_SR", 65);
            hitboxes.Add("brush_F_SR", 65);
            hitboxes.Add("brush_C_SR", 65);
            hitboxes.Add("brush_A_SR", 65);
            hitboxes.Add("brush_B_SR", 65);
            hitboxes.Add("ARAMChaosTurretInhib", 88.4);
            hitboxes.Add("Shen", 65);
            hitboxes.Add("Nocturne", 65);
            hitboxes.Add("Sona", 65);
            hitboxes.Add("ARAMChaosTurretNexus", 88.4);
            hitboxes.Add("YellowTrinket", 1);
            hitboxes.Add("OrderTurretTutorial", 88.4);
            hitboxes.Add("Caitlyn", 65);
            hitboxes.Add("Trundle", 65);
            hitboxes.Add("Malphite", 80);
            hitboxes.Add("Mordekaiser", 80);
            hitboxes.Add("ZyraSeed", 65);
            hitboxes.Add("Vi", 50);
            hitboxes.Add("Tutorial_Red_Minion_Wizard", 48);
            hitboxes.Add("Renekton", 80);
            hitboxes.Add("Anivia", 65);
            hitboxes.Add("Fizz", 65);
            hitboxes.Add("Heimerdinger", 55);
            hitboxes.Add("Evelynn", 65);
            hitboxes.Add("Rumble", 80);
            hitboxes.Add("Leblanc", 65);
            hitboxes.Add("Darius", 80);
            hitboxes.Add("OlafAxe", 50);
            hitboxes.Add("Viktor", 65);
            hitboxes.Add("XinZhao", 65);
            hitboxes.Add("Orianna", 65);
            hitboxes.Add("Vladimir", 65);
            hitboxes.Add("Nidalee", 65);
            hitboxes.Add("Tutorial_Red_Minion_Basic", 48);
            hitboxes.Add("ZedShadow", 65);
            hitboxes.Add("Syndra", 65);
            hitboxes.Add("Zac", 80);
            hitboxes.Add("Olaf", 65);
            hitboxes.Add("Veigar", 55);
            hitboxes.Add("Twitch", 65);
            hitboxes.Add("Alistar", 80);
            hitboxes.Add("Akali", 65);
            hitboxes.Add("Urgot", 80);
            hitboxes.Add("Leona", 65);
            hitboxes.Add("Talon", 65);
            hitboxes.Add("Karma", 65);
            hitboxes.Add("Jayce", 65);
            hitboxes.Add("Galio", 80);
            hitboxes.Add("Shaco", 65);
            hitboxes.Add("Taric", 65);
            hitboxes.Add("TwistedFate", 65);
            hitboxes.Add("Varus", 65);
            hitboxes.Add("Garen", 65);
            hitboxes.Add("Swain", 65);
            hitboxes.Add("Vayne", 65);
            hitboxes.Add("Fiora", 65);
            hitboxes.Add("Quinn", 65);
            hitboxes.Add("Kayle", 65);
            hitboxes.Add("Blue_Minion_Basic", 48);
            hitboxes.Add("Brand", 65);
            hitboxes.Add("Teemo", 55);
            hitboxes.Add("Amumu", 55);
            hitboxes.Add("Annie", 55);
            hitboxes.Add("Odin_Blue_Minion_caster", 48);
            hitboxes.Add("Elise", 65);
            hitboxes.Add("Nami", 65);
            hitboxes.Add("Poppy", 55);
            hitboxes.Add("AniviaEgg", 65);
            hitboxes.Add("Tristana", 55);
            hitboxes.Add("Graves", 65);
            hitboxes.Add("Morgana", 65);
            hitboxes.Add("Gragas", 80);
            hitboxes.Add("MissFortune", 65);
            hitboxes.Add("Warwick", 65);
            hitboxes.Add("Cassiopeia", 65);
            hitboxes.Add("Tutorial_Blue_Minion_Wizard", 48);
            hitboxes.Add("DrMundo", 80);
            hitboxes.Add("Volibear", 80);
            hitboxes.Add("Irelia", 65);
            hitboxes.Add("Odin_Red_Minion_Caster", 48);
            hitboxes.Add("Lucian", 65);
            hitboxes.Add("Yorick", 80);
            hitboxes.Add("RammusPB", 65);
            hitboxes.Add("Red_Minion_Basic", 48);
            hitboxes.Add("Udyr", 65);
            hitboxes.Add("MonkeyKing", 65);
            hitboxes.Add("Tutorial_Blue_Minion_Basic", 48);
            hitboxes.Add("Kennen", 55);
            hitboxes.Add("Nunu", 65);
            hitboxes.Add("Ryze", 65);
            hitboxes.Add("Zed", 65);
            hitboxes.Add("Nautilus", 80);
            hitboxes.Add("Gangplank", 65);
            hitboxes.Add("shopevo", 65);
            hitboxes.Add("Lux", 65);
            hitboxes.Add("Sejuani", 80);
            hitboxes.Add("Ezreal", 65);
            hitboxes.Add("OdinNeutralGuardian", 65);
            hitboxes.Add("Khazix", 65);
            hitboxes.Add("Sion", 80);
            hitboxes.Add("Aatrox", 65);
            hitboxes.Add("Hecarim", 80);
            hitboxes.Add("Pantheon", 65);
            hitboxes.Add("Shyvana", 50);
            hitboxes.Add("Zyra", 65);
            hitboxes.Add("Karthus", 65);
            hitboxes.Add("Rammus", 65);
            hitboxes.Add("Zilean", 65);
            hitboxes.Add("Chogath", 80);
            hitboxes.Add("Malzahar", 65);
            hitboxes.Add("YorickRavenousGhoul", 1);
            hitboxes.Add("YorickSpectralGhoul", 1);
            hitboxes.Add("JinxMine", 65);
            hitboxes.Add("YorickDecayedGhoul", 1);
            hitboxes.Add("XerathArcaneBarrageLauncher", 65);
            hitboxes.Add("Odin_SOG_Order_Crystal", 65);
            hitboxes.Add("TestCube", 65);
            hitboxes.Add("ShyvanaDragon", 80);
            hitboxes.Add("FizzBait", 65);
            hitboxes.Add("ShopKeeper", 65);
            hitboxes.Add("Blue_Minion_MechMelee", 65);
            hitboxes.Add("OdinQuestBuff", 65);
            hitboxes.Add("TT_Buffplat_L", 65);
            hitboxes.Add("TT_Buffplat_R", 65);
            hitboxes.Add("KogMawDead", 65);
            hitboxes.Add("TempMovableChar", 48);
            hitboxes.Add("Lizard", 50);
            hitboxes.Add("GolemOdin", 80);
            hitboxes.Add("OdinOpeningBarrier", 65);
            hitboxes.Add("TT_ChaosTurret4", 88.4);
            hitboxes.Add("TT_Flytrap_A", 65);
            hitboxes.Add("TT_Chains_Order_Periph", 65);
            hitboxes.Add("TT_NWolf", 65);
            hitboxes.Add("ShopMale", 65);
            hitboxes.Add("OdinShieldRelic", 65);
            hitboxes.Add("TT_Chains_Xaos_Base", 65);
            hitboxes.Add("LuluSquill", 50);
            hitboxes.Add("TT_Shopkeeper", 65);
            hitboxes.Add("redDragon", 100);
            hitboxes.Add("MonkeyKingClone", 65);
            hitboxes.Add("Odin_skeleton", 65);
            hitboxes.Add("OdinChaosTurretShrine", 88.4);
            hitboxes.Add("Cassiopeia_Death", 65);
            hitboxes.Add("OdinCenterRelic", 48);
            hitboxes.Add("Ezreal_cyber_1", 65);
            hitboxes.Add("Ezreal_cyber_3", 65);
            hitboxes.Add("Ezreal_cyber_2", 65);
            hitboxes.Add("OdinRedSuperminion", 55);
            hitboxes.Add("TT_Speedshrine_Gears", 65);
            hitboxes.Add("JarvanIVWall", 65);
            hitboxes.Add("DestroyedNexus", 65);
            hitboxes.Add("ARAMOrderNexus", 65);
            hitboxes.Add("Red_Minion_MechCannon", 65);
            hitboxes.Add("OdinBlueSuperminion", 55);
            hitboxes.Add("SyndraOrbs", 65);
            hitboxes.Add("LuluKitty", 50);
            hitboxes.Add("SwainNoBird", 65);
            hitboxes.Add("LuluLadybug", 50);
            hitboxes.Add("CaitlynTrap", 65);
            hitboxes.Add("TT_Shroom_A", 65);
            hitboxes.Add("ARAMChaosTurretShrine", 88.4);
            hitboxes.Add("Odin_Windmill_Propellers", 65);
            hitboxes.Add("DestroyedInhibitor", 65);
            hitboxes.Add("TT_NWolf2", 50);
            hitboxes.Add("OdinMinionGraveyardPortal", 1);
            hitboxes.Add("SwainBeam", 65);
            hitboxes.Add("Summoner_Rider_Order", 65);
            hitboxes.Add("TT_Relic", 65);
            hitboxes.Add("odin_lifts_crystal", 65);
            hitboxes.Add("OdinOrderTurretShrine", 88.4);
            hitboxes.Add("SpellBook1", 65);
            hitboxes.Add("Blue_Minion_MechCannon", 65);
            hitboxes.Add("TT_ChaosInhibitor_D", 65);
            hitboxes.Add("Odin_SoG_Chaos", 65);
            hitboxes.Add("TrundleWall", 65);
            hitboxes.Add("HA_AP_HealthRelic", 65);
            hitboxes.Add("OrderTurretShrine", 88.4);
            hitboxes.Add("OriannaBall", 48);
            hitboxes.Add("ChaosTurretShrine", 88.4);
            hitboxes.Add("LuluCupcake", 50);
            hitboxes.Add("HA_AP_ChaosTurretShrine", 88.4);
            hitboxes.Add("TT_Chains_Bot_Lane", 65);
            hitboxes.Add("TT_NWraith2", 50);
            hitboxes.Add("TT_Tree_A", 65);
            hitboxes.Add("SummonerBeacon", 65);
            hitboxes.Add("Odin_Drill", 65);
            hitboxes.Add("TT_NGolem", 80);
            hitboxes.Add("Shop", 65);
            hitboxes.Add("AramSpeedShrine", 65);
            hitboxes.Add("DestroyedTower", 65);
            hitboxes.Add("OriannaNoBall", 65);
            hitboxes.Add("Odin_Minecart", 65);
            hitboxes.Add("Summoner_Rider_Chaos", 65);
            hitboxes.Add("OdinSpeedShrine", 65);
            hitboxes.Add("TT_Brazier", 65);
            hitboxes.Add("TT_SpeedShrine", 65);
            hitboxes.Add("odin_lifts_buckets", 65);
            hitboxes.Add("OdinRockSaw", 65);
            hitboxes.Add("OdinMinionSpawnPortal", 1);
            hitboxes.Add("SyndraSphere", 48);
            hitboxes.Add("TT_Nexus_Gears", 65);
            hitboxes.Add("Red_Minion_MechMelee", 65);
            hitboxes.Add("SwainRaven", 65);
            hitboxes.Add("crystal_platform", 65);
            hitboxes.Add("MaokaiSproutling", 48);
            hitboxes.Add("Urf", 65);
            hitboxes.Add("TestCubeRender10Vision", 65);
            hitboxes.Add("MalzaharVoidling", 10);
            hitboxes.Add("GhostWard", 1);
            hitboxes.Add("MonkeyKingFlying", 65);
            hitboxes.Add("LuluPig", 50);
            hitboxes.Add("AniviaIceBlock", 65);
            hitboxes.Add("TT_OrderInhibitor_D", 65);
            hitboxes.Add("yonkey", 65);
            hitboxes.Add("Odin_SoG_Order", 65);
            hitboxes.Add("RammusDBC", 65);
            hitboxes.Add("FizzShark", 65);
            hitboxes.Add("LuluDragon", 50);
            hitboxes.Add("OdinTestCubeRender", 65);
            hitboxes.Add("OdinCrane", 65);
            hitboxes.Add("TT_Tree1", 65);
            hitboxes.Add("ARAMOrderTurretShrine", 88.4);
            hitboxes.Add("TT_Chains_Order_Base", 65);
            hitboxes.Add("Odin_Windmill_Gears", 65);
            hitboxes.Add("ARAMChaosNexus", 65);
            hitboxes.Add("TT_NWraith", 50);
            hitboxes.Add("TT_OrderTurret4", 88.4);
            hitboxes.Add("Odin_SOG_Chaos_Crystal", 65);
            hitboxes.Add("TT_SpiderLayer_Web", 65);
            hitboxes.Add("OdinQuestIndicator", 1);
            hitboxes.Add("JarvanIVStandard", 65);
            hitboxes.Add("TT_DummyPusher", 65);
            hitboxes.Add("OdinClaw", 65);
            hitboxes.Add("EliseSpiderling", 1);
            hitboxes.Add("QuinnValor", 65);
            hitboxes.Add("UdyrTigerUlt", 65);
            hitboxes.Add("UdyrTurtleUlt", 65);
            hitboxes.Add("UdyrUlt", 65);
            hitboxes.Add("UdyrPhoenixUlt", 65);
            hitboxes.Add("ShacoBox", 10);
            hitboxes.Add("HA_AP_Poro", 65);
            hitboxes.Add("AnnieTibbers", 80);
            hitboxes.Add("UdyrPhoenix", 65);
            hitboxes.Add("UdyrTurtle", 65);
            hitboxes.Add("UdyrTiger", 65);
            hitboxes.Add("HA_AP_OrderShrineTurret", 88.4);
            hitboxes.Add("HA_AP_OrderTurretRubble", 65);
            hitboxes.Add("HA_AP_Chains_Long", 65);
            hitboxes.Add("HA_AP_OrderCloth", 65);
            hitboxes.Add("HA_AP_PeriphBridge", 65);
            hitboxes.Add("HA_AP_BridgeLaneStatue", 65);
            hitboxes.Add("HA_AP_ChaosTurretRubble", 88.4);
            hitboxes.Add("HA_AP_BannerMidBridge", 65);
            hitboxes.Add("HA_AP_PoroSpawner", 50);
            hitboxes.Add("HA_AP_Cutaway", 65);
            hitboxes.Add("HA_AP_Chains", 65);
            hitboxes.Add("HA_AP_ShpSouth", 65);
            hitboxes.Add("HA_AP_HeroTower", 65);
            hitboxes.Add("HA_AP_ShpNorth", 65);
            hitboxes.Add("ChaosInhibitor_D", 65);
            hitboxes.Add("ZacRebirthBloblet", 65);
            hitboxes.Add("OrderInhibitor_D", 65);
            hitboxes.Add("Nidalee_Spear", 65);
            hitboxes.Add("Nidalee_Cougar", 65);
            hitboxes.Add("TT_Buffplat_Chain", 65);
            hitboxes.Add("WriggleLantern", 1);
            hitboxes.Add("TwistedLizardElder", 65);
            hitboxes.Add("RabidWolf", 65);
            hitboxes.Add("HeimerTGreen", 50);
            hitboxes.Add("HeimerTRed", 50);
            hitboxes.Add("ViktorFF", 65);
            hitboxes.Add("TwistedGolem", 80);
            hitboxes.Add("TwistedSmallWolf", 50);
            hitboxes.Add("TwistedGiantWolf", 65);
            hitboxes.Add("TwistedTinyWraith", 50);
            hitboxes.Add("TwistedBlueWraith", 50);
            hitboxes.Add("TwistedYoungLizard", 50);
            hitboxes.Add("Red_Minion_Melee", 48);
            hitboxes.Add("Blue_Minion_Melee", 48);
            hitboxes.Add("Blue_Minion_Healer", 48);
            hitboxes.Add("Ghast", 60);
            hitboxes.Add("blueDragon", 100);
            hitboxes.Add("Red_Minion_MechRange", 65);
            hitboxes.Add("Test_CubeSphere", 65);
            bHitBoxes = true;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace SPrediction
{
    class Utils
    {
        public static bool IsValidVector3(Vector3 vector)
        {
            if (vector.X.CompareTo(0.0f) == 0 && vector.Y.CompareTo(0.0f) == 0 && vector.Z.CompareTo(0.0f) == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool IsValidFloat(float t)
        {
            if (t.CompareTo(0) != 0 && !float.IsNaN(t) && t.CompareTo(float.MaxValue) != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static Object[] VectorPointProjectionOnLineSegment(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            float cx = v3.X;
            float cy = v3.Y;
            float ax = v1.X;
            float ay = v1.Y;
            float bx = v2.X;
            float by = v2.Y;
            float rL = ((cx - ax) * (bx - ax) + (cy - ay) * (by - ay)) / ((float)Math.Pow(bx - ax, 2) + (float)Math.Pow(by - ay, 2));
            Vector3 pointLine = new Vector3(ax + rL * (bx - ax), ay + rL * (by - ay), 0);
            float rS;
            if (rL < 0)
            {
                rS = 0;
            }
            else if (rL > 1)
            {
                rS = 1;
            }
            else
            {
                rS = rL;
            }
            bool isOnSegment;
            if (rS.CompareTo(rL) == 0)
            {
                isOnSegment = true;
            }
            else
            {
                isOnSegment = false;
            }
            Vector3 pointSegment = new Vector3();
            if (isOnSegment)
            {
                pointSegment = pointLine;
            }
            else
            {
                pointSegment = new Vector3(ax + rS * (bx - ax), ay + rS * (by - ay), 0);
            }
            return new object[3] { pointSegment, pointLine, isOnSegment };
        }

        public static Vector3 VectorIntersection(Vector3 a1, Vector3 b1, Vector3 a2, Vector3 b2)
        {
            float x1 = a1.X, y1 = a1.Y, x2 = b1.X, y2 = b1.Y, x3 = a2.X, y3 = a2.Y, x4 = b2.X, y4 = b2.Y;
            float r = x1 * y2 - y1 * x2, s = x3 * y4 - y3 * x4, u = x3 - x4, v = x1 - x2, k = y3 - y4, l = y1 - y2;
            float px = r * u - v * s, py = r * k - l * s, divisor = v * k - l * u;
            if (divisor.CompareTo(0) != 0)
            {
                return new Vector3(px / divisor, py / divisor, 0);
            }
            return new Vector3();
        }

        public static Vector3 Vector3CrossP(Vector3 self, Vector3 other)
        {
            return new Vector3(other.Z * self.Y - other.Y * self.Z, other.X * self.Z - other.Z * self.X, other.Y * self.X - other.X * self.Y);
        }

        public static Vector3 Vector3Rotate(Vector3 self, float x, float y, float z)
        {
            double c;
            double s;
            if (x.CompareTo(0) != 0)
            {
                c = Math.Cos(x);
                s = Math.Sin(x);
                self.Y = (float)(self.Y * c - self.Z * s);
                self.Z = (float)(self.Z * c + self.Y * s);
            }
            if (y.CompareTo(0) != 0)
            {
                c = Math.Cos(y);
                s = Math.Sin(y);
                self.X = (float)(self.X * c + self.Z * s);
                self.Z = (float)(self.Z * c - self.X * s);
            }
            if (z.CompareTo(0) != 0)
            {
                c = Math.Cos(z);
                s = Math.Sin(z);
                self.X = (float)(self.X * c - self.Z * s);
                self.Y = (float)(self.Y * c + self.X * s);
            }
            return self;
        }

        public static float Vector3Len2(Vector3 v1, Vector3 v2)
        {
            return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
        }

        public static float Vector3Len(Vector3 v1, Vector3 v2)
        {
            return (float)Math.Sqrt(Vector3Len2(v1, v2));
        }

        public static Vector3 Vector3Center(Vector3 v1, Vector3 v2)
        {
            return (v1 + v2) / 2;
        }

        public static float Vector3Dist(Vector3 v1, Vector3 v2)
        {
            Vector3 v3 = v2 - v1;
            float distance = (float)(Math.Pow((v1.X - v2.X), 2) +
                              Math.Pow((v1.Y - v2.Y), 2) +
                              Math.Pow((v1.Z - v2.Z), 2));
            distance = (float)Math.Sqrt(distance);
            return distance;
        }

        public static float VectorDirection(Vector3 v1, Vector3 v2, Vector3 v)
        {
            return (v.Y - v1.Y) * (v2.X - v1.X) - (v2.Y - v1.Y) * (v.X - v1.X);
        }

        public static bool Ccw(Vector3 A, Vector3 B, Vector3 C)
        {
            return (C.Y - A.Y) * (B.X - A.X) > (B.Y - A.Y) * (C.X - A.X);
        }

        public static bool Intersect(Vector3 A, Vector3 B, Vector3 C, Vector3 D)
        {
            return Ccw(A, C, D) != Ccw(B, C, D) && Ccw(A, B, C) != Ccw(A, B, D);
        }

        public static List<Vector3> MECHalfHull(Vector3 left, Vector3 right, List<Vector3> points, float factor)
        {
            List<Vector3> input = points;
            input.Add(right);
            List<Vector3> half = new List<Vector3>();
            half.Add(left);
            foreach (Vector3 point in input)
            {
                half.Add(point);
                while (half.Count >= 3)
                {
                    float dir = factor * VectorDirection(half[half.Count - 3], half[half.Count - 1], half[half.Count - 2]);
                    if (dir <= 0)
                    {
                        half.Remove(half[half.Count - 2]);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return half;
        }

        public static List<Vector3> MECConvexHull(List<Vector3> points)
        {
            Vector3 left = points[0], right = points[points.Count - 1];
            List<Vector3> upper = new List<Vector3>(), lower = new List<Vector3>(), ret = new List<Vector3>();
            for (int i = 1; i < points.Count - 2; i++)
            {
                if (VectorDirection(left, right, points[i]) < 0)
                {
                    upper.Add(points[i]);
                }
                else
                {
                    lower.Add(points[i]);
                }
            }
            List<Vector3> upperHull = MECHalfHull(left, right, upper, -1);
            List<Vector3> lowerHull = MECHalfHull(left, right, lower, 1);
            foreach (Vector3 vector3 in upperHull)
            {
                ret.Add(vector3);
            }
            foreach (Vector3 vector3 in lowerHull)
            {
                ret.Add(vector3);
            }
            return ret;
        }

        public static Object[] ComputeMEC(List<Vector3> points)
        {
            if (points.Count == 0)
            {
                return null;
            }
            Vector3 center = new Vector3();
            float radius = 0;
            Vector3 radiusPoint = new Vector3();
            if (points.Count == 1)
            {
                center = points[0];
                radius = 0;
                radiusPoint = points[0];
            }
            else if (points.Count == 2)
            {
                center = Vector3Center(points[0], points[1]);
                radius = Vector3Dist(points[0], center);
                radiusPoint = points[0];
            }
            else
            {
                List<Vector3> a = MECConvexHull(points);
                Vector3 point_a = a[0];
                Vector3 point_b;
                Vector3 point_c;
                if (a.Count >= 2)
                {
                    point_c = a[1];
                }
                else
                {
                    center = point_a;
                    radius = 0;
                    radiusPoint = point_a;
                    return new Object[] { center, radius, radiusPoint };
                }
                while (true)
                {
                    point_b = new Vector3();
                    float best_theta = 180.0f;
                    foreach (Vector3 point in points)
                    {
                        if (point != point_a && point != point_c)
                        {
                            float theta_abc = Utils.AngleBetween(point, point_a, point_c);
                            if (theta_abc < best_theta)
                            {
                                point_b = point;
                                best_theta = theta_abc;
                            }
                        }
                    }

                    if (best_theta >= 90.0f || !Utils.IsValidVector3(point_b))
                    {
                        center = Vector3Center(point_a, point_c);
                        radius = Vector3Dist(point_a, center);
                        radiusPoint = point_a;
                        return new Object[] { center, radius, radiusPoint };
                    }

                    float ang_bca = Utils.AngleBetween(point_c, point_b, point_a);
                    float ang_cab = Utils.AngleBetween(point_a, point_c, point_b);
                    if (ang_bca > 90.0f)
                    {
                        point_c = point_b;
                    }
                    else if (ang_cab <= 90.0f)
                    {
                        break;
                    }
                    else
                    {
                        point_a = point_b;
                    }
                }
                Vector3 ch1 = Vector3.Multiply(point_b - point_a, 0.5f);
                Vector3 ch2 = Vector3.Multiply(point_c - point_a, 0.5f);
                Vector3 n1 = Utils.perpendicular2(ch1);
                Vector3 n2 = Utils.perpendicular2(ch2);
                ch1 = point_a + ch1;
                ch2 = point_a + ch2;
                center = VectorIntersection(ch1, n1, ch2, n2);
                radius = Vector3Dist(center, point_a);
                radiusPoint = point_a;
            }
            return new Object[] { center, radius, radiusPoint };
        }

        public static Vector3 perpendicular(Vector3 v)
        {
            return new Vector3(-v.Z, v.Y, v.X);
        }

        public static Vector3 perpendicular2(Vector3 v)
        {
            return new Vector3(v.Z, v.Y, -v.X);
        }

        public static Vector3[] CircleCircleIntersection(Vector3 from, Vector3 middlePoint, float width, float distance)
        {
            float D = (float)GetDistance(from, middlePoint);
            float A = (width * width - distance * distance + D * D) / (2 * D);
            float H = (float)Math.Sqrt(width * width - A * A);
            Vector3 Direction = middlePoint - from;
            Direction.Normalize();
            Vector3 PA = from + A * Direction;
            Vector3 S1 = PA + H * Direction;
            Utils.perpendicular(S1);
            Vector3 S2 = PA - H * Direction;
            Utils.perpendicular(S2);
            return new Vector3[] { S1, S2 };
        }

        public static bool Close(float a, float b, float eps)
        {
            if (Utils.IsValidFloat(eps))
                eps = eps;
            else
                eps = (float)1e-9;
            return Math.Abs(a - b) <= eps;
        }

        public static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        public static float Polar(Vector3 v1)
        {
            if (Close(v1.X, 0, 0))
            {
                float area1 = v1.Y;
                if (area1 > 0)
                {
                    return 90;
                }
                else if (area1 < 0)
                {
                    return 270;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                float area1 = v1.Y;
                var theta = (float)RadianToDegree(Math.Atan((area1) / v1.X));
                if (v1.X < 0)
                {
                    theta = theta + 180;
                }
                if (theta < 0)
                {
                    theta = theta + 360;
                }
                return theta;
            }
        }

        public static float AngleBetween(Vector3 self, Vector3 v1, Vector3 v2)
        {
            Vector3 p1 = (-self + v1);
            Vector3 p2 = (-self + v2);
            float theta = Polar(p1) - Polar(p2);
            if (theta < 0)
                theta = theta + 360;
            if (theta > 180)
                theta = 360 - theta;
            return theta;
        }

        public static double GetDistanceSqr(Vector3 distance1, Vector3 distance2)
        {
            float area1 = distance1.Y;
            float area2 = distance2.Y;
            double distance = Math.Pow((distance1.X - distance2.X), 2) +
                              Math.Pow(((area1) - (area2)), 2);
            return distance;
        }

        public static double GetDistance(Vector3 distance1, Vector3 distance2)
        {
            double distance = GetDistanceSqr(distance1, distance2);
            distance = Math.Sqrt(distance);
            return distance;
        }
    }
}

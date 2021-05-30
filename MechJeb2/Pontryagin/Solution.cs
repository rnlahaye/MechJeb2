using System;
using System.Collections.Generic;
using System.Text;

namespace MuMech
{
    public class Solution
    {
        public double t0; // kerbal time
        public double t_scale;
        public double v_scale;
        public double r_scale;

        public Solution(double t_scale, double v_scale, double r_scale, double t0)
        {
            this.t_scale = t_scale;
            this.v_scale = v_scale;
            this.r_scale = r_scale;
            this.t0      = t0;
        }

        public double tgo(double t)
        {
            double tbar = (t - t0) / t_scale;
            return (tmax() - tbar) * t_scale;
        }

        public double tgo(double t, int n) // tgo for each segment/arc in the solution
        {
            double tbar = (t - t0) / t_scale;
            return tgo_bar(tbar, n) * t_scale;
        }

        // this is the tgo of the "booster" stage, this is deliberately allowed to go negative if
        // we're staging so "current" may be a misnomer.
        //
        public double current_tgo(double t)
        {
            double tbar = (t - t0) / t_scale;
            return (segments[0].tmax - tbar) * t_scale;
        }

        public double tgo_bar(double tbar, int n) // tgo for each segment/arc in the solution
        {
            if (tbar > segments[n].tmin)
                return Math.Max(segments[n].tmax - tbar, 0);
            return segments[n].tmax - segments[n].tmin;
        }

        public double tburn_bar(double tbar)
        {
            double tburn = 0.0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (arcs[i].coast)
                    continue;
                tburn += tgo_bar(tbar, i);
            }

            return tburn;
        }

        public string ArcString(double t)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < arcs.Count; i++)
            {
                sb.AppendLine(ArcString(t, i));
            }

            return sb.ToString();
        }

        public string ArcString(double t, int n)
        {
            Arc arc = arcs[n];
            if (arc.ksp_stage < 0)
                return string.Format("coast: {0:F1}s", tgo(t, n));
            return string.Format("burn {0}: {1:F1}s {2:F1}m/s ({3:F1})", arc.ksp_stage, tgo(t, n), dV(t, n),
                arc.AvailDV - dV(arc._synch_time, n));
        }

        public double vgo(double t)
        {
            return dV(tf()) - dV(t);
        }

        public double tf() // tmax in kerbal time
        {
            return t0 + tmax() * t_scale;
        }

        public Vector3d vf()
        {
            return v(tf());
        }

        public Vector3d rf()
        {
            return r(tf());
        }

        public double tmax() // normalized time
        {
            int last = segments.Count - 1;
            if (arcs[last].coast)
                // we do not include the time of a final coast in overall tgo/vgo
                return segments[last].tmin;
            return segments[last].tmax;
        }

        public double tmin() // normalized time
        {
            return segments[0].tmin;
        }

        public double tbar(double t)
        {
            double tbar = (t - t0) / t_scale;
            if (tbar < tmin())
                return tmin();

            if (tbar > tmax())
                return tmax();

            return tbar;
        }

        public Vector3d r(double t)
        {
            double tbar = (t - t0) / t_scale;
            return Planetarium.fetch.rotation * new Vector3d(interpolate(0, tbar), interpolate(1, tbar), interpolate(2, tbar)) * r_scale;
        }

        public Vector3d r_bar(double tbar)
        {
            return new Vector3d(interpolate(0, tbar), interpolate(1, tbar), interpolate(2, tbar));
        }

        public Vector3d v(double t)
        {
            double tbar = (t - t0) / t_scale;
            return Planetarium.fetch.rotation * new Vector3d(interpolate(3, tbar), interpolate(4, tbar), interpolate(5, tbar)) * v_scale;
        }

        public Vector3d v_bar(double tbar)
        {
            return new Vector3d(interpolate(3, tbar), interpolate(4, tbar), interpolate(5, tbar));
        }

        public Vector3d pv(double t)
        {
            double tbar = (t - t0) / t_scale;
            return Planetarium.fetch.rotation * new Vector3d(interpolate(6, tbar), interpolate(7, tbar), interpolate(8, tbar));
        }

        public Vector3d pv_bar(double tbar)
        {
            return new Vector3d(interpolate(6, tbar), interpolate(7, tbar), interpolate(8, tbar));
        }

        public Vector3d pr(double t)
        {
            double tbar = (t - t0) / t_scale;
            return Planetarium.fetch.rotation * new Vector3d(interpolate(9, tbar), interpolate(10, tbar), interpolate(11, tbar));
        }

        public Vector3d pr_bar(double tbar)
        {
            return new Vector3d(interpolate(9, tbar), interpolate(10, tbar), interpolate(11, tbar));
        }

        public double m(double t)
        {
            double tbar = (t - t0) / t_scale;
            return interpolate(12, tbar);
        }

        public double m_bar(double tbar)
        {
            return interpolate(12, tbar);
        }

        public double dV(double t)
        {
            double tbar = (t - t0) / t_scale;
            return interpolate(13, tbar) * v_scale;
        }

        public double dV(double t, int n)
        {
            double tbar = (t - t0) / t_scale;
            double tmin = segments[n].tmin;
            double tmax = segments[n].tmax;

            if (tbar > tmin)
                tmin = tbar;

            return (interpolate(13, tmax) - interpolate(13, tmin)) * v_scale;
        }

        public void pitch_and_heading(double t, ref double pitch, ref double heading)
        {
            double tbar = (t - t0) / t_scale;
            var rbar = new Vector3d(interpolate(0, tbar), interpolate(1, tbar), interpolate(2, tbar));
            var pv = new Vector3d(interpolate(6, tbar), interpolate(7, tbar), interpolate(8, tbar));
            Vector3d headVec = pv - Vector3d.Dot(pv, rbar) * rbar;
            Vector3d east = Vector3d.Cross(rbar, new Vector3d(0, 1, 0)).normalized;
            Vector3d north = Vector3d.Cross(east, rbar).normalized;
            pitch   = 90.0 - Vector3d.Angle(pv, rbar);
            heading = MuUtils.ClampDegrees360(UtilMath.Rad2Deg * Math.Atan2(Vector3d.Dot(headVec, east), Vector3d.Dot(headVec, north)));
        }

        private double interpolate(int i, double tbar)
        {
            for (int k = 0; k < segments.Count; k++)
            {
                Segment s = segments[k];
                if (tbar < s.tmax)
                    return s.interpolate(i, tbar);
            }

            return segments[segments.Count - 1].interpolate(i, tbar);
        }

        public int           num_segments => segments.Count;
        public List<Segment> segments = new List<Segment>();
        public List<Arc>     arcs     = new List<Arc>();

        public Arc last_arc()
        {
            return arcs[arcs.Count - 1];
        }

        public Arc terminal_burn_arc()
        {
            for (int k = arcs.Count - 1; k >= 0; k--)
                if (arcs[k].Thrust > 0)
                    return arcs[k];
            return arcs[0];
        }

        // Arc from index
        public Arc arc(int n)
        {
            return arcs[n];
        }

        // Segment from time
        public int segment(double t)
        {
            double tbar = (t - t0) / t_scale;
            for (int k = 0; k < segments.Count; k++)
            {
                Segment s = segments[k];
                if (tbar < s.tmax)
                    return k;
            }

            return segments.Count - 1;
        }

        // Arc from time
        public Arc arc(double t)
        {
            return arcs[segment(t)];
        }

        // Synch stats from LogicalStageTracking controller
        public void UpdateStageInfo(double t0)
        {
            for (int i = 0; i < arcs.Count; i++)
                arcs[i].UpdateStageInfo(t0);
        }

        public class Segment
        {
            public double                       tmin, tmax;
            public alglib.spline1dinterpolant[] interpolant = new alglib.spline1dinterpolant[14];
            public double[]                     ysaved;

            public double interpolate(int i, double tbar)
            {
                if (interpolant[i] != null)
                    return alglib.spline1dcalc(interpolant[i], tbar);
                return ysaved[i];
            }

            public Segment(List<double> t, List<double[]> y)
            {
                int n = t.Count;
                double[] ti = new double[n];
                double[][] yi = new double[14][];

                tmin = t[0];
                tmax = t[n - 1];

                // if tmin == tmax (zero delta-t arc) alglib explodes
                if (tmin != tmax)
                {
                    for (int i = 0; i < 14; i++)
                        yi[i] = new double[n];

                    int j2 = 0;

                    for (int j = 0; j < n; j++)
                    {
                        if (j > 1 && Math.Abs(t[j] - ti[j2 - 1]) < 1e-15)
                            continue;

                        ti[j2] = t[j];
                        for (int i = 0; i < 14; i++) yi[i][j2] = y[j][i];

                        j2++;
                    }

                    for (int i = 0; i < 14; i++)
                        alglib.spline1dbuildcubic(ti, yi[i], j2, 0, 0, 0, 0, out interpolant[i]);
                }
                else
                {
                    ysaved = y[0];
                }
            }
        }

        public void AddSegment(List<double> t, List<double[]> y, Arc a)
        {
            segments.Add(new Segment(t, y));
            arcs.Add(a);
        }
    }
}
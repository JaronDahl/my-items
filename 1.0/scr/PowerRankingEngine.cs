using System;
using System.Collections.Generic;
using UnityEngine;

namespace PowerRanking
{
    public static class PowerRankingEngine
    {
        // Floor used inside the weighted geometric mean so a zero sub-score
        // does not collapse the whole product to zero.
        private const float Floor = 0.01f;

        public static List<CountryScore> Compute(Weights weights)
        {
            List<CountryScore> list = new List<CountryScore>();

            if (World.world == null || World.world.kingdoms == null)
            {
                return list;
            }

            foreach (Kingdom kingdom in World.world.kingdoms)
            {
                if (!kingdom.isCiv())
                {
                    continue;
                }

                CountryScore cs;
                if (!TryCollect(kingdom, out cs))
                {
                    continue;
                }
                list.Add(cs);
            }

            ScoreAll(list, weights);

            list.Sort(delegate(CountryScore a, CountryScore b)
            {
                int byScore = b.score.CompareTo(a.score);
                if (byScore != 0)
                {
                    return byScore;
                }
                return string.Compare(a.name, b.name, StringComparison.Ordinal);
            });

            return list;
        }

        // M1: isolate per-kingdom failures so one bad kingdom cannot abort the
        // whole refresh. The error is still logged (not silently swallowed).
        private static bool TryCollect(Kingdom kingdom, out CountryScore cs)
        {
            cs = null;
            try
            {
                cs = new CountryScore();
                cs.kingdom = kingdom;
                cs.name = (kingdom.data != null) ? kingdom.data.name : "?";

                // M2: single pass over cities, single pass over units.
                int cities = 0;
                int zones = 0;
                int warriors = 0;
                int buildings = 0;

                foreach (City city in kingdom.getCities())
                {
                    cities++;
                    zones += city.countZones();
                    warriors += city.countWarriors();
                    buildings += city.buildings.Count;
                }

                long economy = 0L;
                foreach (Actor actor in kingdom.getUnits())
                {
                    economy += (long)actor.money + (long)actor.loot;
                }

                cs.military = warriors;
                cs.economy = economy;
                cs.population = kingdom.getPopulationPeople();
                cs.territory = zones;
                cs.development = cities + buildings;
                cs.influence = kingdom.getRenown();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PowerRanking] skipped a kingdom while collecting data: " + ex.Message);
                return false;
            }
        }

        private static void ScoreAll(List<CountryScore> list, Weights weights)
        {
            int n = list.Count;
            if (n == 0)
            {
                return;
            }

            float[] m = ScoreDimension(list, delegate(CountryScore c) { return c.military; });
            float[] e = ScoreDimension(list, delegate(CountryScore c) { return c.economy; });
            float[] p = ScoreDimension(list, delegate(CountryScore c) { return c.population; });
            float[] t = ScoreDimension(list, delegate(CountryScore c) { return c.territory; });
            float[] d = ScoreDimension(list, delegate(CountryScore c) { return c.development; });
            float[] i = ScoreDimension(list, delegate(CountryScore c) { return c.influence; });

            for (int j = 0; j < n; j++)
            {
                list[j].subMilitary = m[j];
                list[j].subEconomy = e[j];
                list[j].subPopulation = p[j];
                list[j].subTerritory = t[j];
                list[j].subDevelopment = d[j];
                list[j].subInfluence = i[j];

                list[j].score = GeometricMean(m[j], e[j], p[j], t[j], d[j], i[j], weights);
            }
        }

        // Nonlinear transform: ln(1+x) with diminishing returns, then divided
        // by a winsorized upper reference (robust to a single extreme value).
        private static float[] ScoreDimension(List<CountryScore> list, Func<CountryScore, long> get)
        {
            int n = list.Count;
            float[] logValues = new float[n];
            for (int i = 0; i < n; i++)
            {
                long raw = get(list[i]);
                if (raw < 0L)
                {
                    raw = 0L;
                }
                logValues[i] = (float)Math.Log((double)raw + 1.0);
            }

            float reference = RobustReference(logValues);
            float[] result = new float[n];

            if (reference <= 0.0001f)
            {
                return result; // all zero -> all 0
            }

            for (int i = 0; i < n; i++)
            {
                float s = logValues[i] / reference;
                if (s > 1f) s = 1f;
                if (s < 0f) s = 0f;
                result[i] = s * 100f;
            }

            return result;
        }

        private static float RobustReference(float[] values)
        {
            if (values.Length == 0)
            {
                return 0f;
            }

            float[] sorted = (float[])values.Clone();
            Array.Sort(sorted);

            if (values.Length >= 20)
            {
                int idx = (int)Math.Floor(sorted.Length * 0.95);
                if (idx >= sorted.Length) idx = sorted.Length - 1;
                if (idx < 0) idx = 0;
                return sorted[idx];
            }

            return sorted[sorted.Length - 1];
        }

        // Weighted geometric mean over six 0..100 sub-scores, which penalizes
        // imbalance so a single strong dimension cannot fully mask weak ones.
        private static float GeometricMean(float m, float e, float p, float t, float d, float i, Weights w)
        {
            double lnSum =
                w.military * Math.Log(Math.Max(m, Floor)) +
                w.economy * Math.Log(Math.Max(e, Floor)) +
                w.population * Math.Log(Math.Max(p, Floor)) +
                w.territory * Math.Log(Math.Max(t, Floor)) +
                w.development * Math.Log(Math.Max(d, Floor)) +
                w.influence * Math.Log(Math.Max(i, Floor));

            double g = Math.Exp(lnSum);

            // Rescale [Floor, 100] back to [0, 100].
            double total = (g - Floor) / (100.0 - Floor);
            if (total < 0.0) total = 0.0;
            if (total > 1.0) total = 1.0;
            return (float)(total * 100.0);
        }
    }
}

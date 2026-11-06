using System;
using System.Collections.Generic;
using System.IO;

namespace PowerRanking
{
    public class Weights
    {
        public float military = 0.20f;
        public float economy = 0.20f;
        public float population = 0.15f;
        public float territory = 0.15f;
        public float development = 0.15f;
        public float influence = 0.15f;

        public static Weights Default()
        {
            return new Weights();
        }

        public static Weights Load(string path)
        {
            Weights w = Default();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return w;
            }

            try
            {
                Dictionary<string, float> values = new Dictionary<string, float>();
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    float parsed;
                    if (float.TryParse(line.Substring(eq + 1).Trim(), out parsed))
                    {
                        values[key] = parsed;
                    }
                }

                if (values.ContainsKey("military")) w.military = values["military"];
                if (values.ContainsKey("economy")) w.economy = values["economy"];
                if (values.ContainsKey("population")) w.population = values["population"];
                if (values.ContainsKey("territory")) w.territory = values["territory"];
                if (values.ContainsKey("development")) w.development = values["development"];
                else if (values.ContainsKey("cities")) w.development = values["cities"];
                if (values.ContainsKey("influence")) w.influence = values["influence"];
                else if (values.ContainsKey("renown")) w.influence = values["renown"];
            }
            catch (Exception)
            {
                // Keep defaults on any parse error.
            }

            Normalize(w);
            return w;
        }

        public static void Normalize(Weights w)
        {
            w.military = Max0(w.military);
            w.economy = Max0(w.economy);
            w.population = Max0(w.population);
            w.territory = Max0(w.territory);
            w.development = Max0(w.development);
            w.influence = Max0(w.influence);

            float sum = w.military + w.economy + w.population + w.territory + w.development + w.influence;
            if (sum <= 0f)
            {
                Weights d = Default();
                w.military = d.military;
                w.economy = d.economy;
                w.population = d.population;
                w.territory = d.territory;
                w.development = d.development;
                w.influence = d.influence;
                return;
            }

            w.military /= sum;
            w.economy /= sum;
            w.population /= sum;
            w.territory /= sum;
            w.development /= sum;
            w.influence /= sum;
        }

        private static float Max0(float value)
        {
            return value > 0f ? value : 0f;
        }
    }
}

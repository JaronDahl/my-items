using System.IO;
using NeoModLoader.api;
using UnityEngine;

namespace PowerRanking
{
    // NeoModLoader (NML) entry point. NML discovers this class because it is a
    // MonoBehaviour subclass that implements IMod (via BasicMod<T>).
    public class PowerRankingMod : BasicMod<PowerRankingMod>
    {
        private PowerRankingUI ui;
        private Weights weights;
        private float refreshTimer;

        protected override void OnModLoad()
        {
            string folder = GetDeclaration().FolderPath;
            string configPath = string.IsNullOrEmpty(folder)
                ? Path.Combine(Application.streamingAssetsPath, "mods", "PowerRanking.weights.txt")
                : Path.Combine(folder, "PowerRanking.weights.txt");

            weights = Weights.Load(configPath);

            GameObject uiGo = new GameObject("PowerRankingUI");
            uiGo.transform.SetParent(transform, false);
            ui = uiGo.AddComponent<PowerRankingUI>();
            ui.Setup(weights);

            Debug.Log("[PowerRanking] Loaded. Press F8 to toggle the ranking window.");
        }

        private void Update()
        {
            if (ui == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                ui.SetVisible(!ui.IsVisible());
            }

            if (ui.IsVisible())
            {
                refreshTimer += Time.deltaTime;
                if (refreshTimer >= 3f)
                {
                    refreshTimer = 0f;
                    ui.RefreshNow();
                }
            }
        }
    }
}

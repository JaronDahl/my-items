using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace PowerRanking
{
    // Reuses the game's own window prefab and replaces its list with our
    // ranking rows. Rows are pooled and reused across refreshes.
    public class PowerRankingUI : MonoBehaviour
    {
        private class RowView
        {
            public GameObject root;
            public Image background;
            public Text rankText;
            public Text nameText;
            public Text scoreText;
        }

        private ScrollWindow window;
        private Font font;
        private RectTransform contentRoot;
        private Weights weights;
        private List<CountryScore> lastRanking = new List<CountryScore>();
        private List<RowView> rowViews = new List<RowView>();
        private GameObject emptyIndicator;
        private bool headerBuilt;
        private bool forcePositionOnShow;
        private int selectedIndex = -1;
        private bool visible;

        public void Setup(Weights pWeights)
        {
            weights = pWeights;
            BuildWindow();
            visible = false;
        }

        public void SetVisible(bool pVisible)
        {
            if (window == null)
            {
                return;
            }

            visible = pVisible;
            window.gameObject.SetActive(pVisible);

            // Keep the window's own CanvasGroup in sync with our state so a
            // hide cannot be "stuck" at alpha 0 or non-interactable.
            CanvasGroup cg = window.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = pVisible ? 1f : 0f;
                cg.interactable = pVisible;
                cg.blocksRaycasts = pVisible;
            }

            if (pVisible)
            {
                RefreshNow();
            }
        }

        public bool IsVisible()
        {
            return visible;
        }

        // M5 fallback: only active when the internal create(false) reflection
        // failed. Runs once and clears itself, so it never fights dragging.
        private void LateUpdate()
        {
            if (forcePositionOnShow && window != null && window.gameObject.activeSelf)
            {
                window.transform.localPosition = Vector3.zero;
                forcePositionOnShow = false;
            }
        }

        public void RefreshNow()
        {
            if (window == null || !visible)
            {
                return;
            }

            lastRanking = PowerRankingEngine.Compute(weights);
            if (selectedIndex < 0 && lastRanking.Count > 0)
            {
                selectedIndex = 0;
            }
            if (selectedIndex >= lastRanking.Count)
            {
                selectedIndex = lastRanking.Count - 1;
            }

            UpdateRows();
            UpdateHighlight();
        }

        private void BuildWindow()
        {
            ScrollWindow prefab = Resources.Load<ScrollWindow>("windows/list_kingdoms");
            if (prefab == null)
            {
                Debug.LogError("[PowerRanking] could not load the list_kingdoms window prefab");
                return;
            }

            window = Object.Instantiate<ScrollWindow>(prefab, CanvasMain.instance.transformWindows);
            window.name = "power_ranking";
            window.screen_id = "power_ranking";
            window.historyActionEnabled = false;

            // M5: call the internal create(false) once; fall back to a one-shot
            // position correction if the reflection is unavailable.
            try
            {
                MethodInfo createMethod = typeof(ScrollWindow).GetMethod("create",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (createMethod != null)
                {
                    createMethod.Invoke(window, new object[] { false });
                }
                else
                {
                    forcePositionOnShow = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PowerRanking] create(false) reflection failed, using position fallback: " + ex.Message);
                forcePositionOnShow = true;
            }

            ListWindow listWindow = window.GetComponent<ListWindow>();
            if (listWindow != null)
            {
                Object.Destroy(listWindow);
            }

            if (window.tabs != null)
            {
                window.tabs.gameObject.SetActive(false);
            }

            SortingTab sortingTab = window.GetComponentInChildren<SortingTab>();
            if (sortingTab != null)
            {
                sortingTab.gameObject.SetActive(false);
            }

            KingdomListComponent listComponent = window.GetComponentInChildren<KingdomListComponent>();
            if (listComponent != null)
            {
                Object.Destroy(listComponent);
            }

            if (window.titleText != null)
            {
                font = window.titleText.font;
                window.titleText.text = "综合国力排行榜";
                window.titleText.raycastTarget = true;

                DragPanel drag = window.titleText.gameObject.AddComponent<DragPanel>();
                drag.target = window.transform as RectTransform;
            }
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            // M4: bind the original close button to hide this window.
            BindCloseButton();

            contentRoot = window.transform_content;
            ClearChildren(contentRoot);
            SetupContentLayout();

            window.transform.localPosition = Vector3.zero;
            window.gameObject.SetActive(false);
        }

        private void BindCloseButton()
        {
            Button closeBtn = FindCloseButton();
            if (closeBtn != null)
            {
                closeBtn.onClick.RemoveAllListeners();
                closeBtn.onClick.AddListener(delegate { SetVisible(false); });
            }
            else
            {
                Debug.LogWarning("[PowerRanking] close button not found; use F8 to close.");
            }
        }

        // Locate the real close button, first by the public close_background
        // reference, then by its serialized onClick method, then by name.
        private Button FindCloseButton()
        {
            if (window.close_background != null)
            {
                Button b = window.close_background.GetComponent<Button>();
                if (b != null)
                {
                    return b;
                }
                if (window.close_background.transform.parent != null)
                {
                    b = window.close_background.transform.parent.GetComponent<Button>();
                    if (b != null)
                    {
                        return b;
                    }
                }
            }

            foreach (Button b in window.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    string method = b.onClick.GetPersistentMethodName(i);
                    if (method == "clickCloseButton" || method == "clickHide" ||
                        method.ToLowerInvariant().Contains("close"))
                    {
                        return b;
                    }
                }
            }

            foreach (Button b in window.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name.ToLowerInvariant().Contains("close"))
                {
                    return b;
                }
            }

            return null;
        }

        private void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        private void SetupContentLayout()
        {
            VerticalLayoutGroup vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 3f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentRoot.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            }
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void EnsureHeader()
        {
            if (headerBuilt)
            {
                return;
            }
            headerBuilt = true;

            RectTransform row = MakeRowRect();
            row.GetComponent<LayoutElement>().preferredHeight = 22f;
            AddCell(row, "#", 36f, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.85f, 1f), 13);
            AddCell(row, "国家", 0f, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.85f, 1f), 13);
            AddCell(row, "分数", 52f, TextAnchor.MiddleRight, new Color(0.8f, 0.8f, 0.85f, 1f), 13);
        }

        // Reuse existing rows, only create/destroy on count change.
        private void UpdateRows()
        {
            EnsureHeader();

            if (lastRanking.Count == 0)
            {
                DestroyRows();
                if (emptyIndicator == null)
                {
                    emptyIndicator = MakeCenteredText("暂无文明国家");
                }
                else
                {
                    emptyIndicator.SetActive(true);
                }
                return;
            }

            if (emptyIndicator != null)
            {
                emptyIndicator.SetActive(false);
            }

            for (int i = 0; i < lastRanking.Count; i++)
            {
                RowView rv;
                if (i < rowViews.Count)
                {
                    rv = rowViews[i];
                }
                else
                {
                    rv = CreateRow();
                    rowViews.Add(rv);
                }
                FillRow(rv, i);
            }

            for (int i = rowViews.Count - 1; i >= lastRanking.Count; i--)
            {
                Object.Destroy(rowViews[i].root);
                rowViews.RemoveAt(i);
            }
        }

        private RowView CreateRow()
        {
            RectTransform rect = MakeRowRect();
            rect.GetComponent<LayoutElement>().preferredHeight = 24f;

            Image bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);

            Button btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            int captured = rowViews.Count;
            btn.onClick.AddListener(delegate { Select(captured); });

            RowView rv = new RowView();
            rv.root = rect.gameObject;
            rv.background = bg;
            rv.rankText = AddCell(rect, "", 36f, TextAnchor.MiddleLeft, Color.white, 13);
            rv.nameText = AddCell(rect, "", 0f, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.72f, 1f), 13);
            rv.scoreText = AddCell(rect, "", 52f, TextAnchor.MiddleRight, Color.white, 13);
            return rv;
        }

        private void FillRow(RowView rv, int index)
        {
            CountryScore cs = lastRanking[index];
            string rank = (index + 1).ToString();
            string score = cs.score.ToString("0.0");
            if (rv.rankText.text != rank) rv.rankText.text = rank;
            if (rv.nameText.text != cs.name) rv.nameText.text = cs.name;
            if (rv.scoreText.text != score) rv.scoreText.text = score;
        }

        private void UpdateHighlight()
        {
            for (int i = 0; i < rowViews.Count; i++)
            {
                Color c = (i == selectedIndex)
                    ? new Color(0.22f, 0.24f, 0.32f, 1f)
                    : new Color(0f, 0f, 0f, 0f);
                rowViews[i].background.color = c;
            }
        }

        private void DestroyRows()
        {
            for (int i = rowViews.Count - 1; i >= 0; i--)
            {
                Object.Destroy(rowViews[i].root);
            }
            rowViews.Clear();
        }

        private RectTransform MakeRowRect()
        {
            GameObject go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(contentRoot, false);
            go.AddComponent<LayoutElement>();

            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(4, 4, 0, 0);
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = true;

            return go.GetComponent<RectTransform>();
        }

        private Text AddCell(RectTransform row, string text, float width, TextAnchor align, Color color, int size)
        {
            RectTransform cell = MakeRect("Cell", row);
            LayoutElement le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = (width <= 0f) ? 1f : 0f;

            return MakeText(cell, text, size, align, color);
        }

        private GameObject MakeCenteredText(string text)
        {
            RectTransform rt = MakeRect("Empty", contentRoot);
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 60f;
            MakeText(rt, text, 15, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.75f, 1f));
            return rt.gameObject;
        }

        private void Select(int index)
        {
            if (index < 0 || index >= lastRanking.Count)
            {
                return;
            }
            selectedIndex = index;
            UpdateHighlight();
        }

        private Text MakeText(RectTransform parent, string text, int size, TextAnchor align, Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private RectTransform MakeRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}

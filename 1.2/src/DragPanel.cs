using UnityEngine;
using UnityEngine.EventSystems;

namespace PowerRanking
{
    // Lets the title text drag the whole window.
    public class DragPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform target;
        private Vector2 dragOffset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target, eventData.position, eventData.pressEventCamera, out local))
            {
                dragOffset = target.anchoredPosition - local;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            RectTransform parentRect = target.parent as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventData.pressEventCamera, out local))
            {
                target.anchoredPosition = local + dragOffset;
            }
        }
    }
}

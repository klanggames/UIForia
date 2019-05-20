using UIForia.Attributes;
using UIForia.Layout;
using UIForia.Rendering;
using UIForia.UIInput;
using UnityEngine;

namespace UIForia.Elements {

    [Template(TemplateType.Internal, "Elements/ScrollView.xml")]
    public class ScrollView : UIElement {

        public UIElement targetElement { get; protected set; }
        public UIElement verticalHandle { get; protected set; }
        public UIElement verticalTrack { get; protected set; }

        public UIElement horizontalHandle { get; protected set; }
        public UIElement horizontalTrack { get; protected set; }

        public float scrollSpeed = 0.05f;
        public float fadeTime = 2f;

        protected float lastScrollVerticalTimestamp;
        protected float lastScrollHorizontalTimestamp;

        // todo -- without layout system integration this is an overlay scroll bar only

        public override void OnReady() {
            targetElement = FindFirstByType<UIChildrenElement>().GetChild(0);
            verticalHandle = FindById("scroll-handle-vertical");
            verticalTrack = FindById("scroll-track-vertical");
            horizontalHandle = FindById("scroll-handle-horizontal");
            horizontalTrack = FindById("scroll-track-horizontal");
        }

        [OnMouseWheel]
        public void OnMouseWheel(MouseInputEvent evt) {
            if (!verticalTrack.isEnabled) return;
            lastScrollVerticalTimestamp = Time.realtimeSinceStartup;
            float trackRectHeight = verticalTrack.layoutResult.allocatedSize.height;
            float handleHeight = verticalHandle.layoutResult.allocatedSize.height;
            float max = trackRectHeight - handleHeight;
            float offset = Mathf.Clamp(targetElement.scrollOffset.y - (scrollSpeed * evt.ScrollDelta.y), 0, 1);
            targetElement.scrollOffset = new Vector2(targetElement.scrollOffset.x, offset);
            verticalHandle.style.SetTransformPositionY(offset * (max), StyleState.Normal);
            evt.StopPropagation();
        }

        public void OnHoverHorizontal(MouseInputEvent evt) {
            lastScrollHorizontalTimestamp = Time.realtimeSinceStartup;
        }

        public void OnHoverVertical(MouseInputEvent evt) {
            lastScrollVerticalTimestamp = Time.realtimeSinceStartup;
        }
        
        public override void OnUpdate() {
            Size actualSize = targetElement.layoutResult.actualSize;
            Size allocatedSize = targetElement.layoutResult.allocatedSize;

            if (actualSize.width <= allocatedSize.width) {
                horizontalTrack.SetEnabled(false);
            }
            else {
                horizontalTrack.SetEnabled(true);
                float width = (allocatedSize.width / actualSize.width) * allocatedSize.width;
                float opacity =  1 - Mathf.Clamp01(Easing.Interpolate((Time.realtimeSinceStartup - lastScrollHorizontalTimestamp) / fadeTime, EasingFunction.CubicEaseInOut));
                horizontalHandle.style.SetPreferredWidth(width, StyleState.Normal);
                horizontalTrack.style.SetOpacity(opacity, StyleState.Normal);
            }

            if (actualSize.height <= allocatedSize.height) {
                verticalTrack.SetEnabled(false);
            }
            else {
                verticalTrack.SetEnabled(true);
                float height = (allocatedSize.height / actualSize.height) * allocatedSize.height;
                float opacity = 1 - Mathf.Clamp01(Easing.Interpolate((Time.realtimeSinceStartup - lastScrollVerticalTimestamp) / fadeTime, EasingFunction.CubicEaseInOut));
                verticalHandle.style.SetPreferredHeight(height, StyleState.Normal);
                verticalTrack.style.SetOpacity(opacity, StyleState.Normal);
            }
        }

        public void OnClickVertical(MouseInputEvent evt) {
            if (!verticalTrack.isEnabled) return;
            lastScrollVerticalTimestamp = Time.realtimeSinceStartup;
            float trackRectHeight = verticalTrack.layoutResult.allocatedSize.height;
            float targetHeight = targetElement.layoutResult.actualSize.height;
            float handleTop = verticalHandle.layoutResult.screenPosition.y;
            float handleBottom = handleTop + verticalHandle.layoutResult.allocatedSize.height;
            float pageSize = trackRectHeight;
            float direction = 0;
            if (evt.MousePosition.y < handleTop) {
                direction = -1;
            }
            else if (evt.MousePosition.y > handleBottom) {
                direction = 1;
            }
            float handleHeight = verticalHandle.layoutResult.allocatedSize.height;
            float max = trackRectHeight - handleHeight;
            float offset = Mathf.Clamp(targetElement.scrollOffset.y + (direction * (pageSize / targetHeight)), 0, 1);
            targetElement.scrollOffset = new Vector2(targetElement.scrollOffset.x, offset);
            verticalHandle.style.SetTransformPositionY(offset * (max), StyleState.Normal);
            evt.StopPropagation();
        }
        
        protected virtual DragEvent OnCreateVerticalDrag(MouseInputEvent evt) {
            lastScrollVerticalTimestamp = Time.realtimeSinceStartup;
            float trackRectY = verticalTrack.layoutResult.screenPosition.y;
            float handlePosition = verticalHandle.layoutResult.screenPosition.y;
            float baseOffset = evt.MouseDownPosition.y - (trackRectY + handlePosition);
            return new ScrollbarDragEvent(ScrollbarOrientation.Vertical, baseOffset, this);
        }

        protected virtual DragEvent OnCreateHorizontalDrag(MouseInputEvent evt) {
            lastScrollHorizontalTimestamp = Time.realtimeSinceStartup;
            float trackRectX = horizontalTrack.layoutResult.screenPosition.x;
            float handlePosition = horizontalHandle.layoutResult.screenPosition.x;
            float baseOffset = evt.MouseDownPosition.x - (trackRectX + handlePosition);
            return new ScrollbarDragEvent(ScrollbarOrientation.Horizontal, baseOffset, this);
        }

        public class ScrollbarDragEvent : DragEvent {

            public readonly float baseOffset;
            public readonly ScrollView scrollbar;
            public readonly ScrollbarOrientation orientation;

            public ScrollbarDragEvent(ScrollbarOrientation orientation, float baseOffset, ScrollView scrollbar) : base(scrollbar.targetElement) {
                this.orientation = orientation;
                this.baseOffset = baseOffset;
                this.scrollbar = scrollbar;
            }

            public override void Update() {
                if (orientation == ScrollbarOrientation.Vertical) {
                    scrollbar.lastScrollVerticalTimestamp = Time.realtimeSinceStartup;
                    float trackRectY = scrollbar.verticalTrack.layoutResult.screenPosition.y;
                    float trackRectHeight = scrollbar.verticalTrack.layoutResult.allocatedSize.height;
                    float handleHeight = scrollbar.verticalHandle.layoutResult.allocatedSize.height;
                    float max = trackRectHeight - handleHeight;
                    float offset = Mathf.Clamp(MousePosition.y - trackRectY - baseOffset, 0, max);
                    scrollbar.targetElement.scrollOffset = new Vector2(scrollbar.targetElement.scrollOffset.x, offset / max);
                    scrollbar.verticalHandle.style.SetTransformPositionY(offset, StyleState.Normal);
                }
                else {
                    scrollbar.lastScrollHorizontalTimestamp = Time.realtimeSinceStartup;
                    float trackRectX = scrollbar.horizontalTrack.layoutResult.screenPosition.x;
                    float trackRectWidth = scrollbar.horizontalTrack.layoutResult.allocatedSize.width;
                    float handleWidth = scrollbar.horizontalHandle.layoutResult.allocatedSize.width;
                    float max = trackRectWidth - handleWidth;
                    float offset = Mathf.Clamp(MousePosition.x - trackRectX - baseOffset, 0, max);
                    scrollbar.targetElement.scrollOffset = new Vector2(offset / max, scrollbar.targetElement.scrollOffset.y);
                    scrollbar.horizontalHandle.style.SetTransformPositionX(offset, StyleState.Normal);
                }
            }

        }

    }

}
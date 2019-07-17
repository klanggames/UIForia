using System;
using UIForia.Animation;
using UIForia.Attributes;
using UIForia.Elements;
using UIForia.Extensions;
using UIForia.Layout;
using UIForia.Rendering;
using UIForia.UIInput;
using UIForia.Util;
using UnityEngine;

namespace UI {

    [Template("KlangWindow/KlangWindow.xml")]
    public class KlangWindow : UIElement, IPointerQueryHandler {

        public bool isOpen;

        private UIElement titlebarButtons;
        private UIElement runner;
        private UIElement sidebar;
        private UIElement currentSidebarItem;

        public string windowTitle = "Window Title";

        public WindowSide allowedDragSides = WindowSide.Top | WindowSide.Right | WindowSide.Bottom | WindowSide.Left;

        public event Action onOpen;
        public event Action onClose;

        public override void OnCreate() {
            allowedDragSides &= ~(WindowSide.Left);
            runner = FindById("side-bar-runner");
            sidebar = FindById("side-bar");
            currentSidebarItem = sidebar?.GetChild(1);
            if (currentSidebarItem != null) {
                runner?.style.SetTransformPositionY(currentSidebarItem.layoutResult.localPosition.y, StyleState.Normal);
            }
            else {
                runner?.SetEnabled(false);
            }
        }

        public void Open() {
            isOpen = true;
            onOpen?.Invoke();
        }

        public void Close() {
            isOpen = false;
            onClose?.Invoke();
        }

        [OnDragCreate]
        public DragEvent ResizeWindow(MouseInputEvent evt) {
            const float growFactor = 3f;

            Rect screenRect = layoutResult.ScreenRect.Grow(growFactor);
            Vector2 mouse = evt.MouseDownPosition;

            WindowSide side = 0;

            if ((allowedDragSides & WindowSide.Top) != 0 && MathUtil.Between(mouse.y, screenRect.yMin - (growFactor * 2), screenRect.yMin + growFactor)) {
                side |= WindowSide.Top;
            }

            if ((allowedDragSides & WindowSide.Right) != 0 && MathUtil.Between(mouse.x, screenRect.xMax - (growFactor * 2), screenRect.xMax + growFactor)) {
                side |= WindowSide.Right;
            }

            if ((allowedDragSides & WindowSide.Bottom) != 0 && MathUtil.Between(mouse.y, screenRect.yMax - (growFactor * 2), screenRect.yMax + growFactor)) {
                side |= WindowSide.Bottom;
            }

            if ((allowedDragSides & WindowSide.Left) != 0 && MathUtil.Between(mouse.x, screenRect.xMin - (growFactor * 2), screenRect.xMin + growFactor)) {
                side |= WindowSide.Left;
            }

            if (side != 0) {
                return new WindowResizeEvent(this, side);
            }

            return null;
        }

        public void SelectSidebarItem(UIElement element) {
            AnimationOptions options = new AnimationOptions();
            options.duration = 250;
            options.timingFunction = EasingFunction.CubicEaseIn;

            if (currentSidebarItem == null) {
                currentSidebarItem = element;
                runner?.style.SetTransformPositionY(element.layoutResult.localPosition.y, StyleState.Normal);
            }

            if (currentSidebarItem == element) {
                return;
            }

            AnimationKeyFrame[] frames = {
                new AnimationKeyFrame(0,
                    StyleProperty.TransformPositionY(currentSidebarItem.layoutResult.localPosition.y)
                ),
                new AnimationKeyFrame(1,
                    StyleProperty.TransformPositionY(element.layoutResult.localPosition.y)
                )
            };

            currentSidebarItem = element;

            Application.Animate(runner, new AnimationData(options, frames));
        }

        public bool ContainsPoint(Vector2 point) {
            return layoutResult.ScreenRect.Grow(3).ContainOrOverlap(point);
        }

        public DragEvent CreateDrag_TitleBar(MouseInputEvent evt) {
            titlebarButtons = titlebarButtons ?? FindById("title-button-group");
            if (evt.Origin.IsAncestorOf(titlebarButtons)) {
                return null;
            }

            return new WindowDragEvent(this, evt.MouseDownPosition - layoutResult.screenPosition + View.Viewport.position);
        }


        public class WindowDragEvent : DragEvent {

            private readonly Vector2 offset;

            public WindowDragEvent(UIElement origin, Vector2 offset) : base(origin) {
                this.offset = offset;
            }

            public override void Update() {
                origin.style.SetTransformPosition(MousePosition - offset, StyleState.Normal);
            }

        }

        public class WindowResizeEvent : DragEvent {

            private readonly WindowSide windowSide;
            private readonly Size originalSize;
            private readonly Vector2 originalLocalPosition;

            public WindowResizeEvent(UIElement origin, WindowSide side) : base(origin) {
                this.windowSide = side;
                this.originalLocalPosition = origin.layoutResult.localPosition;
                this.originalSize = origin.layoutResult.actualSize;
            }

            public override void Update() {
                Vector2 localMouse = MousePosition - origin.layoutResult.screenPosition;

                if ((windowSide & WindowSide.Top) != 0) {
                    float y = origin.layoutResult.localPosition.y + localMouse.y;
                    float height = originalSize.height + (originalLocalPosition.y - y);
                    origin.style.SetTransformPositionY(y, StyleState.Normal);
                    origin.style.SetPreferredHeight(height, StyleState.Normal);
                }

                if ((windowSide & WindowSide.Bottom) != 0) {
                    origin.style.SetPreferredHeight(localMouse.y, StyleState.Normal);
                }

                if ((windowSide & WindowSide.Left) != 0) {
                    float x = origin.layoutResult.localPosition.x + localMouse.x;
                    float width = originalSize.width + (originalLocalPosition.x - x);
                    origin.style.SetTransformPositionX(x, StyleState.Normal);
                    origin.style.SetPreferredWidth(width, StyleState.Normal);
                }

                if ((windowSide & WindowSide.Right) != 0) {
                    origin.style.SetPreferredWidth(localMouse.x, StyleState.Normal);
                }
            }

        }

        [Flags]
        public enum WindowSide {

            Top = 1 << 0,
            Bottom = 1 << 1,
            Left = 1 << 2,
            Right = 1 << 3

        }

    }

}
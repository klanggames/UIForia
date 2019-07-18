using System;
using System.Collections.Generic;
using System.Diagnostics;
using SVGX;
using UIForia.Elements;
using UIForia.Extensions;
using UIForia.Layout;
using UIForia.Layout.LayoutTypes;
using UIForia.Rendering;
using UIForia.Util;
using UnityEngine;
using RectExtensions = Vertigo.RectExtensions;

namespace UIForia.Systems {

    public class LayoutSystem : ILayoutSystem {

        public const int TextLayoutPoolKey = 100;
        public const int ImageLayoutPoolKey = 200;

        protected readonly IStyleSystem m_StyleSystem;
        protected readonly IntMap<LayoutBox> m_LayoutBoxMap;
        protected readonly LightList<TextLayoutBox> m_TextLayoutBoxes;

        private readonly LightList<LayoutBox> m_VisibleBoxList;

        private static readonly IComparer<LayoutBox> comparer = new DepthComparer();
        private readonly Dictionary<int, LayoutBoxPool> layoutBoxPoolMap;
        private readonly LightList<LayoutBox> toLayout = new LightList<LayoutBox>(128);
        private readonly Application application;
        private readonly LightList<LayoutBox> leaves;

        public LayoutSystem(Application application, IStyleSystem styleSystem) {
            this.application = application;
            this.m_StyleSystem = styleSystem;
            this.m_LayoutBoxMap = new IntMap<LayoutBox>();
            this.m_VisibleBoxList = new LightList<LayoutBox>();
            this.m_TextLayoutBoxes = new LightList<TextLayoutBox>(64);
            this.m_StyleSystem.onStylePropertyChanged += HandleStylePropertyChanged;
            this.layoutBoxPoolMap = new Dictionary<int, LayoutBoxPool>();
            this.leaves = new LightList<LayoutBox>(64);

            this.layoutBoxPoolMap[(int) LayoutType.Flex] = new LayoutBoxPool<FlexLayoutBox>();
            this.layoutBoxPoolMap[(int) LayoutType.Grid] = new LayoutBoxPool<GridLayoutBox>();
            this.layoutBoxPoolMap[(int) LayoutType.Radial] = new LayoutBoxPool<RadialLayoutBox>();
            this.layoutBoxPoolMap[(int) LayoutType.Fixed] = new LayoutBoxPool<FixedLayoutBox>();
            this.layoutBoxPoolMap[(int) LayoutType.Flow] = new LayoutBoxPool<FlowLayoutBox>();
            this.layoutBoxPoolMap[TextLayoutPoolKey] = new LayoutBoxPool<TextLayoutBox>();
            this.layoutBoxPoolMap[ImageLayoutPoolKey] = new LayoutBoxPool<ImageLayoutBox>();
        }

        public void OnReset() {
            m_LayoutBoxMap.Clear();
            m_VisibleBoxList.Clear();
        }

        public void OnUpdate() {
            // todo -- should this be a list per-view?
            m_VisibleBoxList.Clear();

            TextLayoutBox[] textLayouts = m_TextLayoutBoxes.Array;
            for (int i = 0; i < m_TextLayoutBoxes.Count; i++) {
                if (textLayouts[i].TextInfo.LayoutDirty) {
                    textLayouts[i].RequestContentSizeChangeLayout();
                }
            }

            for (int i = 0; i < application.m_Views.Count; i++) {
                RunLayout(application.m_Views[i]);
            }
        }

        /*
         * for each view
         *     gather list of elements to consider for layout
         *     
         *     if element children exceed width bounds
         *     if overflow y is scroll & scroll layout is push content
         *     run scrollbar vertical layout
         *     run layout again w/ width - scroll bar width
         *     set clip rect x - width to 0 -> scroll bar x (or invert if scroll position inverted)
         *
         *     traverse to find things to layout
         *     run those layouts
         *     compute matrices
         *     compute clip rects
         *     update query grid
         *     copy properites to layout result
         */

        private void CollectLayoutBoxes(UIView view) {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            UIElement rootElement = view.rootElement;

            for (int i = 0; i < rootElement.children.Count; i++) {
                stack.Push(rootElement.children[i]);
            }

            int idx = 0;

            toLayout.QuickClear();
            int elementCount = view.GetElementCount();
            toLayout.EnsureCapacity(elementCount);
            LayoutBox[] toLayoutArray = toLayout.Array;

            int viewDepth = view.Depth;

            while (stack.Count > 0) {
                UIElement currentElement = stack.PopUnchecked();

                if (currentElement.isDisabled) {
                    continue;
                }

                LayoutBox currentBox = m_LayoutBoxMap.GetOrDefault(currentElement.id);
                currentBox.traversalIndex = idx;
                currentBox.viewDepthIdx = viewDepth;

                toLayoutArray[idx++] = currentBox;

                UIElement[] childArray = currentElement.children.Array;
                int childCount = currentElement.children.Count;
                for (int i = childCount - 1; i >= 0; i--) {
                    stack.Push(childArray[i]);
                }
            }

            toLayout.Count = idx;
            LightStack<UIElement>.Release(ref stack);
        }

        // todo -- build quad tree for queries
        private unsafe struct QuadTreeNode {

            public fixed int ids[12]; // index into visibility list
            public fixed int children[4];
            public int x;
            public int y;
            public int width;
            public int height;

        }


        public void RunLayout(UIView view) {
            m_VisibleBoxList.QuickClear();
            view.visibleElements.QuickClear();

            UIElement rootElement = view.rootElement;

            LayoutBox rootBox = m_LayoutBoxMap.GetOrDefault(rootElement.id);
            rootBox.element.layoutResult.matrix = new SVGXMatrix(1, 0, 0, 1, view.position.x, view.position.y);

            rootBox.prefWidth = new UIMeasurement(1, UIMeasurementUnit.ViewportWidth);
            rootBox.prefHeight = new UIMeasurement(1, UIMeasurementUnit.ViewportHeight);

            rootBox.clipRect = new Rect(0, 0, Screen.width, Screen.height);

            rootBox.allocatedWidth = view.Viewport.width;
            rootBox.allocatedHeight = view.Viewport.height;
            rootBox.actualWidth = rootBox.allocatedWidth;
            rootBox.actualHeight = rootBox.allocatedHeight;

            CollectLayoutBoxes(view);

            leaves.QuickClear();

            LayoutBox[] toLayoutArray = toLayout.Array;
            int toLayoutCount = toLayout.Count;

            // todo -- only if changed
            if (view.sizeChanged) {
                for (int i = 0; i < toLayoutCount; i++) {
                    toLayoutArray[i].UpdateViewSizeProperties();
                }

                rootBox.RunLayout();
                view.sizeChanged = false; // todo - dont do this here
            }

            for (int i = 0; i < toLayoutCount; i++) {
                LayoutBox box = toLayoutArray[i];

                if (box.IsIgnored) {
                    float currentWidth = box.allocatedWidth;
                    float currentHeight = box.allocatedHeight;
                    box.allocatedWidth = box.GetWidths().clampedSize;
                    box.allocatedHeight = box.GetHeights(box.allocatedWidth).clampedSize;
                    box.localX = 0;
                    box.localY = 0;
                    if (box.allocatedWidth != currentWidth || box.allocatedHeight != currentHeight) {
                        box.markedForLayout = true;
                    }
                }

#if DEBUG && UNITY_EDITOR
                bool debug = (box.element.flags & UIElementFlags.DebugLayout) != 0;
                if (debug || box.markedForLayout) {
                    if (debug) {
                        Debugger.Break();
                    }

                    box.RunLayout();
                    box.markedForLayout = false;
                }
#else
                if (box.markedForLayout) {
                   box.RunLayout();
                   box.markedForLayout = false;
                }
#endif

                if (box.children.Count == 0) {
                    leaves.Add(box);
                }

                box.xMax = box.actualWidth;
                box.yMax = box.actualHeight;
            }

            ProcessLeaves();

            UpdateLayoutResults(toLayoutCount, toLayoutArray);

            m_VisibleBoxList.EnsureAdditionalCapacity(toLayoutCount);

            CullCheck(toLayoutArray, toLayoutCount);

            m_VisibleBoxList.Sort(comparer);

            LayoutBox[] boxes = m_VisibleBoxList.Array;
            view.visibleElements.EnsureCapacity(m_VisibleBoxList.Count);
            for (int i = 0; i < m_VisibleBoxList.Count; i++) {
                boxes[i].element.layoutResult.zIndex = i + 1;
                view.visibleElements.AddUnchecked(boxes[i].element);
            }
        }

        private void ProcessLeaves() {
            int leafCount = leaves.Count;
            LayoutBox[] leafArray = leaves.Array;

            for (int i = 0; i < leafCount; i++) {
                LayoutBox current = leafArray[i];
                LayoutBox ptr = current.parent;

                current.xMax = current.localX + current.actualWidth;
                current.yMax = current.localY + current.actualHeight;

                // each element needs a clip rect
                // how do I find it?

                while (ptr != null) {
                    // todo -- dont look up overflow values, cache them
                    if (current.overflowX != Overflow.Visible) {
                        ptr.xMax = ptr.xMax > current.actualWidth ? ptr.xMax : current.actualWidth;
                    }
                    else if (ptr.overflowX != Overflow.Visible) {
                        ptr.xMax = ptr.xMax > current.xMax ? ptr.xMax : current.xMax;
                    }
                    else {
                        ptr.xMax = ptr.xMax > ptr.localX + current.xMax ? ptr.xMax : ptr.localX + current.xMax;
                    }

                    if (current.overflowY != Overflow.Visible) {
                        ptr.yMax = ptr.yMax > current.actualHeight ? ptr.yMax : current.actualHeight;
                    }
                    else if (ptr.overflowY != Overflow.Visible) {
                        ptr.yMax = ptr.yMax > current.yMax ? ptr.yMax : current.yMax;
                    }
                    else {
                        ptr.yMax = ptr.yMax > ptr.localY + current.yMax ? ptr.yMax : ptr.localY + current.yMax;
                    }

                    current = ptr;
                    ptr = ptr.parent;
                }
            }
        }

        private static void UpdateLayoutResults(int toLayoutCount, LayoutBox[] toLayoutArray) {
            for (int i = 0; i < toLayoutCount; i++) {
                LayoutBox box = toLayoutArray[i];

                Vector2 scrollOffset = default;

                LayoutBox parentBox = box.parent;

                if (!box.IsIgnored) {
                    scrollOffset.x = (parentBox.xMax - parentBox.allocatedWidth) * parentBox.element.scrollOffset.x;
                    scrollOffset.y = (parentBox.yMax - parentBox.allocatedHeight) * parentBox.element.scrollOffset.y;
                }

                // clip rect is nearest ancestor w/ overflow handling on a given axis
                // can pass down top to bottom so that parents clip their children according to their parent's clip bounds
                LayoutResult layoutResult = box.element.layoutResult;

                box.clipRect = parentBox.clipRect;
                layoutResult.clipRect = parentBox.clipRect;

                Vector2 localPosition = ResolveLocalPosition(box) - scrollOffset;
                Vector2 localScale = default;
                localScale.x = box.transformScaleX;
                localScale.y = box.transformScaleY;

                Vector2 pivot = box.pivot;
                SVGXMatrix m;

                if (box.transformRotation != 0) {
                    m = SVGXMatrix.TRS(localPosition, layoutResult.rotation, layoutResult.scale);
                }
                else {
                    m = SVGXMatrix.TranslateScale(localPosition.x, localPosition.y, localScale.x, localScale.y);
                }

                SVGXMatrix parentMatrix = box.parent.element.layoutResult.matrix;

                if (pivot.x != 0 || pivot.y != 0) {
                    SVGXMatrix pivotMat = SVGXMatrix.Translation(new Vector2(box.allocatedWidth * pivot.x, box.allocatedHeight * pivot.y));
                    m = pivotMat * m * pivotMat.Inverse();
                }

                // m = parentMatrix * m; might be backwards :/
                m = new SVGXMatrix(
                    m.m0 * parentMatrix.m0 + m.m2 * parentMatrix.m1,
                    m.m1 * parentMatrix.m0 + m.m3 * parentMatrix.m1,
                    m.m0 * parentMatrix.m2 + m.m2 * parentMatrix.m3,
                    m.m1 * parentMatrix.m2 + m.m3 * parentMatrix.m3,
                    m.m0 * parentMatrix.m4 + m.m2 * parentMatrix.m5 + m.m4,
                    m.m1 * parentMatrix.m4 + m.m3 * parentMatrix.m5 + m.m5
                );
                
                layoutResult.matrix = m;

                layoutResult.overflowSize.width = box.xMax;
                layoutResult.overflowSize.height = box.yMax;

                layoutResult.localPosition = localPosition;

                layoutResult.actualSize.width = box.actualWidth;
                layoutResult.actualSize.height = box.actualHeight;

                layoutResult.allocatedSize.width = box.allocatedWidth;
                layoutResult.allocatedSize.height = box.allocatedHeight;

                layoutResult.screenPosition = m.position;

                layoutResult.scale.x = localScale.x;
                layoutResult.scale.y = localScale.y;
                layoutResult.rotation = box.transformRotation;
                layoutResult.pivot = pivot;

                layoutResult.borderRadius.topLeft = box.resolvedBorderRadiusTopLeft; 
                layoutResult.borderRadius.topRight = box.resolvedBorderRadiusTopRight; 
                layoutResult.borderRadius.bottomRight = box.resolvedBorderRadiusBottomRight; 
                layoutResult.borderRadius.bottomLeft = box.resolvedBorderRadiusBottomLeft;
                
                layoutResult.border.top = box.resolvedBorderTop;
                layoutResult.border.right = box.resolvedBorderRight;
                layoutResult.border.bottom = box.resolvedBorderBottom;
                layoutResult.border.left =  box.resolvedBorderLeft;

                layoutResult.padding.top = box.resolvedPaddingTop;
                layoutResult.padding.right = box.resolvedPaddingRight;
                layoutResult.padding.bottom = box.resolvedPaddingBottom;
                layoutResult.padding.left = box.resolvedPaddingLeft;

                if (box.overflowX != Overflow.Visible) {
                    // use own value for children
                    box.clipRect.x = m.position.x;
                    box.clipRect.width = box.allocatedWidth;
                }

                if (box.overflowY != Overflow.Visible) {
                    box.clipRect.y = m.position.y;
                    box.clipRect.height = box.allocatedHeight;
                }
            }
        }

        private void CullCheck(LayoutBox[] toLayoutArray, int toLayoutCount) {
            float screenXMax = Screen.width;
            float screenYMax = Screen.height;
            LayoutBox[] output = m_VisibleBoxList.array;
            int size = m_VisibleBoxList.size;

            for (int i = 0; i < toLayoutCount; i++) {
                // if height or width is zero
                // if parent overflow is hidden & parent clip bounds ! contains or intersects children
                // if parent overflow is h
                UIElement element = toLayoutArray[i].element;

                if (element.layoutResult.actualSize.width == 0 || element.layoutResult.actualSize.height == 0) {
                    continue;
                }

                float xMin = element.layoutResult.screenPosition.x;
                float xMax = xMin + element.layoutResult.actualSize.width;
                float yMin = element.layoutResult.screenPosition.y;
                float yMax = yMin + element.layoutResult.actualSize.height;

                if (!(xMax > 0 && xMin < screenXMax && yMax > 0 && yMin < screenYMax)) {
                    continue;
                }

                output[size++] = toLayoutArray[i];
            }

            m_VisibleBoxList.size = size;
        }

        private static Vector2 ResolveLocalPosition(LayoutBox box) {
            Vector2 localPosition = Vector2.zero;

            LayoutBehavior layoutBehavior = box.layoutBehavior;
            TransformBehavior transformBehaviorX = box.transformBehaviorX;
            TransformBehavior transformBehaviorY = box.transformBehaviorY;

            switch (layoutBehavior) {
                case LayoutBehavior.TranscludeChildren:
                    localPosition = new Vector2(0, 0);
                    break;

                case LayoutBehavior.Ignored:
                case LayoutBehavior.Normal:

                    switch (transformBehaviorX) {
                        case TransformBehavior.AnchorMinOffset:
                            localPosition.x = box.ResolveAnchorLeft() + box.ResolveTransform(box.transformPositionX);
                            break;
                        case TransformBehavior.AnchorMaxOffset:
                            box.ResolveAnchorRight();
                            localPosition.x = box.ResolveAnchorRight() - box.ResolveTransform(box.transformPositionX) - box.actualWidth;
                            break;
                        case TransformBehavior.LayoutOffset:
                            localPosition.x = box.localX + box.ResolveTransform(box.transformPositionX);
                            break;
                        default:
                            localPosition.x = box.localX;
                            break;
                    }

                    switch (transformBehaviorY) {
                        case TransformBehavior.AnchorMinOffset:
                            localPosition.y = box.ResolveAnchorTop() + box.ResolveTransform(box.transformPositionY);
                            break;
                        case TransformBehavior.AnchorMaxOffset:
                            localPosition.y = box.ResolveAnchorBottom() - box.ResolveTransform(box.transformPositionY) - box.actualHeight;
                            break;
                        case TransformBehavior.LayoutOffset:
                            localPosition.y = box.localY + box.ResolveTransform(box.transformPositionY);
                            break;
                        default:
                            localPosition.y = box.localY;
                            break;
                    }

                    break;
            }

            return localPosition;
        }

        public void OnDestroy() { }

        public void OnViewAdded(UIView view) {
            CreateLayoutBox(view.rootElement);
        }

        public void OnViewRemoved(UIView view) {
            m_LayoutBoxMap.GetOrDefault(view.rootElement.id)?.Release();
            for (int i = 0; i < m_TextLayoutBoxes.size; i++) {
                if (m_TextLayoutBoxes[i].element.View == view) {
                    m_TextLayoutBoxes.RemoveAt(i--);
                }
            }
        }

        private void HandleStylePropertyChanged(UIElement element, StructList<StyleProperty> properties) {
            // todo early-out if we haven't had a layout pass for the element yet
            LayoutBox box = m_LayoutBoxMap.GetOrDefault(element.id);
            if (box == null) {
                return;
            }

            if (box.HandleStylePropertiesChanged(properties)) {
                HandleLayoutChanged(element);
//                box.parent?.OnChildStylePropertyChanged(box, properties);
            }
        }

        private void HandleLayoutChanged(UIElement element) {
            LayoutBox box;
            if (!m_LayoutBoxMap.TryGetValue(element.id, out box)) {
                CreateLayoutBox(element);
                return;
            }

            LayoutBox parent = box.parent;
            LayoutBox replace = CreateLayoutBox(element);

            replace.allocatedWidth = box.allocatedWidth;
            replace.allocatedHeight = box.allocatedHeight;

            replace.parent = parent;
            UpdateChildren(replace);
            UpdateChildren(parent);
            replace.CopyValues(box);
            box.Release();
        }

        public struct LayoutBoxPair {

            public UIElement element;
            public LayoutBox parentBox;

            public LayoutBoxPair(UIElement element, LayoutBox parentBox) {
                this.element = element;
                this.parentBox = parentBox;
            }

        }

        public void OnElementEnabled(UIElement element) {
            // none of these boxes should exist for the whole hierarchy, create them

            LightList<LayoutBox> toUpdateList = LightList<LayoutBox>.Get();
            LightStack<LayoutBoxPair> stack = LightStack<LayoutBoxPair>.Get();

            if (element.parent != null) {
                stack.Push(new LayoutBoxPair(element, m_LayoutBoxMap.GetOrDefault(element.parent.id)));
            }
            else {
                stack.Push(new LayoutBoxPair(element, null));
            }

            while (stack.Count > 0) {
                LayoutBoxPair current = stack.PopUnchecked();

                if (current.element.isDestroyed || current.element.isDisabled) {
                    continue;
                }

                LayoutBox box = CreateLayoutBox(current.element);
                box.parent = current.parentBox;
                box.UpdateViewSizeProperties();
                toUpdateList.Add(box);

                int childCount = current.element.children.Count;
                UIElement[] children = current.element.children.Array;

                for (int i = 0; i < childCount; i++) {
                    stack.Push(new LayoutBoxPair(children[i], box));
                }
            }

            int count = toUpdateList.Count;
            LayoutBox[] toUpdate = toUpdateList.Array;

            for (int i = 0; i < count; i++) {
                UpdateChildren(toUpdate[i]);
            }

            if (element.parent != null) {
                LayoutBox ptr = toUpdate[0].parent;
                while (ptr != null) {
                    if (ptr.layoutBehavior != LayoutBehavior.TranscludeChildren) {
                        UpdateChildren(ptr);
                        break;
                    }

                    ptr = ptr.parent;
                }
            }

            LightList<LayoutBox>.Release(ref toUpdateList);
            LightStack<LayoutBoxPair>.Release(ref stack);
        }

        public void OnElementDisabled(UIElement element) {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            stack.Push(element);
            LayoutBox currentBox = m_LayoutBoxMap.GetOrDefault(element.id);
            LayoutBox parentBox = null;
            if (currentBox != null) {
                parentBox = currentBox.parent;
            }

            while (stack.Count > 0) {
                UIElement current = stack.PopUnchecked();

                if (m_LayoutBoxMap.Remove(current.id, out LayoutBox box)) {
                    if (box is TextLayoutBox textLayoutBox) {
                        m_TextLayoutBoxes.Remove(textLayoutBox);
                    }

                    box.Release();
                }

                int childCount = current.children.Count;
                UIElement[] children = current.children.Array;
                for (int i = 0; i < childCount; i++) {
                    stack.Push(children[i]);
                }
            }

            LightStack<UIElement>.Release(ref stack);
            if (parentBox != null) {
                UpdateChildren(parentBox);
                parentBox.UpdateChildren();
            }
        }

        public void OnElementDestroyed(UIElement element) {
            if (m_LayoutBoxMap.Remove(element.id, out LayoutBox box)) {
                if (box.parent != null) {
                    UpdateChildren(box.parent);
                }

                m_VisibleBoxList.Remove(box);
                box.Release();
            }
        }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string previousValue) { }

        private LayoutBox CreateLayoutBox(UIElement element) {
            LayoutBox retn = null;
            if ((element is UITextElement)) {
                TextLayoutBox textLayout = (TextLayoutBox) layoutBoxPoolMap[TextLayoutPoolKey].Get(element);
                m_TextLayoutBoxes.Add(textLayout);
                retn = textLayout;
            }
            else if ((element is UIImageElement)) {
                retn = layoutBoxPoolMap[ImageLayoutPoolKey].Get(element);
            }
            else {
                switch (element.style.LayoutType) {
                    case LayoutType.Flex:
                        retn = layoutBoxPoolMap[(int) LayoutType.Flex].Get(element);
                        break;

                    case LayoutType.Flow:
                        retn = layoutBoxPoolMap[(int) LayoutType.Flow].Get(element);
                        break;

                    case LayoutType.Fixed:
                        retn = layoutBoxPoolMap[(int) LayoutType.Fixed].Get(element);
                        break;

                    case LayoutType.Grid:
                        retn = layoutBoxPoolMap[(int) LayoutType.Grid].Get(element);
                        break;

                    case LayoutType.Radial:
                        retn = layoutBoxPoolMap[(int) LayoutType.Radial].Get(element);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            retn.UpdateFromStyle();
            m_LayoutBoxMap[element.id] = retn;
            return retn;
        }

        public void OnElementCreated(UIElement element) { }

        private void GetChildBoxes(LayoutBox box, LightList<LayoutBox> list) {
            UIElement element = box.element;
            UIElement[] children = element.children.Array;
            int count = element.children.Count;

            for (int i = 0; i < count; i++) {
                LayoutBox childBox = m_LayoutBoxMap[children[i].id];
                if (childBox == null) {
                    continue;
                }

                LayoutBehavior behavior = childBox.layoutBehavior;
                switch (behavior) {
                    case LayoutBehavior.Unset:
                    case LayoutBehavior.Normal:
                        list.Add(childBox);
                        break;
                    case LayoutBehavior.Ignored:
                        break;
                    case LayoutBehavior.TranscludeChildren:
                        GetChildBoxes(childBox, list);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void UpdateChildren(LayoutBox box) {
            UIElement element = box.element;

            if (box.layoutBehavior == LayoutBehavior.TranscludeChildren) {
                return;
            }

            if (element.children == null || element.children.Count == 0) {
                return;
            }

            LightList<LayoutBox> boxes = LightList<LayoutBox>.Get();
            boxes.EnsureCapacity(element.children.Count);

            box.children.Clear();
            GetChildBoxes(box, boxes);
            box.children.AddRange(boxes);
            for (int i = 0; i < boxes.Count; i++) {
                boxes[i].parent = box;
            }

            LightList<LayoutBox>.Release(ref boxes);
            box.UpdateChildren();
        }

        public IList<UIElement> QueryPoint(Vector2 point, IList<UIElement> retn) {
            // todo convert to quad tree
            if (retn == null) {
                retn = ListPool<UIElement>.Get();
            }

            for (int i = application.m_Views.Count - 1; -1 < i; i--) {
                QueryPointInView(point, application.m_Views[i], retn);
            }

            return retn;
        }

        // todo -- remodel this to use a quad tree or at least screen buckets
        private static void QueryPointInView(Vector2 point, UIView view, IList<UIElement> retn) {
            UIElement[] elements = view.visibleElements.Array;
            int elementCount = view.visibleElements.Count;
            for (int i = 0; i < elementCount; i++) {
                UIElement element = elements[i];

                // todo make this better
                if (element.isDisabled) {
                    continue;
                }

                if (element is IPointerQueryHandler handler) {
                    if (!handler.ContainsPoint(point)) {
                        continue;
                    }
                }
                else if (!element.layoutResult.ScreenRect.ContainOrOverlap(point) || PointInClippedArea(point, element)) {
                    continue;
                }

                UIElement ptr = element.parent;
                while (ptr != null && !PointInClippedArea(point, ptr)) {
                    ptr = ptr.parent;
                }

                // i.e. clipped by parent
                if (ptr != null) {
                    continue;
                }

                retn.Add(element);
            }
        }

        // todo -- use layout box to access cached values instead of style look up
        private static bool PointInClippedArea(Vector2 point, UIElement element) {
            Vector2 screenPosition = element.layoutResult.screenPosition;
            if (element.style.OverflowX != Overflow.Visible) {
                if (point.x < screenPosition.x || point.x > screenPosition.x + element.layoutResult.allocatedSize.width) {
                    return true;
                }
            }

            if (element.style.OverflowY != Overflow.Visible) {
                if (point.y < screenPosition.y || point.y > screenPosition.y + element.layoutResult.allocatedSize.height) {
                    return true;
                }
            }

            return false;
        }

        // todo -- remove this, only used for inspector
        public OffsetRect GetPaddingRect(UIElement element) {
            LayoutBox box = m_LayoutBoxMap.GetOrDefault(element.id);
            if (box == null)
                return new OffsetRect();
            return new OffsetRect(box.resolvedPaddingTop, box.resolvedPaddingRight, box.resolvedPaddingBottom, box.resolvedPaddingLeft);
        }

        // todo -- remove this, only used for inspector
        public OffsetRect GetMarginRect(UIElement element) {
            LayoutBox box = m_LayoutBoxMap.GetOrDefault(element.id);
            if (box == null)
                return new OffsetRect();
            return new OffsetRect(box.GetMarginTop(box.actualWidth), box.GetMarginRight(), box.GetMarginBottom(box.actualWidth), box.GetMarginLeft());
        }

        // todo -- remove this, only used for inspector
        public OffsetRect GetBorderRect(UIElement element) {
            LayoutBox box = m_LayoutBoxMap.GetOrDefault(element.id);
            if (box == null)
                return new OffsetRect();
            return new OffsetRect(box.resolvedBorderTop, box.resolvedBorderRight, box.resolvedBorderBottom, box.resolvedBorderLeft);
        }

        public LayoutBox GetBoxForElement(UIElement itemElement) {
            return m_LayoutBoxMap.GetOrDefault(itemElement.id);
        }

        public LightList<UIElement> GetVisibleElements(LightList<UIElement> retn = null) {
            if (retn == null) {
                retn = new LightList<UIElement>(m_VisibleBoxList.Count);
            }
            else {
                retn.EnsureCapacity(m_VisibleBoxList.Count);
            }

            UIElement[] elements = retn.Array;
            LayoutBox[] boxes = m_VisibleBoxList.Array;
            for (int i = 0; i < m_VisibleBoxList.Count; i++) {
                elements[i] = boxes[i].element;
            }

            retn.Count = m_VisibleBoxList.Count;
            return retn;
        }

    }

}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UIForia.Elements;
using UIForia.Rendering;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Layout.LayoutTypes {

    public abstract class LayoutBox {

        public float localX;
        public float localY;

        public float allocatedWidth;
        public float allocatedHeight;

        public float actualWidth;
        public float actualHeight;

        public float xMax;
        public float yMax;

        public UIElement element;
        public UIStyleSet style;

        public LayoutBox parent;
        public List<LayoutBox> children;

        protected internal UIView view;

        public float resolvedPaddingTop;
        public float resolvedPaddingRight;
        public float resolvedPaddingBottom;
        public float resolvedPaddingLeft;

        public float resolvedBorderTop;
        public float resolvedBorderRight;
        public float resolvedBorderBottom;
        public float resolvedBorderLeft;

        public float resolvedBorderRadiusTopLeft;
        public float resolvedBorderRadiusTopRight;
        public float resolvedBorderRadiusBottomRight;
        public float resolvedBorderRadiusBottomLeft;

        internal UIFixedLength paddingTop;
        internal UIFixedLength paddingRight;
        internal UIFixedLength paddingBottom;
        internal UIFixedLength paddingLeft;

        internal UIFixedLength borderTop;
        internal UIFixedLength borderRight;
        internal UIFixedLength borderBottom;
        internal UIFixedLength borderLeft;

        internal UIFixedLength borderRadiusTopLeft;
        internal UIFixedLength borderRadiusTopRight;
        internal UIFixedLength borderRadiusBottomLeft;
        internal UIFixedLength borderRadiusBottomRight;

        internal UIMeasurement marginTop;
        internal UIMeasurement marginRight;
        internal UIMeasurement marginBottom;
        internal UIMeasurement marginLeft;

        internal float transformRotation;
        internal float transformScaleX;
        internal float transformScaleY;

        internal int zIndex;
        internal int layer;

        internal UIMeasurement prefWidth;
        internal UIMeasurement minWidth;
        internal UIMeasurement maxWidth;

        internal UIMeasurement prefHeight;
        internal UIMeasurement minHeight;
        internal UIMeasurement maxHeight;

        internal UIFixedLength transformPivotX;
        internal UIFixedLength transformPivotY;
        internal TransformOffset transformPositionX;
        internal TransformOffset transformPositionY;
        internal TransformBehavior transformBehaviorX;
        internal TransformBehavior transformBehaviorY;

        internal Overflow overflowX;
        internal Overflow overflowY;

        internal LayoutBehavior layoutBehavior;
        internal bool isInPool;
        internal LayoutBoxPool pool;

        internal int traversalIndex;
        internal int viewDepthIdx;

        internal Vector2 pivot;
        
        public bool markedForLayout;
        protected internal float cachedPreferredWidth;

        private static readonly Dictionary<int, WidthCache> s_HeightForWidthCache = new Dictionary<int, WidthCache>();

        public Rect clipRect;

        public abstract void RunLayout();

        public float PaddingHorizontal => resolvedPaddingRight + resolvedPaddingLeft;
        public float BorderHorizontal => resolvedBorderLeft + resolvedBorderRight;

        public float PaddingVertical => resolvedPaddingTop + resolvedPaddingBottom;
        public float BorderVertical => resolvedBorderTop + resolvedBorderBottom;

        public bool IsIgnored => (layoutBehavior & LayoutBehavior.Ignored) != 0;

        public float PaddingBorderHorizontal =>
            resolvedPaddingLeft +
            resolvedPaddingRight +
            resolvedBorderRight +
            resolvedBorderLeft;

        public float PaddingBorderVertical =>
            resolvedPaddingTop +
            resolvedPaddingBottom +
            resolvedBorderBottom +
            resolvedBorderTop;

        public Rect ContentRect {
            get {
                float x = resolvedPaddingLeft + resolvedBorderLeft;
                float y = resolvedPaddingTop + resolvedBorderTop;
                float width = allocatedWidth - PaddingBorderHorizontal;
                float height = allocatedHeight - PaddingBorderVertical;
                return new Rect(x, y, Mathf.Max(0, width), Mathf.Max(0, height));
            }
        }

//        public Vector2 Pivot => new Vector2(
//            ResolveFixedWidth(transformPivotX),
//            ResolveFixedHeight(transformPivotY)
//        );

        public float GetMarginTop(float width) {
            return ResolveMarginVertical(width, marginTop);
        }

        public float GetMarginBottom(float width) {
            return ResolveMarginVertical(width, marginBottom);
        }

        public float GetMarginLeft() {
            return ResolveMarginHorizontal(marginLeft);
        }

        public float GetMarginRight() {
            return ResolveMarginHorizontal(marginRight);
        }

        // need layout when
        /*
         * - Child Add / Remove / Move / Enable / Disable
         * - Allocated size changes && we give a shit -> ie any child is parent dependent
         * - Parent Allocated size changes & we give a shit -> ie we are parent dependent, handled automatically
         * - Child size changes from style
         * - Child layout behavior changes
         * - Child transform properties change & we care
         * - Child constraint changes && affects output size or position
         * - Layout property changes
         */

        internal void UpdateViewSizeProperties() {
            resolvedPaddingTop = ResolveFixedHeight(paddingTop);
            resolvedPaddingRight = ResolveFixedWidth(paddingRight);
            resolvedPaddingBottom = ResolveFixedHeight(paddingBottom);
            resolvedPaddingLeft = ResolveFixedWidth(paddingLeft);

            resolvedBorderTop =  ResolveFixedHeight(borderTop);
            resolvedBorderRight = ResolveFixedWidth(borderRight);
            resolvedBorderBottom = ResolveFixedHeight(borderBottom);
            resolvedBorderLeft = ResolveFixedWidth(borderLeft);

            resolvedBorderRadiusTopLeft = ResolveFixedWidth(borderRadiusTopLeft);
            resolvedBorderRadiusTopRight =  ResolveFixedWidth(borderRadiusTopRight);
            resolvedBorderRadiusBottomRight = ResolveFixedWidth(borderRadiusBottomRight);
            resolvedBorderRadiusBottomLeft = ResolveFixedWidth(borderRadiusBottomLeft);
        }

        public void UpdateFromStyle() {
            paddingLeft = style.PaddingLeft;
            paddingTop = style.PaddingTop;
            paddingBottom = style.PaddingBottom;
            paddingRight = style.PaddingRight;
            borderLeft = style.BorderLeft;
            borderTop = style.BorderTop;
            borderBottom = style.BorderBottom;
            borderRight = style.BorderRight;
            marginLeft = style.MarginLeft;
            marginTop = style.MarginTop;
            marginBottom = style.MarginBottom;
            marginRight = style.MarginRight;
            borderRadiusTopLeft = style.BorderRadiusTopLeft;
            borderRadiusTopRight = style.BorderRadiusTopRight;
            borderRadiusBottomLeft = style.BorderRadiusBottomLeft;
            borderRadiusBottomRight = style.BorderRadiusBottomRight;
            transformPositionX = style.TransformPositionX;
            transformPositionY = style.TransformPositionY;
            transformBehaviorX = style.TransformBehaviorX;
            transformBehaviorY = style.TransformBehaviorY;
            transformPivotX = style.TransformPivotX;
            transformPivotY = style.TransformPivotY;
            transformScaleX = style.TransformScaleX;
            transformScaleY = style.TransformScaleY;
            transformRotation = style.TransformRotation;
            prefWidth = style.PreferredWidth;
            minWidth = style.MinWidth;
            maxWidth = style.MaxWidth;
            prefHeight = style.PreferredHeight;
            minHeight = style.MinHeight;
            maxHeight = style.MaxHeight;
            zIndex = style.ZIndex;
            layer = style.Layer;
            layoutBehavior = style.LayoutBehavior;
            overflowX = style.OverflowX;
            overflowY = style.OverflowY;
        }


        internal void CopyValues(LayoutBox other) {
            paddingLeft = other.paddingLeft;
            paddingTop = other.paddingTop;
            paddingBottom = other.paddingBottom;
            paddingRight = other.paddingRight;
            borderLeft = other.borderLeft;
            borderTop = other.borderTop;
            borderBottom = other.borderBottom;
            borderRight = other.borderRight;
            marginLeft = other.marginLeft;
            marginTop = other.marginTop;
            marginBottom = other.marginBottom;
            marginRight = other.marginRight;
            borderRadiusTopLeft = other.borderRadiusTopLeft;
            borderRadiusTopRight = other.borderRadiusTopRight;
            borderRadiusBottomLeft = other.borderRadiusBottomLeft;
            borderRadiusBottomRight = other.borderRadiusBottomRight;
            transformPositionX = other.transformPositionX;
            transformPositionY = other.transformPositionY;
            transformBehaviorX = other.transformBehaviorX;
            transformBehaviorY = other.transformBehaviorY;
            transformPivotX = other.transformPivotX;
            transformPivotY = other.transformPivotY;
            transformScaleX = other.transformScaleX;
            transformScaleY = other.transformScaleY;
            transformRotation = other.transformRotation;
            prefWidth = other.prefWidth;
            minWidth = other.minWidth;
            maxWidth = other.maxWidth;
            prefHeight = other.prefHeight;
            minHeight = other.minHeight;
            maxHeight = other.maxHeight;
            zIndex = other.zIndex;
            layer = other.layer;
            layoutBehavior = other.layoutBehavior;
            overflowX = other.overflowX;
            overflowY = other.overflowY;

            resolvedPaddingTop = other.resolvedPaddingTop;
            resolvedPaddingRight = other.resolvedPaddingRight;
            resolvedPaddingBottom = other.resolvedPaddingBottom;
            resolvedPaddingLeft = other.resolvedPaddingLeft;

            resolvedBorderTop = other.resolvedBorderTop;
            resolvedBorderRight = other.resolvedBorderRight;
            resolvedBorderBottom = other.resolvedBorderBottom;
            resolvedBorderLeft = other.resolvedBorderLeft;

            resolvedBorderRadiusTopLeft = other.resolvedBorderRadiusTopLeft;
            resolvedBorderRadiusTopRight = other.resolvedBorderRadiusTopRight;
            resolvedBorderRadiusBottomRight = other.resolvedBorderRadiusBottomRight;
            resolvedBorderRadiusBottomLeft = other.resolvedBorderRadiusBottomLeft;
        }

        public void UpdateChildren() {
            InvalidatePreferredSizeCache();
            RequestContentSizeChangeLayout();
            OnChildrenChanged();
        }

        public void RequestContentSizeChangeLayout() {
            markedForLayout = true;
            InvalidatePreferredSizeCache();
            LayoutBox ptr = parent;
            while (ptr != null) {
                // not 100% sure this is safe
                if (ptr.markedForLayout) {
                    return;
                }

                ptr.markedForLayout = true;
                ptr.InvalidatePreferredSizeCache();
                ptr = ptr.parent;
            }
        }

        public void SetAllocatedRect(float x, float y, float width, float height) {
            SetAllocatedXAndWidth(x, width);
            SetAllocatedYAndHeight(y, height);
        }

        public void SetAllocatedXAndWidth(float x, float width) {
            localX = x;
            if ((int) allocatedWidth != (int) width) {
                allocatedWidth = width;
                markedForLayout = true; // todo might not need it, delegate to virtual fn 

                resolvedBorderLeft = ResolveFixedWidth(borderLeft);
                resolvedBorderRight = ResolveFixedWidth(borderRight);

                resolvedPaddingLeft = ResolveFixedWidth(paddingLeft);
                resolvedPaddingRight = ResolveFixedWidth(paddingRight);

                resolvedBorderRadiusTopLeft = ResolveFixedWidth(borderRadiusTopLeft);
                resolvedBorderRadiusTopRight = ResolveFixedWidth(borderRadiusTopRight);
                resolvedBorderRadiusBottomRight = ResolveFixedWidth(borderRadiusBottomRight);
                resolvedBorderRadiusBottomLeft = ResolveFixedWidth(borderRadiusBottomLeft);
                pivot.x = ResolveFixedWidth(transformPivotX);
            }
        }

        public void SetAllocatedYAndHeight(float y, float height) {
            localY = y;
            if ((int) allocatedHeight != (int) height) {
                allocatedHeight = height;
                markedForLayout = true; // todo might not need it, delegate to virtual fn 
                
                resolvedBorderTop = ResolveFixedHeight(borderTop);
                resolvedBorderBottom = ResolveFixedHeight(borderBottom);

                resolvedPaddingTop = ResolveFixedHeight(paddingTop);
                resolvedPaddingBottom = ResolveFixedHeight(paddingBottom);
                pivot.y = ResolveFixedWidth(transformPivotY);

            }
        }

        [DebuggerStepThrough]
        protected float ResolveFixedWidth(UIFixedLength width) {
            switch (width.unit) {
                case UIFixedUnit.Pixel:
                    return width.value * view.ScaleFactor;

                case UIFixedUnit.Percent:
                    return allocatedWidth * width.value;

                case UIFixedUnit.ViewportHeight:
                    return view.Viewport.height * width.value;

                case UIFixedUnit.ViewportWidth:
                    return view.Viewport.width * width.value;

                case UIFixedUnit.Em:
                    return style.GetResolvedFontSize() * width.value;

                case UIFixedUnit.LineHeight:
                    return style.LineHeightSize * width.value;

                default:
                    return 0;
            }
        }

        [DebuggerStepThrough]
        protected float ResolveFixedHeight(UIFixedLength height) {
            switch (height.unit) {
                case UIFixedUnit.Pixel:
                    return height.value * view.ScaleFactor;

                case UIFixedUnit.Percent:
                    return allocatedHeight * height.value;

                case UIFixedUnit.ViewportHeight:
                    return view.Viewport.height * height.value;

                case UIFixedUnit.ViewportWidth:
                    return view.Viewport.width * height.value;

                case UIFixedUnit.Em:
                    return style.GetResolvedFontSize() * height.value;

                case UIFixedUnit.LineHeight:
                    return style.LineHeightSize * height.value;

                default:
                    return 0;
            }
        }

        protected float ResolveMarginVertical(float width, UIMeasurement margin) {
            AnchorTarget anchorTarget;
            switch (margin.unit) {
                case UIMeasurementUnit.Pixel:
                    return margin.value * view.ScaleFactor;

                case UIMeasurementUnit.Content:
                    return GetContentHeight(width) * margin.value;

                case UIMeasurementUnit.ParentSize:
                    if (parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return parent.allocatedHeight * margin.value;

                case UIMeasurementUnit.ViewportWidth:
                    return view.Viewport.width * margin.value;

                case UIMeasurementUnit.ViewportHeight:
                    return view.Viewport.height * margin.value;

                case UIMeasurementUnit.ParentContentArea:
                    if (parent.prefHeight.IsContentBased) {
                        return 0f;
                    }

                    return parent.allocatedHeight * margin.value -
                           (parent.style == null ? 0 : parent.PaddingBorderVertical);

                case UIMeasurementUnit.Em:
                    return style.GetResolvedFontSize() * margin.value;

                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(margin.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(margin.value);

                case UIMeasurementUnit.Unset:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        public virtual void OnStylePropertyChanged(StructList<StyleProperty> property) { }

        public virtual void OnChildStylePropertyChanged(LayoutBox child, StructList<StyleProperty> property) { }

        protected static int FindLayoutSiblingIndex(UIElement element) {
            // if parent is not in layout
            // we want to replace it
            // so find parent's sibling index
            // spin through laid out children until finding target
            // use parent index + child index
            if (element.parent == null) return 0;

            int idx = 0;
            for (int i = 0; i < element.parent.children.Count; i++) {
                UIElement sibling = element.parent.children[i];
                if (sibling == element) {
                    break;
                }

                if ((sibling.style.LayoutBehavior & LayoutBehavior.Ignored) == 0) {
                    idx++;
                }
            }

            return idx;
        }

        public void InvalidatePreferredSizeCache() {
            cachedPreferredWidth = -1;
            if (element != null) {
                s_HeightForWidthCache.Remove(element.id);
            }
        }

        protected void SetCachedHeightForWidth(float width, float height) {
            WidthCache retn;
            int intWidth = (int) width;
            if (s_HeightForWidthCache.TryGetValue(element.id, out retn)) {
                if (retn.next == 0) {
                    retn.next = 1;
                    retn.width0 = intWidth;
                    retn.height0 = height;
                }
                else if (retn.next == 1) {
                    retn.next = 2;
                    retn.width1 = intWidth;
                    retn.height1 = height;
                }
                else {
                    retn.next = 0;
                    retn.width2 = intWidth;
                    retn.height2 = height;
                }
            }
            else {
                retn.next = 1;
                retn.width0 = intWidth;
                retn.height0 = height;
            }

            s_HeightForWidthCache[element.id] = retn;
        }

        protected float GetCachedHeightForWidth(float width) {
            WidthCache retn;
            int intWidth = (int) width;
            if (s_HeightForWidthCache.TryGetValue(element.id, out retn)) {
                if (retn.width0 == intWidth) {
                    return retn.height0;
                }

                if (retn.width1 == intWidth) {
                    return retn.height1;
                }

                if (retn.width2 == intWidth) {
                    return retn.height2;
                }

                return -1;
            }

            return -1;
        }

        protected abstract float ComputeContentWidth();

        protected abstract float ComputeContentHeight(float width);

        private float GetContentWidth() {
            if (cachedPreferredWidth == -1) {
                cachedPreferredWidth = ComputeContentWidth();
            }

            return cachedPreferredWidth;
        }

        private float GetContentHeight(float width) {
            // todo -- get some stats on this
            float cachedHeight = GetCachedHeightForWidth(width);
            if (cachedHeight == -1) {
                cachedHeight = ComputeContentHeight(width);
                SetCachedHeightForWidth(width, cachedHeight);
            }

            return cachedHeight;
        }

        public float GetPreferredWidth() {
            AnchorTarget anchorTarget;
            UIMeasurement widthMeasurement = prefWidth;
            switch (widthMeasurement.unit) {
                case UIMeasurementUnit.Pixel:
                    return view.ScaleFactor * Mathf.Max(0, widthMeasurement.value);

                case UIMeasurementUnit.Percentage:
                    return 0;
                
                case UIMeasurementUnit.Content:
                    return Mathf.Max(0, PaddingBorderHorizontal + (GetContentWidth() * widthMeasurement.value));

                case UIMeasurementUnit.ParentSize:
                    if (parent == null) return view.Viewport.width;
                    if (!IsIgnored && parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0, parent.allocatedWidth * widthMeasurement.value);

                case UIMeasurementUnit.ViewportWidth:
                    return Mathf.Max(0, view.Viewport.width * widthMeasurement.value);

                case UIMeasurementUnit.ViewportHeight:
                    return Mathf.Max(0, view.Viewport.height * widthMeasurement.value);

                case UIMeasurementUnit.ParentContentArea:
                    if (parent == null) return view.Viewport.width;
                    if (!IsIgnored && parent.prefWidth.IsContentBased) {
                        // todo there are cases where this is not true
                        // if we hit the paradox -> size = own content size
                        // ie parent is layout that can grow and parent is growing
                        return 0f;
                    }

                    return Mathf.Max(0, parent.allocatedWidth - parent.PaddingBorderHorizontal) * widthMeasurement.value;

                case UIMeasurementUnit.Em:
                    return Math.Max(0, style.GetResolvedFontSize() * widthMeasurement.value);


                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent == null) return view.Viewport.width;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(widthMeasurement.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent == null) return view.Viewport.height;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(widthMeasurement.value);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected float ResolveAnchorWidth(float widthMeasurement) {
            return Mathf.Max(ResolveAnchorRight() - ResolveAnchorLeft(), 0) * widthMeasurement;
        }

        protected float ResolveAnchorHeight(float heightMeasurement) {
            return Mathf.Max(ResolveAnchorBottom() - ResolveAnchorTop(), 0) * heightMeasurement;
        }

        protected float ResolveAnchorValue(float width, UIFixedLength anchor) {
            switch (anchor.unit) {
                case UIFixedUnit.Unset:
                    return 0;

                case UIFixedUnit.Pixel:
                    return anchor.value;

                case UIFixedUnit.Percent:
                    return width * anchor.value;

                case UIFixedUnit.Em:
                    return style.GetResolvedFontSize() * anchor.value;

                case UIFixedUnit.LineHeight:
                    return 0;

                case UIFixedUnit.ViewportWidth:
                    return view.Viewport.width * anchor.value;

                case UIFixedUnit.ViewportHeight:
                    return view.Viewport.height * anchor.value;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ResolveAnchorRight() {
            UIFixedLength anchor = style.AnchorRight;
            switch (style.AnchorTarget) {
                case AnchorTarget.Unset:
                case AnchorTarget.Parent:
                    if (parent == null) {
                        return view.Viewport.xMax - ResolveAnchorValue(view.Viewport.width, anchor);
                    }

                    // needs to be allocated width not actualWidth because we might not know the actual width yet
                    return parent.actualWidth - ResolveAnchorValue(parent.actualWidth, anchor);

                case AnchorTarget.ParentContentArea:
                    if (parent == null) {
                        return view.Viewport.x + ResolveAnchorValue(view.Viewport.width, anchor);
                    }

                    LayoutResult parentResult = parent.element.layoutResult;
                    float offset = parentResult.padding.right + parentResult.border.right + parentResult.border.left + parentResult.padding.left;
                    return parent.actualWidth - (ResolveAnchorValue(parent.actualWidth - offset, anchor) + parentResult.padding.right + parentResult.border.right);

                case AnchorTarget.Viewport:
                    return view.Viewport.x +
                           view.Viewport.width - ResolveAnchorValue(view.Viewport.width, anchor);

                case AnchorTarget.Screen:
                    float parentX = parent?.element.layoutResult.screenPosition.x ?? 0;
                    return Screen.width - parentX - ResolveAnchorValue(Screen.width, anchor);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ResolveAnchorLeft() {
            UIFixedLength anchor = style.AnchorLeft;
            switch (style.AnchorTarget) {
                case AnchorTarget.Unset:
                case AnchorTarget.Parent:
                    if (parent == null) {
                        return view.Viewport.xMin + ResolveAnchorValue(view.Viewport.width, anchor);
                    }

                    return ResolveAnchorValue(parent.actualWidth, anchor);

                case AnchorTarget.ParentContentArea:
                    if (parent == null) {
                        return view.Viewport.xMax + ResolveAnchorValue(view.Viewport.width, anchor);
                    }

                    LayoutResult parentResult = parent.element.layoutResult;
                    float offset = parentResult.padding.left + parentResult.border.left + parentResult.padding.right + parentResult.border.right;
                    return ResolveAnchorValue(parent.actualWidth - offset, anchor) + parentResult.padding.left + parentResult.border.left;

                case AnchorTarget.Viewport:
                    return view.Viewport.xMax + ResolveAnchorValue(view.Viewport.width, anchor);

                case AnchorTarget.Screen:
                    float parentX = parent?.element.layoutResult.screenPosition.x ?? 0;
                    return ResolveAnchorValue(Screen.width, anchor) - parentX;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ResolveAnchorTop() {
            UIFixedLength anchor = style.AnchorTop;
            switch (style.AnchorTarget) {
                case AnchorTarget.Unset:
                case AnchorTarget.Parent:
                    if (parent == null) {
                        return view.Viewport.y + ResolveAnchorValue(view.Viewport.height, anchor);
                    }

                    return ResolveAnchorValue(parent.actualHeight, anchor);

                case AnchorTarget.ParentContentArea:
                    if (parent == null) {
                        return view.Viewport.y + ResolveAnchorValue(view.Viewport.height, anchor);
                    }

                    LayoutResult parentResult = parent.element.layoutResult;
                    float offset = parentResult.padding.top + parentResult.border.top + parentResult.border.bottom + parentResult.padding.bottom;
                    return ResolveAnchorValue(parent.actualHeight - offset, anchor) + parentResult.padding.top + parentResult.border.top;

                case AnchorTarget.Viewport:
                    return view.Viewport.y + ResolveAnchorValue(view.Viewport.height, anchor);

                case AnchorTarget.Screen:
                    float parentY = parent?.element.layoutResult.screenPosition.y ?? 0;
                    return ResolveAnchorValue(Screen.height, anchor) - parentY;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ResolveAnchorBottom() {
            UIFixedLength anchor = style.AnchorBottom;
            switch (style.AnchorTarget) {
                case AnchorTarget.Unset:
                case AnchorTarget.Parent:
                    if (parent == null) {
                        return view.Viewport.yMax + ResolveAnchorValue(view.Viewport.height, anchor);
                    }

                    return parent.actualHeight - ResolveAnchorValue(parent.actualHeight, anchor);

                case AnchorTarget.ParentContentArea:
                    if (parent == null) {
                        return view.Viewport.yMax + ResolveAnchorValue(view.Viewport.height, anchor);
                    }

                    LayoutResult parentResult = parent.element.layoutResult;
                    float offset = parentResult.padding.top + parentResult.border.top + parentResult.border.bottom + parentResult.padding.bottom;
                    return parent.actualHeight - (ResolveAnchorValue(parent.actualHeight - offset, anchor) + parentResult.border.bottom + parentResult.padding.bottom);

                case AnchorTarget.Viewport:
                    return view.Viewport.yMax + ResolveAnchorValue(view.Viewport.height, anchor);

                case AnchorTarget.Screen:
                    float parentY = parent?.element.layoutResult.screenPosition.y ?? 0;
                    return Screen.height - parentY - ResolveAnchorValue(Screen.height, anchor);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float GetPreferredHeight(float contentWidth) {
            AnchorTarget anchorTarget;
            UIMeasurement height = prefHeight;
            switch (height.unit) {
                case UIMeasurementUnit.Pixel:
                    return Mathf.Max(0, height.value * view.ScaleFactor);

                case UIMeasurementUnit.Content:
                    float contentHeight = GetCachedHeightForWidth(contentWidth);
                    if (contentHeight == -1) {
                        float cachedWidth = allocatedWidth;
                        contentHeight = ComputeContentHeight(contentWidth);
                        SetCachedHeightForWidth(contentWidth, contentHeight);
                        allocatedWidth = cachedWidth;
                    }

                    return Mathf.Max(0, PaddingBorderVertical + (contentHeight * height.value));

                case UIMeasurementUnit.ParentSize:
                    if (parent == null) return view.Viewport.height;
                    if (!IsIgnored && parent.prefHeight.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0, parent.allocatedHeight * height.value);

                case UIMeasurementUnit.ViewportWidth:
                    return Mathf.Max(0, view.Viewport.width * height.value);

                case UIMeasurementUnit.ViewportHeight:
                    return Mathf.Max(0, view.Viewport.height * height.value);

                case UIMeasurementUnit.ParentContentArea:
                    if (parent == null) return view.Viewport.height;
                    if (!IsIgnored && parent.prefHeight.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0,
                        parent.allocatedHeight * height.value -
                        (parent.style == null ? 0 : parent.PaddingBorderVertical));

                case UIMeasurementUnit.Em:
                    return Mathf.Max(0, style.GetResolvedFontSize() * height.value);

                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent == null) return view.Viewport.width;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(height.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent == null) return view.Viewport.height;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(height.value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public float ResolveMarginHorizontal(UIMeasurement margin) {
            AnchorTarget anchorTarget;

            switch (margin.unit) {
                case UIMeasurementUnit.Pixel:
                    return margin.value * view.ScaleFactor;

                case UIMeasurementUnit.Em:
                    return style.GetResolvedFontSize() * margin.value;

                case UIMeasurementUnit.Content:
                    return GetContentWidth() * margin.value;

                case UIMeasurementUnit.ParentSize:
                    if (parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return parent.allocatedHeight * margin.value;

                case UIMeasurementUnit.ViewportWidth:
                    return view.Viewport.width * margin.value;

                case UIMeasurementUnit.ViewportHeight:
                    return view.Viewport.height * margin.value;

                case UIMeasurementUnit.ParentContentArea:
                    if (parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return parent.allocatedWidth * margin.value -
                           (parent.style == null ? 0 : parent.PaddingBorderHorizontal);


                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(margin.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(margin.value);

                case UIMeasurementUnit.Unset:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Note: Only call this AFTER layout has run
        public float ResolveTransform(TransformOffset transformOffset) {
            switch (transformOffset.unit) {
                case TransformUnit.Unset:
                    return 0;

                case TransformUnit.Pixel:
                    return transformOffset.value;

                case TransformUnit.Em:
                    return style.GetResolvedFontSize() * transformOffset.value;

                case TransformUnit.ActualWidth:
                    return transformOffset.value * actualWidth;

                case TransformUnit.ActualHeight:
                    return transformOffset.value * actualHeight;

                case TransformUnit.AllocatedWidth:
                    return transformOffset.value * allocatedWidth;

                case TransformUnit.AllocatedHeight:
                    return transformOffset.value * allocatedHeight;

                case TransformUnit.ContentWidth:
                    return GetContentWidth() * transformOffset.value;

                case TransformUnit.ContentHeight:
                    // should this be allocatedWidth instead?
                    return GetContentHeight(actualWidth) * transformOffset.value;

                case TransformUnit.ContentAreaWidth:
                    float width = allocatedWidth - resolvedPaddingLeft - resolvedBorderLeft - resolvedPaddingRight - resolvedBorderRight;
                    return Mathf.Max(0, width) * transformOffset.value;

                case TransformUnit.ContentAreaHeight:
                    float height = allocatedHeight - resolvedPaddingTop - resolvedBorderTop - resolvedPaddingBottom - resolvedBorderBottom;
                    return Mathf.Max(0, height) * transformOffset.value;

                case TransformUnit.ViewportWidth:
                    return view.Viewport.width * transformOffset.value;

                case TransformUnit.ViewportHeight:
                    return view.Viewport.height * transformOffset.value;

                case TransformUnit.AnchorWidth: {
                    AnchorTarget anchorTarget = style.AnchorTarget;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(transformOffset.value);
                }

                case TransformUnit.AnchorHeight: {
                    AnchorTarget anchorTarget = style.AnchorTarget;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(transformOffset.value);
                }

                case TransformUnit.ParentWidth:
                    if (parent == null) return 0;
                    return parent.actualWidth * transformOffset.value;

                case TransformUnit.ParentHeight:
                    if (parent == null) return 0;
                    return parent.actualHeight * transformOffset.value;

                case TransformUnit.ParentContentAreaWidth:
                    if (parent == null) return 0;
                    return parent.ContentRect.width * transformOffset.value;

                case TransformUnit.ParentContentAreaHeight:
                    if (parent == null) return 0;
                    return parent.ContentRect.height * transformOffset.value;

                case TransformUnit.ScreenWidth:
                    return Screen.width * transformOffset.value;

                case TransformUnit.ScreenHeight:
                    return Screen.height * transformOffset.value;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        //   [DebuggerStepThrough]
        protected float ResolveMinOrMaxWidth(UIMeasurement widthMeasurement) {
            AnchorTarget anchorTarget;
            switch (widthMeasurement.unit) {
                case UIMeasurementUnit.Pixel:
                    return Mathf.Max(0, widthMeasurement.value);

                case UIMeasurementUnit.Content:
                    return Mathf.Max(0, PaddingBorderHorizontal + (GetContentWidth() * widthMeasurement.value));

                case UIMeasurementUnit.ParentSize:
                    if (parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0, parent.allocatedWidth * widthMeasurement.value);

                case UIMeasurementUnit.ViewportWidth:
                    return Mathf.Max(0, view.Viewport.width * widthMeasurement.value);

                case UIMeasurementUnit.ViewportHeight:
                    return Mathf.Max(0, view.Viewport.height * widthMeasurement.value);

                case UIMeasurementUnit.ParentContentArea:
                    if (!IsIgnored && parent.prefWidth.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0,
                        parent.allocatedWidth * widthMeasurement.value - (parent.style == null
                            ? 0
                            : (parent.resolvedPaddingLeft + parent.resolvedPaddingRight) - (parent.resolvedBorderLeft - parent.resolvedBorderRight)
                        )
                    );

                case UIMeasurementUnit.Em:
                    return Math.Max(0, style.GetResolvedFontSize() * widthMeasurement.value);

                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(widthMeasurement.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefWidth.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(widthMeasurement.value);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [DebuggerStepThrough]
        protected float ResolveMinOrMaxHeight(UIMeasurement heightMeasurement, float width) {
            AnchorTarget anchorTarget;
            switch (heightMeasurement.unit) {
                case UIMeasurementUnit.Pixel:
                    return Mathf.Max(0, heightMeasurement.value) * view.ScaleFactor;

                case UIMeasurementUnit.Content:
                    return Mathf.Max(0, PaddingBorderVertical + (GetContentHeight(width) * heightMeasurement.value));

                case UIMeasurementUnit.ParentSize:
                    if (parent.prefHeight.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0, parent.allocatedHeight * heightMeasurement.value);

                case UIMeasurementUnit.ViewportWidth:
                    return Mathf.Max(0, view.Viewport.width * heightMeasurement.value);

                case UIMeasurementUnit.ViewportHeight:
                    return Mathf.Max(0, view.Viewport.height * heightMeasurement.value);

                case UIMeasurementUnit.ParentContentArea:
                    if (!IsIgnored && parent.prefHeight.IsContentBased) {
                        return 0f;
                    }

                    return Mathf.Max(0,
                        parent.allocatedHeight * heightMeasurement.value - (parent.style == null
                            ? 0
                            : (parent.resolvedPaddingTop + parent.resolvedPaddingBottom) - (parent.resolvedBorderTop + parent.resolvedBorderBottom)
                        )
                    );

                case UIMeasurementUnit.Em:
                    return Mathf.Max(0, style.GetResolvedFontSize() * heightMeasurement.value);

                case UIMeasurementUnit.AnchorWidth:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorWidth(heightMeasurement.value);

                case UIMeasurementUnit.AnchorHeight:
                    anchorTarget = style.AnchorTarget;
                    if (parent.prefHeight.IsContentBased && anchorTarget == AnchorTarget.Parent ||
                        anchorTarget == AnchorTarget.ParentContentArea) {
                        return 0f;
                    }

                    return ResolveAnchorHeight(heightMeasurement.value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public LayoutBoxSize GetHeights(float width) {
            float prf = GetPreferredHeight(width);
            float min = ResolveMinOrMaxHeight(minHeight, width);
            float max = ResolveMinOrMaxHeight(maxHeight, width);
            return new LayoutBoxSize(min, max, prf);
        }

        public LayoutBoxSize GetWidths() {
            float prf = GetPreferredWidth();
            float min = ResolveMinOrMaxWidth(minWidth);
            float max = ResolveMinOrMaxWidth(maxWidth);

            return new LayoutBoxSize(min, max, prf);
        }

        internal bool HandleStylePropertiesChanged(StructList<StyleProperty> properties) {
            // todo early-out if we haven't had a layout pass for the element yet

            bool notifyParent = parent != null && !IsIgnored && element.isEnabled;
            bool invalidatePreferredSizeCache = false;
            bool layoutTypeChanged = false;

            for (int i = 0; i < properties.Count; i++) {
                StyleProperty property = properties[i];

                switch (property.propertyId) {

                    case StylePropertyId.PaddingLeft:
                        paddingLeft = property.AsUIFixedLength;
                        resolvedPaddingLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.PaddingRight:
                        paddingRight = property.AsUIFixedLength;
                        resolvedPaddingRight = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.PaddingTop:
                        paddingTop = property.AsUIFixedLength;
                        resolvedPaddingTop = ResolveFixedHeight(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.PaddingBottom:
                        paddingBottom = property.AsUIFixedLength;
                        resolvedPaddingBottom = ResolveFixedHeight(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderLeft:
                        borderLeft = property.AsUIFixedLength;
                        resolvedBorderLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderRight:
                        borderRight = property.AsUIFixedLength;
                        resolvedBorderRight = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderTop:
                        borderTop = property.AsUIFixedLength;
                        resolvedBorderTop = ResolveFixedHeight(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderBottom:
                        borderBottom = property.AsUIFixedLength;
                        resolvedBorderBottom = ResolveFixedHeight(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderRadiusTopLeft:
                        borderRadiusTopLeft = property.AsUIFixedLength;
                        resolvedBorderRadiusTopLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderRadiusTopRight:
                        borderRadiusTopRight = property.AsUIFixedLength;
                        resolvedBorderRadiusTopRight = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderRadiusBottomLeft:
                        borderRadiusBottomLeft = property.AsUIFixedLength;
                        resolvedBorderRadiusBottomLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.BorderRadiusBottomRight:
                        borderRadiusBottomRight = property.AsUIFixedLength;
                        resolvedBorderRadiusBottomRight = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    case StylePropertyId.TextFontSize:
                        resolvedPaddingLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedPaddingRight = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedPaddingTop = ResolveFixedHeight(property.AsUIFixedLength);
                        resolvedPaddingBottom = ResolveFixedHeight(property.AsUIFixedLength);
                        resolvedBorderLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedBorderRight = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedBorderTop = ResolveFixedHeight(property.AsUIFixedLength);
                        resolvedBorderBottom = ResolveFixedHeight(property.AsUIFixedLength);
                        resolvedBorderRadiusTopLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedBorderRadiusTopRight = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedBorderRadiusBottomLeft = ResolveFixedWidth(property.AsUIFixedLength);
                        resolvedBorderRadiusBottomRight = ResolveFixedWidth(property.AsUIFixedLength);
                        break;

                    // todo -- margin should be a fixed measurement probably
                    case StylePropertyId.MarginLeft:
                        marginLeft = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MarginRight:
                        marginRight = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MarginTop:
                        marginTop = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MarginBottom:
                        marginBottom = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.TransformPivotX:
                        transformPivotX = property.AsUIFixedLength;
                        break;

                    case StylePropertyId.TransformPivotY:
                        transformPivotY = property.AsUIFixedLength;
                        break;

                    case StylePropertyId.TransformPositionX:
                        transformPositionX = property.AsTransformOffset;
                        break;

                    case StylePropertyId.TransformPositionY:
                        transformPositionY = property.AsTransformOffset;
                        break;

                    case StylePropertyId.TransformBehaviorX:
                        transformBehaviorX = property.AsTransformBehavior;
                        break;

                    case StylePropertyId.TransformBehaviorY:
                        transformBehaviorY = property.AsTransformBehavior;
                        break;

                    case StylePropertyId.TransformRotation:
                        transformRotation = property.AsFloat;
                        break;

                    case StylePropertyId.TransformScaleX:
                        transformScaleX = property.AsFloat;
                        break;

                    case StylePropertyId.TransformScaleY:
                        transformScaleY = property.AsFloat;
                        break;

                    case StylePropertyId.PreferredWidth:
                        prefWidth = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.PreferredHeight:
                        prefHeight = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MinWidth:
                        minWidth = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MinHeight:
                        minHeight = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MaxWidth:
                        maxWidth = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.MaxHeight:
                        maxHeight = property.AsUIMeasurement;
                        break;

                    case StylePropertyId.ZIndex:
                        zIndex = property.AsInt;
                        break;

                    case StylePropertyId.Layer:
                        layer = property.AsInt;
                        break;

                    case StylePropertyId.LayoutBehavior:
                        layoutBehavior = property.AsLayoutBehavior;
                        UpdateChildren();
                        break;

                    case StylePropertyId.LayoutType:
                        layoutTypeChanged = true;
                        break;

                    case StylePropertyId.OverflowX:
                        overflowX = property.AsOverflow;
                        break;

                    case StylePropertyId.OverflowY:
                        overflowY = property.AsOverflow;
                        break;
                }

                switch (property.propertyId) {
                    case StylePropertyId.MinWidth:
                    case StylePropertyId.MaxWidth:
                    case StylePropertyId.PreferredWidth:
                    case StylePropertyId.MinHeight:
                    case StylePropertyId.MaxHeight:
                    case StylePropertyId.PreferredHeight:
                    case StylePropertyId.AnchorTop:
                    case StylePropertyId.AnchorRight:
                    case StylePropertyId.AnchorBottom:
                    case StylePropertyId.AnchorLeft:
                    case StylePropertyId.AnchorTarget:
                        invalidatePreferredSizeCache = true;
                        break;
                }
            }

            if (invalidatePreferredSizeCache) {
                if (notifyParent) {
                    RequestContentSizeChangeLayout();
                }

                InvalidatePreferredSizeCache();
            }

            OnStylePropertyChanged(properties);

            if (notifyParent) {
                parent.OnChildStylePropertyChanged(this, properties);
            }


            return layoutTypeChanged;
        }

        public struct LayoutBoxSize {

            public readonly float minSize;
            public readonly float maxSize;
            public readonly float clampedSize;

            public LayoutBoxSize(float minSize, float maxSize, float prfSize) {
                this.minSize = minSize;
                this.maxSize = maxSize;
                this.clampedSize = Mathf.Max(minSize, Mathf.Min(prfSize, maxSize));
            }

        }

        private struct WidthCache {

            public int next;

            public int width0;
            public int width1;
            public int width2;

            public float height0;
            public float height1;
            public float height2;

        }

        public OffsetRect GetMargin(float width) {
            return new OffsetRect(
                GetMarginTop(width),
                GetMarginRight(),
                GetMarginBottom(width),
                GetMarginLeft()
            );
        }

        protected abstract void OnChildrenChanged();

        public virtual void OnSpawn(UIElement element) { }

        public virtual void OnRelease() {
            this.element = null;
            this.style = null;
            this.children.Clear();
            this.cachedPreferredWidth = -1;
            this.view = null;
            this.markedForLayout = true;
        }

        internal void Release() {
            pool?.Release(this);
        }

    }

}
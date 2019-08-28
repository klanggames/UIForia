using System;
using System.Diagnostics;
using SVGX;
using UIForia.Elements;
using UIForia.Rendering;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UIForia.Layout {

    public struct SizeConstraints {

        public float minWidth;
        public float maxWidth;
        public float prefWidth;
        public float minHeight;
        public float maxHeight;
        public float prefHeight;

    }

    public abstract class FastLayoutBox {

        public LayoutRenderFlag flags;

        public FastLayoutBox parent;
        public FastLayoutBox firstChild;
        public FastLayoutBox nextSibling;

        // optimized to use bits for units & holds resolved value 
        public OffsetRect paddingBox;
        public OffsetRect borderBox;

        public UIMeasurement marginTop;
        public UIMeasurement marginRight;
        public UIMeasurement marginBottom;
        public UIMeasurement marginLeft;

        public UIMeasurement minWidth;
        public UIMeasurement maxWidth;
        public UIMeasurement prefWidth;
        public UIMeasurement minHeight;
        public UIMeasurement maxHeight;
        public UIMeasurement prefHeight;

        public UIFixedLength transformPositionX;
        public UIFixedLength transformPositionY;

        public int traversalIndex;

        public Size allocatedSize;
        public Size contentSize;
        public Size size;
        public Vector2 allocatedPosition;
        public Vector2 alignedPosition;

        public BlockSize containingBoxWidth;
        public BlockSize containingBoxHeight;

        public LayoutFit selfLayoutFitHorizontal;
        public LayoutFit selfLayoutFitVertical;

        public OffsetMeasurement parentAlignmentHorizontal;
        public OffsetMeasurement parentAlignmentVertical;

        public OffsetMeasurement selfAlignmentVertical;
        public OffsetMeasurement selfAlignmentHorizontal;

        public UIElement element;
        public LayoutOwner owner;

        public float rotation;
        public float pivotX;
        public float pivotY;
        public float scaleX;
        public float scaleY;
        
        public AlignmentBehavior alignmentTargetX;
        public AlignmentBehavior alignmentTargetY;

        public virtual void AddChild(FastLayoutBox child) {
            child.parent = this;

            if (firstChild == null) {
                firstChild = child;
                OnChildAdded(child, 0);
                return;
            }

            FastLayoutBox ptr = firstChild;
            FastLayoutBox trail = null;
            int idx = 0;

            if (ptr == null) {
                firstChild = child;
                child.nextSibling = null;
                OnChildAdded(child, 0);
                return;
            }

            while (ptr != null) {
                if (ptr.element.parent == child.element.parent) {
                    if (ptr.element.siblingIndex > child.element.siblingIndex) {
                        child.nextSibling = ptr.nextSibling;
                        ptr.nextSibling = child;
                        OnChildAdded(child, idx);
                        return;
                    }
                }
                else {
                    if (ptr.element.parent.depth == child.element.parent.depth) {
                        // find common parent, compare sibling index
                        throw new NotImplementedException();
                    }
                    else {
                        if (ptr.element.depth > child.element.depth) {
                            child.nextSibling = ptr.nextSibling;
                            ptr.nextSibling = child;
                            OnChildAdded(child, idx);
                            return;
                        }
                    }
                }

                trail = ptr;
                ptr = ptr.nextSibling;
                idx++;

                if (idx > 1000) {
                    throw new Exception("fail layout");
                }
            }

            child.nextSibling = null;
            trail.nextSibling = child;
            OnChildAdded(child, idx);

            //todo this always add to end, we actually want an insert 
        }

        public virtual void RemoveChild(FastLayoutBox child) {
            int idx = 0;
            FastLayoutBox ptr = firstChild;

            Debug.Assert(child != null, "child != null");

            while (ptr != null && ptr != child) {
                idx++;
                ptr = ptr.nextSibling;
            }

            if (child == firstChild) {
                firstChild = firstChild.nextSibling;
            }

            OnChildRemoved(child, idx);
        }

        public virtual void UpdateStyleData() {
            minWidth = element.style.MinWidth;
            maxWidth = element.style.MaxWidth;
            prefWidth = element.style.PreferredWidth;

            minHeight = element.style.MinHeight;
            maxHeight = element.style.MaxHeight;
            prefHeight = element.style.PreferredHeight;

            marginTop = element.style.MarginTop;
            marginRight = element.style.MarginRight;
            marginBottom = element.style.MarginBottom;
            marginLeft = element.style.MarginLeft;

            transformPositionX = element.style.TransformPositionX;
            transformPositionY = element.style.TransformPositionY;
            rotation = element.style.TransformRotation;
            scaleX = element.style.TransformScaleX;
            scaleY = element.style.TransformScaleY;

            alignmentTargetX = element.style.AlignmentBehaviorX;
            alignmentTargetY = element.style.AlignmentBehaviorY;
            
            selfLayoutFitHorizontal = element.style.LayoutFitHorizontal;
            selfLayoutFitVertical = element.style.LayoutFitVertical;
            
            MarkForLayout();
        }

        protected abstract void PerformLayout();

        protected virtual void OnChildAdded(FastLayoutBox child, int index) { }

        protected virtual void OnChildRemoved(FastLayoutBox child, int index) { }

        public abstract float GetIntrinsicMinWidth();

        public abstract float GetIntrinsicMinHeight();

        public abstract float GetIntrinsicPreferredWidth();

        public abstract float GetIntrinsicPreferredHeight();

        public float ResolveWidth(in BlockSize resolvedBlockSize, in UIMeasurement measurement) {
            float value = measurement.value;

            switch (measurement.unit) {
                case UIMeasurementUnit.Content: {
                    float width = ComputeContentWidth(resolvedBlockSize);
                    float baseVal = width;
                    // todo -- try not to fuck with style here
                    baseVal += ResolveFixedSize(width, element.style.PaddingLeft);
                    baseVal += ResolveFixedSize(width, element.style.PaddingRight);
                    baseVal += ResolveFixedSize(width, element.style.BorderRight);
                    baseVal += ResolveFixedSize(width, element.style.BorderLeft);
                    if (baseVal < 0) baseVal = 0;
                    float retn = measurement.value * baseVal;
                    return retn > 0 ? retn : 0;
                }

                case UIMeasurementUnit.FitContent:
                    float min = GetIntrinsicMinWidth();
                    float pref = GetIntrinsicPreferredWidth();
                    return Mathf.Max(min, Mathf.Min(resolvedBlockSize.contentAreaSize, pref));

                case UIMeasurementUnit.Pixel:
                    return value;

                case UIMeasurementUnit.Em:
                    return 0;

                case UIMeasurementUnit.ViewportWidth:
                    return element.View.Viewport.width * value;

                case UIMeasurementUnit.ViewportHeight:
                    return element.View.Viewport.height * value;

                case UIMeasurementUnit.IntrinsicMinimum:
                    return GetIntrinsicMinWidth();

                case UIMeasurementUnit.IntrinsicPreferred:
                    return GetIntrinsicPreferredWidth();

                case UIMeasurementUnit.ParentSize:
                    return resolvedBlockSize.size * measurement.value;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    return resolvedBlockSize.contentAreaSize * measurement.value;
            }

            return 0;
        }

        public float ResolveHeight(float width, in BlockSize blockWidth, in BlockSize blockHeight, in UIMeasurement measurement) {
            float value = measurement.value;

            switch (measurement.unit) {
                case UIMeasurementUnit.Content:
                    float height = ComputeContentHeight(width, blockWidth, blockHeight);
                    float baseVal = height;

                    baseVal += ResolveFixedSize(height, element.style.PaddingTop);
                    baseVal += ResolveFixedSize(height, element.style.PaddingBottom);
                    baseVal += ResolveFixedSize(height, element.style.BorderBottom);
                    baseVal += ResolveFixedSize(height, element.style.BorderTop);

                    if (baseVal < 0) baseVal = 0;
                    float retn = measurement.value * baseVal;
                    return retn > 0 ? retn : 0;

                case UIMeasurementUnit.FitContent:
                    float min = GetIntrinsicMinHeight();
                    float pref = GetIntrinsicPreferredHeight();
                    return Mathf.Max(min, Mathf.Min(blockHeight.contentAreaSize, pref));

                case UIMeasurementUnit.Pixel:
                    return value;

                case UIMeasurementUnit.Em:
                    return 0;


                case UIMeasurementUnit.ViewportWidth:
                    return element.View.Viewport.width * value;

                case UIMeasurementUnit.ViewportHeight:
                    return element.View.Viewport.height * value;

                case UIMeasurementUnit.IntrinsicMinimum:
                    return GetIntrinsicMinHeight();

                case UIMeasurementUnit.IntrinsicPreferred:
                    return GetIntrinsicPreferredHeight();

                case UIMeasurementUnit.ParentSize:
                    return blockHeight.size * measurement.value;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    return blockHeight.contentAreaSize * measurement.value;
            }

            return 0;
        }

        public abstract float ComputeContentWidth(BlockSize blockWidth);

        public abstract float ComputeContentHeight(float width, BlockSize blockWidth, BlockSize blockHeight);

        [DebuggerStepThrough]
        public float ResolveFixedWidth(UIFixedLength width) {
            switch (width.unit) {
                case UIFixedUnit.Pixel:
                    return width.value;

                case UIFixedUnit.Percent:
                    return size.width * width.value;

                case UIFixedUnit.ViewportHeight:
                    return element.View.Viewport.height * width.value;

                case UIFixedUnit.ViewportWidth:
                    return element.View.Viewport.width * width.value;

                case UIFixedUnit.Em:
                    return element.style.GetResolvedFontSize() * width.value;

                default:
                    return 0;
            }
        }

        [DebuggerStepThrough]
        public float ResolveFixedSize(float baseSize, UIFixedLength fixedSize) {
            switch (fixedSize.unit) {
                case UIFixedUnit.Pixel:
                    return fixedSize.value;

                case UIFixedUnit.Percent:
                    return baseSize * fixedSize.value;

                case UIFixedUnit.ViewportHeight:
                    return element.View.Viewport.height * fixedSize.value;

                case UIFixedUnit.ViewportWidth:
                    return element.View.Viewport.width * fixedSize.value;

                case UIFixedUnit.Em:
                    return element.style.GetResolvedFontSize() * fixedSize.value;

                default:
                    return 0;
            }
        }

        [DebuggerStepThrough]
        public float ResolveFixedHeight(UIFixedLength height) {
            switch (height.unit) {
                case UIFixedUnit.Pixel:
                    return height.value;

                case UIFixedUnit.Percent:
                    return size.height * height.value;

                case UIFixedUnit.ViewportHeight:
                    return element.View.Viewport.height * height.value;

                case UIFixedUnit.ViewportWidth:
                    return element.View.Viewport.width * height.value;

                case UIFixedUnit.Em:
                    return element.style.GetResolvedFontSize() * height.value;

                default:
                    return 0;
            }
        }

        public void ApplyHorizontalLayout(float localX, in BlockSize containingWidth, float allocatedWidth, float preferredWidth, float alignment, LayoutFit layoutFit) {

            allocatedPosition.x = localX;

            pivotX = ResolveFixedWidth(element.style.TransformPivotX);

            if (selfLayoutFitHorizontal != LayoutFit.Unset) {
                layoutFit = selfLayoutFitHorizontal;
            }

            Size oldSize = size;

            paddingBox.left = ResolveFixedWidth(element.style.PaddingLeft);
            paddingBox.right = ResolveFixedWidth(element.style.PaddingRight);
            borderBox.left = ResolveFixedWidth(element.style.BorderLeft);
            borderBox.right = ResolveFixedWidth(element.style.BorderRight);

            size.width = preferredWidth;

            switch (layoutFit) {
                case LayoutFit.Unset:
                case LayoutFit.None:
                    break;

                case LayoutFit.Grow:
                    if (allocatedWidth > preferredWidth) {
                        size.width = allocatedWidth;
                    }

                    break;

                case LayoutFit.Shrink:
                    if (allocatedWidth < preferredWidth) {
                        size.width = allocatedWidth;
                    }

                    break;

                case LayoutFit.Fill:
                    size.width = allocatedWidth;
                    break;
            }

            allocatedSize.width = allocatedWidth;
            containingBoxWidth = containingWidth;

            float originBase = localX;
            float originOffset = allocatedWidth * alignment;
            float offset = size.width * -alignment;

            alignedPosition.x = originBase + originOffset + offset;

            ref SizeSet sizeSet = ref owner.sizeSetList.array[traversalIndex];
            sizeSet.size = size;
            sizeSet.allocatedSize = allocatedSize;

            ref PositionSet positionSet = ref owner.positionSetList.array[traversalIndex];
            positionSet.allocatedPosition = allocatedPosition;
            positionSet.alignedPosition = alignedPosition;

            // if content size changed we need to layout todo account for padding
            if ((int) oldSize.width != (int) size.width) {
                flags |= LayoutRenderFlag.NeedsLayout;
            }

            // size = how big am I actually
            // allocated size = size my parent told me to be
            // content size = extents of my content
        }

        public void ApplyVerticalLayout(float localY, in BlockSize containingHeight, float allocatedHeight, float preferredHeight, float alignment, LayoutFit layoutFit) {
            allocatedPosition.y = localY;

            if (selfLayoutFitVertical != LayoutFit.Unset) {
                layoutFit = selfLayoutFitVertical;
            }

            Size oldSize = size;
            pivotY = ResolveFixedWidth(element.style.TransformPivotY);

            paddingBox.top = ResolveFixedHeight(element.style.PaddingTop);
            paddingBox.bottom = ResolveFixedHeight(element.style.PaddingBottom);
            borderBox.top = ResolveFixedHeight(element.style.BorderTop);
            borderBox.bottom = ResolveFixedHeight(element.style.BorderBottom);

            size.height = preferredHeight;

            switch (layoutFit) {
                case LayoutFit.Unset:
                case LayoutFit.None:
                    break;

                case LayoutFit.Grow:
                    if (allocatedHeight > preferredHeight) {
                        size.height = allocatedHeight;
                    }

                    break;

                case LayoutFit.Shrink:
                    if (allocatedHeight < preferredHeight) {
                        size.height = allocatedHeight;
                    }

                    break;

                case LayoutFit.Fill:
                    size.height = allocatedHeight;
                    break;
            }

            // alignment code here

            float originBase = localY;
            float originOffset = allocatedHeight * alignment;
            float offset = size.height * -alignment;
            
            alignedPosition.y = originBase + originOffset + offset;

            allocatedSize.height = allocatedHeight;
            containingBoxHeight = containingHeight;

            ref SizeSet sizeSet = ref owner.sizeSetList.array[traversalIndex];
            sizeSet.size = size;
            sizeSet.allocatedSize = allocatedSize;

            ref PositionSet positionSet = ref owner.positionSetList.array[traversalIndex];
            positionSet.allocatedPosition = allocatedPosition;
            positionSet.alignedPosition = alignedPosition;

            if ((int) oldSize.height != (int) size.height) {
                flags |= LayoutRenderFlag.NeedsLayout;
            }
        }

        public void GetWidth(in BlockSize lastResolvedWidth, ref SizeConstraints output) {
            output.minWidth = ResolveWidth(lastResolvedWidth, minWidth);
            output.maxWidth = ResolveWidth(lastResolvedWidth, maxWidth);
            output.prefWidth = ResolveWidth(lastResolvedWidth, prefWidth);

            if (output.prefWidth < output.minWidth) output.prefWidth = output.minWidth;
            if (output.prefWidth > output.maxWidth) output.prefWidth = output.maxWidth;
        }

        public void GetHeight(float width, in BlockSize blockWidth, in BlockSize blockHeight, ref SizeConstraints output) {
            output.minHeight = ResolveHeight(width, blockWidth, blockHeight, minHeight);
            output.maxHeight = ResolveHeight(width, blockWidth, blockHeight, maxHeight);
            output.prefHeight = ResolveHeight(width, blockWidth, blockHeight, prefHeight);

            if (output.prefHeight < output.minHeight) output.prefHeight = output.minHeight;
            if (output.prefHeight > output.maxHeight) output.prefHeight = output.maxHeight;
        }

        public void Layout() {
            // todo -- size check

            if ((flags & LayoutRenderFlag.NeedsLayout) == 0) {
                return;
            }

            // pivotY = ResolveFixedHeight(element.style.TransformPivotY);
            // pivotX = ResolveFixedWidth(element.style.TransformPivotX);
            // scaleX = element.style.TransformScaleX;
            // scaleY = element.style.TransformScaleY;
            // rotation = element.style.TransformRotation;

            PerformLayout();

            flags &= ~LayoutRenderFlag.NeedsLayout;

            FastLayoutBox child = firstChild;
            while (child != null) {
                child.Layout();
                // todo find out who sets nextSibling to child
                if (child == child.nextSibling) {
                    break;
                }

                child = child.nextSibling;
            }

            // todo -- compute content size & local overflow? might need to happen elsewhere
        }


        protected virtual void OnChildSizeChanged(FastLayoutBox child) {
            if ((flags & LayoutRenderFlag.NeedsLayout) != 0) {
                return;
            }

            if ((child.flags & LayoutRenderFlag.Ignored) != 0) {
                return;
            }

            if (prefWidth.unit == UIMeasurementUnit.Content || prefHeight.unit == UIMeasurementUnit.Content) {
                MarkForLayout();
            }
        }

        protected virtual void ChildMarkedForLayout(FastLayoutBox child) {
            if ((flags & LayoutRenderFlag.NeedsLayout) != 0) {
                return;
            }

            flags |= LayoutRenderFlag.NeedsLayout;
            owner.toLayout.Add(this);

            // if this element is not sized based on its content we can stop propagating
            if (prefWidth.unit != UIMeasurementUnit.Content && prefHeight.unit != UIMeasurementUnit.Content) {
                return;
            }

            parent?.ChildMarkedForLayout(this);
        }

        public void MarkForLayout() {
            if ((flags & LayoutRenderFlag.NeedsLayout) != 0) {
                return;
            }

            flags |= LayoutRenderFlag.NeedsLayout;
            owner.toLayout.Add(this);

            parent?.ChildMarkedForLayout(this);
        }

        public virtual void OnStyleChanged(StructList<StyleProperty> changeList) {
            bool marked = false;

            int count = changeList.size;
            StyleProperty[] properties = changeList.array;

            bool sizeChanged = false;

            for (int i = 0; i < count; i++) {
                ref StyleProperty property = ref properties[i];

                switch (property.propertyId) {
                    case StylePropertyId.PaddingLeft:
                        paddingBox.left = ResolveFixedWidth(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.PaddingRight:
                        paddingBox.right = ResolveFixedWidth(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.PaddingTop:
                        paddingBox.top = ResolveFixedHeight(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.PaddingBottom:
                        paddingBox.bottom = ResolveFixedHeight(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.BorderLeft:
                        borderBox.left = ResolveFixedWidth(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.BorderRight:
                        borderBox.right = ResolveFixedWidth(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.BorderTop:
                        borderBox.top = ResolveFixedHeight(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.BorderBottom:
                        borderBox.bottom = ResolveFixedHeight(property.AsUIFixedLength);
                        marked = true;
                        break;

                    case StylePropertyId.TextFontSize:

                        paddingBox.left = ResolveFixedWidth(element.style.PaddingLeft);
                        paddingBox.right = ResolveFixedWidth(element.style.PaddingRight);
                        paddingBox.top = ResolveFixedHeight(element.style.PaddingTop);
                        paddingBox.bottom = ResolveFixedHeight(element.style.PaddingBottom);

                        borderBox.left = ResolveFixedWidth(element.style.BorderLeft);
                        borderBox.right = ResolveFixedWidth(element.style.BorderRight);
                        borderBox.top = ResolveFixedHeight(element.style.BorderTop);
                        borderBox.bottom = ResolveFixedHeight(element.style.BorderBottom);

                        // anything else em sized should be updated here too

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

                    case StylePropertyId.PreferredWidth:
                        prefWidth = property.AsUIMeasurement;
                        sizeChanged = true;
                        marked = true;
                        break;

                    case StylePropertyId.PreferredHeight:
                        prefHeight = property.AsUIMeasurement;
                        sizeChanged = true;
                        marked = true;
                        break;

                    case StylePropertyId.MinWidth:
                        minWidth = property.AsUIMeasurement;
                        sizeChanged = true;
                        marked = true;
                        break;

                    case StylePropertyId.MinHeight:
                        minHeight = property.AsUIMeasurement;
                        marked = true;
                        break;

                    case StylePropertyId.MaxWidth:
                        maxWidth = property.AsUIMeasurement;
                        sizeChanged = true;
                        marked = true;
                        break;

                    case StylePropertyId.MaxHeight:
                        maxHeight = property.AsUIMeasurement;
                        sizeChanged = true;
                        marked = true;
                        break;

                    case StylePropertyId.ZIndex:
                        // zIndex = property.AsInt;
                        break;

                    case StylePropertyId.Layer:
                        // layer = property.AsInt;
                        break;

                    case StylePropertyId.LayoutBehavior:
                        // layoutBehavior = property.AsLayoutBehavior;
                        // UpdateChildren();
                        break;

                    case StylePropertyId.LayoutType:
                        //layoutTypeChanged = true;
                        break;

                    case StylePropertyId.OverflowX:
                        // overflowX = property.AsOverflow;
                        break;

                    case StylePropertyId.OverflowY:
                        // overflowY = property.AsOverflow;
                        break;
                    case StylePropertyId.AlignmentOffsetX:
                        break;

                    case StylePropertyId.AlignmentOffsetY:
                        break;

                    case StylePropertyId.TransformPositionX:
                        transformPositionX = property.AsUIFixedLength;
                        break;

                    case StylePropertyId.TransformPositionY:
                        transformPositionY = property.AsUIFixedLength;
                        break;
                    
                    case StylePropertyId.AlignmentBehaviorX:
                        alignmentTargetX = property.AsAlignmentBehavior;
                        break;
                    
                    case StylePropertyId.AlignmentBehaviorY:
                        alignmentTargetY = property.AsAlignmentBehavior;
                        break;
                    case StylePropertyId.LayoutFitHorizontal:
                        selfLayoutFitHorizontal = property.AsLayoutFit;
                        break;
                    case StylePropertyId.LayoutFitVertical:
                        selfLayoutFitVertical = property.AsLayoutFit;
                        break;
                }
            }

            if (marked) {
                MarkForLayout();
            }

            if (sizeChanged) {
                parent?.OnChildSizeChanged(this);
            }

            parent?.OnChildStyleChanged(this, changeList);
        }

        public virtual void SetChildren(LightList<FastLayoutBox> container) {
            if (container.size == 0) {
                firstChild = null;
                return;
            }

            firstChild = container[0];
            for (int i = 0; i < container.size; i++) {
                FastLayoutBox ptr = container[i];
                ptr.parent = this;

                if (i != container.size - 1) {
                    ptr.nextSibling = container[i + 1];
                }
            }
        }

        public FastLayoutBox[] __DebugChildren {
            get {
                LightList<FastLayoutBox> boxList = new LightList<FastLayoutBox>();

                FastLayoutBox ptr = firstChild;
                while (ptr != null) {
                    boxList.Add(ptr);
                    ptr = ptr.nextSibling;
                }

                return boxList.ToArray();
            }
        }
        
        public void GetMarginHorizontal(BlockSize blockWidth, ref OffsetRect margin) {
            switch (marginLeft.unit) {
                case UIMeasurementUnit.Pixel:
                    margin.left = marginLeft.value;
                    break;

                case UIMeasurementUnit.Em:
                    margin.left = element.style.GetResolvedFontSize() * marginLeft.value;
                    break;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    margin.left = blockWidth.contentAreaSize * marginLeft.value;
                    break;
                case UIMeasurementUnit.ParentSize:
                    margin.left = blockWidth.size * marginLeft.value;
                    break;
            }

            switch (marginRight.unit) {
                case UIMeasurementUnit.Pixel:
                    margin.right = marginRight.value;
                    break;

                case UIMeasurementUnit.Em:
                    margin.right = element.style.GetResolvedFontSize() * marginRight.value;
                    break;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    margin.right = blockWidth.contentAreaSize * marginRight.value;
                    break;
                case UIMeasurementUnit.ParentSize:
                    margin.right = blockWidth.size * marginRight.value;
                    break;
            }

            margin.left = Math.Max(margin.left, 0);
            margin.right = Math.Max(margin.right, 0);
        }

        public void GetMarginVertical(BlockSize blockHeight, ref OffsetRect margin) {
            switch (marginTop.unit) {
                case UIMeasurementUnit.Pixel:
                    margin.top = marginTop.value;
                    break;

                case UIMeasurementUnit.Em:
                    margin.top = element.style.GetResolvedFontSize() * marginTop.value;
                    break;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    margin.top = blockHeight.contentAreaSize * marginTop.value;
                    break;
                case UIMeasurementUnit.ParentSize:
                    margin.top = blockHeight.size * marginTop.value;
                    break;
            }

            switch (marginBottom.unit) {
                case UIMeasurementUnit.Pixel:
                    margin.bottom = marginBottom.value;
                    break;

                case UIMeasurementUnit.Em:
                    margin.bottom = element.style.GetResolvedFontSize() * marginBottom.value;
                    break;

                case UIMeasurementUnit.Percentage:
                case UIMeasurementUnit.ParentContentArea:
                    margin.bottom = blockHeight.contentAreaSize * marginBottom.value;
                    break;
                case UIMeasurementUnit.ParentSize:
                    margin.bottom = blockHeight.size * marginBottom.value;
                    break;
            }

            margin.top = Math.Max(margin.top, 0);
            margin.bottom = Math.Max(margin.bottom, 0);
        }

        public void Replace(FastLayoutBox box) {
            firstChild = box.firstChild;
            parent = box.parent;
            FastLayoutBox ptr = firstChild;
            while (ptr != null) {
                ptr.parent = this;
                ptr = ptr.nextSibling;
            }
        }

        protected virtual void OnChildStyleChanged(FastLayoutBox child, StructList<StyleProperty> changeList) { }

        public virtual void OnInitialize() { }
        public virtual void OnDestroy() { }

    }

    public struct BlockSize {

        public float size;
        public float contentAreaSize;

    }

}
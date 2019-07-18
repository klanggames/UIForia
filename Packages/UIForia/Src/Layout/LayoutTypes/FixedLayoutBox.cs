using UIForia.Elements;
using UIForia.Rendering;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Layout.LayoutTypes {

    public class FixedLayoutBox : LayoutBox {
        
        private OffsetRect padding;
        private OffsetRect border;
        
        protected override float ComputeContentWidth() {
            float minX = 0;
            float maxX = 0;
            for (int i = 0; i < children.Count; i++) {
                LayoutBox child = children[i];

                // todo - borked
                float x = 0;

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x + child.GetWidths().clampedSize);
            }

            return maxX - minX;
        }

        protected override float ComputeContentHeight(float width) {
            float minY = 0;
            float maxY = 0;
            for (int i = 0; i < children.Count; i++) {
                LayoutBox child = children[i];

                float y = 0; //child.TransformY;

                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y + child.GetHeights(child.GetWidths().clampedSize).clampedSize);
            }

            return Mathf.Max(0, maxY - minY);
        }

        protected override void OnChildrenChanged() { }

        public override void RunLayout() {
            float minX = 0;
            float maxX = 0;
            float minY = 0;
            float maxY = 0;

            for (int i = 0; i < children.Count; i++) {
                LayoutBox child = children[i];

                float x = 0;// child.TransformX;
                float y = 0;// child.TransformY;

                float width = child.GetWidths().clampedSize;
                LayoutBoxSize heights = child.GetHeights(width);

                child.SetAllocatedRect(x, y, width, heights.clampedSize);

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x + width);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y + heights.clampedSize);
            }

            padding = new OffsetRect(resolvedPaddingTop, resolvedPaddingRight, resolvedPaddingBottom, resolvedPaddingLeft);
            border = new OffsetRect(resolvedBorderBottom, resolvedBorderRight, resolvedBorderBottom, resolvedBorderLeft);

            Size size = default;
         
                
            if (allocatedWidth > size.width) {
                actualWidth = allocatedWidth;
            }
            else {
                actualWidth = size.width + padding.Horizontal + border.Horizontal;
            }

            if (allocatedHeight > size.height) {
                actualHeight = allocatedHeight;
            }
            else {
                actualHeight = size.height + padding.Vertical + border.Vertical;
            }
        }

        public override void OnStylePropertyChanged(StructList<StyleProperty> properties) {
            for (int i = 0; i < properties.Count; i++) {
                StyleProperty property = properties[i];

                switch (property.propertyId) {
                    case StylePropertyId.TransformPositionX:
                    case StylePropertyId.TransformPositionY:
                        RequestContentSizeChangeLayout();
                        return;
                }
            }
        }

    }

}
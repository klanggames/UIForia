using Src.Layout;

namespace Rendering {

    public partial class UIStyleSet {

        public LayoutWrap GetFlexWrapMode(StyleState state) {
            return (LayoutWrap) GetEnumProperty(StylePropertyId.FlexLayoutWrap, state);
        }

        public LayoutDirection GetFlexLayoutDirection(StyleState state) {
            return (LayoutDirection) GetEnumProperty(StylePropertyId.FlexLayoutDirection, state);
        }

        public MainAxisAlignment GetFlexLayoutMainAlignment(StyleState state) {
            return (MainAxisAlignment) GetEnumProperty(StylePropertyId.FlexLayoutMainAxisAlignment, state);
        }

        public CrossAxisAlignment GetFlexLayoutCrossAlignment(StyleState state) {
            return (CrossAxisAlignment) GetEnumProperty(StylePropertyId.FlexLayoutCrossAxisAlignment, state);
        }

        public void SetFlexWrapMode(LayoutWrap wrapMode, StyleState state) {
            SetEnumProperty(StylePropertyId.FlexLayoutWrap, (int) wrapMode, state);
        }

        public void SetFlexDirection(LayoutDirection direction, StyleState state) {
            SetEnumProperty(StylePropertyId.FlexLayoutDirection, (int) direction, state);
        }

        public void SetFlexMainAxisAlignment(MainAxisAlignment alignment, StyleState state) {
            SetEnumProperty(StylePropertyId.FlexLayoutMainAxisAlignment, (int) alignment, state);
        }

        public void SetFlexCrossAxisAlignment(CrossAxisAlignment alignment, StyleState state) {
            SetEnumProperty(StylePropertyId.FlexLayoutCrossAxisAlignment, (int) alignment, state);
        }

    }

}
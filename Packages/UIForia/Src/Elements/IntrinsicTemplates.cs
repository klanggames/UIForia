using UIForia.Attributes;
using UIForia.UIInput;

namespace UIForia.Elements {

    [TemplateTagName("Group")]
    public class UIGroupElement : UIContainerElement {

        public UIGroupElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Group";
        }

    }

    [TemplateTagName("Panel")]
    public class UIPanelElement : UIContainerElement {

        public UIPanelElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Panel";
        }

    }

    [TemplateTagName("Section")]
    public class UISectionElement : UIContainerElement {
        
        public UISectionElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Section";
        }

    }

    [TemplateTagName("Div")]
    public class UIDivElement : UIContainerElement {

        public UIDivElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Div";
        }

    }

    [TemplateTagName("Header")]
    public class UIHeaderElement : UIContainerElement {

        public UIHeaderElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Header";
        }

    }

    [TemplateTagName("Footer")]
    public class UIFooterElement : UIContainerElement {
        
        public UIFooterElement() {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Footer";
        }

    }

    [TemplateTagName("Label")]
    public class UILabelElement : UITextElement {

        public string forElement;
        
        public UILabelElement(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        [OnMouseClick]
        public void OnClick() {
            UIElement forEl = parent.FindById(forElement);
            if (forEl is IFocusable focusable) {
                Application.InputSystem.RequestFocus(focusable);
            }
        }

        public override string GetDisplayName() {
            return "Label";
        }

    }

    [TemplateTagName("Paragraph")]
    public class UIParagraphElement : UITextElement {

        public UIParagraphElement(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Paragraph";
        }

    }

    [TemplateTagName("Heading1")]
    public class UIHeading1Element : UITextElement {

        public UIHeading1Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Heading1";
        }

    }

    [TemplateTagName("Heading2")]
    public class UIHeading2Element : UITextElement {

        public UIHeading2Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Heading2";
        }

    }

    [TemplateTagName("Heading3")]
    public class UIHeading3Element : UITextElement {

        public UIHeading3Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }
        

        public override string GetDisplayName() {
            return "Heading3";
        }

    }

    [TemplateTagName("Heading4")]
    public class UIHeading4Element : UITextElement {

        public UIHeading4Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Heading4";
        }

    }

    [TemplateTagName("Heading5")]
    public class UIHeading5Element : UITextElement {

        public UIHeading5Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Heading5";
        }

    }

    [TemplateTagName("Heading6")]
    public class UIHeading6Element : UITextElement {

        public UIHeading6Element(string text = "") : base(text) {
            flags |= UIElementFlags.BuiltIn;
        }

        public override string GetDisplayName() {
            return "Heading6";
        }

    }
}
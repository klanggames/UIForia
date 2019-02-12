namespace UIForia.Rendering {

    public struct UIStyleGroup {

        public string name { get; internal set; }
        public StyleType styleType { get; internal set; }
        public UIStyle hover { get; internal set; }
        public UIStyle normal { get; internal set; }
        public UIStyle active { get; internal set; }
        public UIStyle inactive { get; internal set; }
        public UIStyle focused { get; internal set; }
        public UIStyleRule rule { get; internal set; }

        public static bool operator ==(UIStyleGroup x, UIStyleGroup y) {
            return string.Equals(x.name, y.name) 
                   && Equals(x.hover, y.hover) 
                   && Equals(x.normal, y.normal) 
                   && Equals(x.active, y.active) 
                   && Equals(x.inactive, y.inactive) 
                   && x.styleType == y.styleType 
                   && Equals(x.focused, y.focused)
                   && x.rule == y.rule;
        }

        public static bool operator !=(UIStyleGroup x, UIStyleGroup y) {
            return !(x == y);
        }
    }

}

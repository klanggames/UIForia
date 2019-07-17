using System;

[Flags]
internal enum UIElementFlags {

    TextElement = 1 << 0,
    ImplicitElement = 1 << 1,
    Enabled = 1 << 2,
    AncestorEnabled = 1 << 3,
    Alive = 1 << 4,
    AncestorDestroyed = 1 << 5,
    Primitive = 1 << 6,
    HasBeenEnabled = 1 << 7,
    Created = 1 << 8,
    VirtualElement = 1 << 9,
    TemplateRoot = 1 << 10,
    BuiltIn = 1 << 11,
    CreatedExplicitly = 1 << 12, // was this element created via user code?
    SelfAndAncestorEnabled = Alive | Enabled | AncestorEnabled,

    Ready = 1 << 13,

    Registered = 1 << 14,

    DebugLayout = 1 << 15,


    Selector_AttributeAdded = 1 << 17,
    Selector_AttributeChanged = 1 << 18,
    Selector_AttributeRemoved = 1 << 19,
    Selector_SiblingIndexChanged = 1 << 20,
    Selector_ChildAdded = 1 << 21,
    Selector_ChildRemoved = 1 << 22,

    SelectorNeedsUpdate = (
        Selector_AttributeAdded |
        Selector_AttributeChanged |
        Selector_AttributeRemoved |
        Selector_ChildAdded |
        Selector_ChildRemoved |
        Selector_SiblingIndexChanged
    )

}
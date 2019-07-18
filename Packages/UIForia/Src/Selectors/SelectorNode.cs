using System;
using UIForia.Elements;
using UIForia.Util;

namespace UIForia.Selectors {

    public class SelectorNode {

        public SelectorNode parent;
        public LightList<SelectorNode> children;
        public UIElement host;
        public LightList<Selector> selectors;

        public void Update(UIElement element) {
            throw new NotImplementedException();
        }

    }

    // mirrors UIElementFlag 
    [Flags]
    public enum SelectorFlags {

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

    public class Selector {

        public string name;
        public Func<UIElement, bool> fn;
        public LightList<UIElement> matchedElements;
        public SelectorFlags mask;

        public bool needsUpdate;

        public void HandleAttributeChanged(UIElement element, string attributeName, string currentValue, string previousValue) {
            
        }
        
        public Selector() {
            
        }

        public void Run() { }

        public void ElementChanged(UIElement element) {
            if ((mask & (SelectorFlags) element.flags) == 0) {
                return;
            }

            bool matches = fn(element);
            int matchIdx = matchedElements.IndexOf(element);
            bool wasMatch = matchIdx != -1;

            if (matches && !wasMatch) {
                matchedElements = matchedElements ?? LightList<UIElement>.Get();
                matchedElements.Add(element);
                element.style.AddSelectorStyleGroup(this);
            }
            else if (!matches && wasMatch) {
                matchedElements.RemoveAt(matchIdx);
                element.style.RemoveSelectorStyleGroup(this);
            }
        }

        public void ElementEnabled(UIElement element) {
            if (fn(element)) {
                matchedElements = matchedElements ?? LightList<UIElement>.Get();
                matchedElements.Add(element);
                element.style.AddSelectorStyleGroup(this);
            }
        }

        public void ElementDisabled(UIElement element) {
            matchedElements?.Remove(element);
        }

        public void ElementDestroyed(UIElement element) {
            matchedElements?.Remove(element);
        }

    }

}
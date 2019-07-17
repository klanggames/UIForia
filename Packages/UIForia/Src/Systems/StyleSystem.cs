using System;
using System.Collections.Generic;
using UIForia.Elements;
using UIForia.Rendering;
using UIForia.Util;

namespace UIForia.Systems {

    public interface IStylePropertiesWillChangeHandler {

        void OnStylePropertiesWillChange();

    }

    public interface IStylePropertiesDidChangeHandler {

        void OnStylePropertiesDidChange();

    }

    public interface IStyleChangeHandler {

        void OnStylePropertyChanged(in StyleProperty property);

    }

    public class StyleSystem : IStyleSystem {

        public event Action<UIElement, StructList<StyleProperty>> onStylePropertyChanged;

        private static readonly Stack<UIElement> s_ElementStack = new Stack<UIElement>();

        private readonly IntMap<ChangeSet> m_ChangeSets;

        public StyleSystem() {
            this.m_ChangeSets = new IntMap<ChangeSet>();
        }
        
        public void OnReset() { }

        public void OnElementCreated(UIElement element) {

            element.style.styleSystem = this;
        }
        
        private void OnElementEnabledStep(UIElement element, StructList<StyleProperty> parentProperties) {

            if (element.isDisabled) {
                return;
            }
            
            int count = parentProperties.Count;
            StyleProperty[] parentPropertiesArray = parentProperties.Array;

            if (element.children == null || element.children.Count == 0) {
                for (int i = 0; i < count; i++) {
                    element.style.SetInheritedStyle(parentPropertiesArray[i]);
                }

                return;
            }

            for (int i = 0; i < count; i++) {
                element.style.SetInheritedStyle(parentPropertiesArray[i]);
            }
            
            StructList<StyleProperty> inheritedProperties = StructList<StyleProperty>.Get();
            inheritedProperties.EnsureCapacity(count);
            StyleProperty[] inheritedPropertiesArray = inheritedProperties.Array;

            for (int i = 0; i < count; i++) {
                inheritedPropertiesArray[i] = element.style.GetComputedStyleProperty(StyleUtil.InheritedProperties[i]);
            }
            inheritedProperties.Count = count;

            for (int i = 0; i < element.children.Count; i++) {
                OnElementEnabledStep(element.children[i], inheritedProperties);
            }

            StructList<StyleProperty>.Release(ref inheritedProperties);
        }

        public void OnUpdate() {
            if (onStylePropertyChanged == null) {
                return;
            }

            m_ChangeSets.ForEach(this, (id, changeSet, self) => {
                if (changeSet.element is IStylePropertiesWillChangeHandler willChangeHandler) {
                    willChangeHandler.OnStylePropertiesWillChange();
                }

                if (changeSet.element.isEnabled) {
                    self.onStylePropertyChanged.Invoke(changeSet.element, changeSet.changes);
                }

                if (changeSet.element is IStyleChangeHandler changeHandler) {
                    StyleProperty[] properties = changeSet.changes.Array;
                    int count = changeSet.changes.Count;
                    for (int i = 0; i < count; i++) {
                        changeHandler.OnStylePropertyChanged(properties[i]);
                    }
                }

                if (changeSet.element is IStylePropertiesDidChangeHandler didChangeHandler) {
                    didChangeHandler.OnStylePropertiesDidChange();
                }

                StructList<StyleProperty>.Release(ref changeSet.changes);
                changeSet.element = null;
            });

            m_ChangeSets.Clear();
        }

        public void OnDestroy() { }

        public void OnViewAdded(UIView view) { }

        public void OnViewRemoved(UIView view) { }

        public void OnElementEnabled(UIElement element) {
            if (element.parent != null) {
                int count = StyleUtil.InheritedProperties.Count;
                UIStyleSet parentStyle = element.parent.style;
                StructList<StyleProperty> inheritedProperties = StructList<StyleProperty>.Get();
                inheritedProperties.EnsureCapacity(count);
                StyleProperty[] inheritedPropertiesArray = inheritedProperties.Array;

                for (int i = 0; i < count; i++) {
                    inheritedPropertiesArray[i] = parentStyle.GetComputedStyleProperty(StyleUtil.InheritedProperties[i]);
                }

                inheritedProperties.Count = count;
                OnElementEnabledStep(element, inheritedProperties);
                StructList<StyleProperty>.Release(ref inheritedProperties);
            }
            else {
                
                StructList<StyleProperty> inheritedProperties = StructList<StyleProperty>.Get();
                inheritedProperties.EnsureCapacity(StyleUtil.InheritedProperties.Count);
                StyleProperty[] inheritedPropertiesArray = inheritedProperties.Array;

                for (int i = 0; i < inheritedProperties.Count; i++) {
                    inheritedPropertiesArray[i] = DefaultStyleValues_Generated.GetPropertyValue(StyleUtil.InheritedProperties[i]);
                }

                OnElementEnabledStep(element, inheritedProperties);
                StructList<StyleProperty>.Release(ref inheritedProperties);
            }

        }

        public void OnElementDisabled(UIElement element) {
            m_ChangeSets.Remove(element.id);
        }

        public void OnElementDestroyed(UIElement element) {
            element.style.styleSystem = null;
            m_ChangeSets.Remove(element.id);
        }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string attributeValue) {
            element.style.UpdateApplicableAttributeRules(attributeName, attributeValue);
        }

        private void AddToChangeSet(UIElement element, StyleProperty property) {
            ChangeSet changeSet;
            if (!m_ChangeSets.TryGetValue(element.id, out changeSet)) {
                changeSet = new ChangeSet(element, StructList<StyleProperty>.Get());
                m_ChangeSets[element.id] = changeSet;
            }

            changeSet.changes.Add(property);
        }

        public void SetStyleProperty(UIElement element, StyleProperty property) {
            
            if (element.isDisabled) return;
            
            AddToChangeSet(element, property);

            if (!StyleUtil.IsInherited(property.propertyId) || element.children == null || element.children.Count == 0) {
                return;
            }

            if (property.IsUnset) {
                UIElement ptr = element.parent;
                StyleProperty parentProperty = StyleProperty.Unset(property.propertyId);

                while (ptr != null) {
                    parentProperty = ptr.style.GetPropertyValue(property.propertyId);
                    if (parentProperty.IsDefined) {
                        break;
                    }

                    ptr = ptr.parent;
                }

                if (!parentProperty.IsDefined) {
                    parentProperty = DefaultStyleValues_Generated.GetPropertyValue(property.propertyId);
                }

                property = parentProperty;
            }

            for (int i = 0; i < element.children.Count; i++) {
                s_ElementStack.Push(element.children[i]);
            }

            while (s_ElementStack.Count > 0) {
                UIElement descendant = s_ElementStack.Pop();

                if (!descendant.style.SetInheritedStyle(property)) {
                    continue;
                }

                // todo -- we might want to cache font size lookups for em values, this would be the place 
                // if (property.propertyId == StylePropertyId.TextFontSize) {
                // do caching    
                // }

                AddToChangeSet(descendant, property);

                if (descendant.children == null) {
                    continue;
                }

                for (int i = 0; i < descendant.children.Count; i++) {
                    s_ElementStack.Push(descendant.children[i]);
                }
            }
        }

        public void UpdateSelectors() {
        }

        public void UpdateBindings() {
        }

        public void UpdateAnimations() {
        }

        public void Flush() {
        }

        private struct ChangeSet {

            public UIElement element;
            public StructList<StyleProperty> changes;

            public ChangeSet(UIElement element, StructList<StyleProperty> changes) {
                this.element = element;
                this.changes = changes;
            }

        }

    }

}
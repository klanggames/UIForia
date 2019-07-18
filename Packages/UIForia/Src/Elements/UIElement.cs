using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using UIForia.Compilers;
using UIForia.Elements.Routing;
using UIForia.Expressions;
using UIForia.Layout;
using UIForia.Rendering;
using UIForia.Routing;
using UIForia.Selectors;
using UIForia.Systems;
using UIForia.Templates;
using UIForia.UIInput;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Elements {

    public struct UIElementRef {

        private readonly int id;
        private UIElement element;

        public UIElementRef(UIElement element) {
            this.id = element?.id ?? -1;
            this.element = element;
        }

        public UIElement Element {
            get {
                if (id != element.id) {
                    element = null;
                    return null;
                }

                return element;
            }
        }
        
        public static implicit operator UIElement(UIElementRef elementRef) {
            return elementRef.Element;
        }
        
    }

    public struct UIElementRef<T> where T : UIElement {

        private readonly int id;
        private T element;

        public UIElementRef(T element) {
            this.id = element?.id ?? -1;
            this.element = element;
        }
        
        public T Element {
            get {
                if (id != element.id) {
                    element = null;
                    return null;
                }

                return element;
            }
        }

        public static implicit operator UIElementRef(UIElementRef<T> elementRef) {
            return new UIElementRef(elementRef.Element);
        }
        
        public static implicit operator UIElement(UIElementRef<T> elementRef) {
            return elementRef.Element;
        }
        
        public static implicit operator T(UIElementRef<T> elementRef) {
            return elementRef.Element;
        }


    }

    public struct ArrayContainer<T> {

        public T[] array;
        public int size;
        
        public ArrayContainer(T[] array, int size = 0) {
            this.array = array;
            this.size = size;
        }
        
    }

    
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public class UIElement : IHierarchical {

        public readonly int id;

        internal LightList<UIElement> children; // todo -- replace w/ linked list & child count

        public ExpressionContext templateContext; // todo -- can probably be moved to binding system
        
        internal UIElementFlags flags;
        internal UIElement parent;

        public readonly LayoutResult layoutResult;

        internal static IntMap<ElementColdData> s_ColdDataMap = new IntMap<ElementColdData>();

        public readonly UIStyleSet style;
        internal LinqBindingNode bindingNode;
        internal SelectorNode selectorNode;
        
        internal StructList<ElementAttribute> attributes;
        public UIView View { get; internal set; }
        public Vector2 scrollOffset { get; internal set; }
        public int depth { get; internal set; }
        public int siblingIndex { get; internal set; }
        
        // todo -- move this
        public ArrayContainer<StoredTemplate> storedTemplates;
        
        protected internal UIElement() {
            this.id = Application.NextElementId;
            this.style = new UIStyleSet(this);
            this.layoutResult = new LayoutResult();
            this.flags = UIElementFlags.Enabled | UIElementFlags.Alive;
            this.children = LightList<UIElement>.Get();
        }

        public Application Application => View.Application;

        public UIChildrenElement TranscludedChildren {
            get { return s_ColdDataMap.GetOrDefault(id).transcludedChildren; }
            internal set {
                ElementColdData coldData = s_ColdDataMap.GetOrDefault(id);
                coldData.transcludedChildren = value;
                s_ColdDataMap[id] = coldData;
            }
        }

        public UITemplate OriginTemplate {
            get { return s_ColdDataMap.GetOrDefault(id).templateRef; }
            internal set {
                ElementColdData coldData = s_ColdDataMap.GetOrDefault(id);
                coldData.templateRef = value;
                coldData.InitializeAttributes();
                s_ColdDataMap[id] = coldData;
            }
        }


        public IInputProvider Input => View.Application.InputSystem;

        public int ChildCount => children?.Count ?? 0;

        public bool isSelfEnabled => (flags & UIElementFlags.Enabled) != 0;

        public bool isSelfDisabled => (flags & UIElementFlags.Enabled) == 0;

        public bool isEnabled => (flags & UIElementFlags.Alive) == 0 && (flags & UIElementFlags.SelfAndAncestorEnabled) == UIElementFlags.SelfAndAncestorEnabled;

        public bool isDisabled => isDestroyed || (flags & UIElementFlags.Enabled) == 0 || (flags & UIElementFlags.AncestorEnabled) == 0;

        public bool hasDisabledAncestor => (flags & UIElementFlags.AncestorEnabled) == 0;

        public bool isDestroyed => (flags & UIElementFlags.Alive) == 0;

        public bool isBuiltIn => (flags & UIElementFlags.BuiltIn) != 0;

        internal bool isPrimitive => (flags & UIElementFlags.Primitive) != 0;

        public bool isCreated => (flags & UIElementFlags.Created) != 0;
        
        public virtual void OnCreate() { }
        
        public virtual void OnUpdate() { }

        public virtual void OnEnable() { }

        public virtual void OnDisable() { }

        public virtual void OnDestroy() { }

        public virtual void HandleUIEvent(UIEvent evt) { }

        public void Destroy() {
            View.Application.DoDestroyElement(this);
        }

        public UIElement InsertChild(uint idx, UIElement element) {
            throw new NotImplementedException();
//            if (element == null || element == this || element.isDestroyed) {
//                return null;
//            }
//
//            if (View == null) {
//                element.parent = this;
//                element.View = null;
//                element.siblingIndex = children.Count;
//                element.depth = depth + 1;
//                children.Insert((int) idx, element);
//            }
//            else {
//                Application.InsertChild(this, element, (uint) children.Count);
//            }
//
//            return element;
        }

        public UIElement AddChild(UIElement element) {
            throw new NotImplementedException();

//            // todo -- if <Children/> is defined in the template, attach child to that element instead
//            if (element == null || element == this || element.isDestroyed) {
//                return null;
//            }
//
//            if (View == null) {
//                element.parent = this;
//                element.View = null;
//                element.siblingIndex = children.Count;
//                element.depth = depth + 1;
//                children.Add(element);
//            }
//            else {
//                Application.InsertChild(this, element, (uint) children.Count);
//            }
//
//            return element;
        }

        public UIElement AddChild(in StoredTemplate storedTemplate) {
            throw new NotImplementedException();
           // return Application.InsertChild(this, storedTemplate, (uint)children.Count);
        }

        public void TriggerEvent(UIEvent evt) {
            evt.origin = this;
            UIElement ptr = this.parent;
            while (evt.IsPropagating() && ptr != null) {
                ptr.HandleUIEvent(evt);
                ptr = ptr.parent;
            }
        }

        public void SetEnabled(bool active) {
            if (View == null) {
                flags &= ~UIElementFlags.Enabled;
                return;
            }

            if (active && isSelfDisabled) {
                View.Application.DoEnableElement(this);
            }
            else if (!active && isSelfEnabled) {
                View.Application.DoDisableElement(this);
            }
        }

        public UIElement GetChild(int index) {
            if (children == null || (uint) index >= (uint) children.Count) {
                return null;
            }

            return children[index];
        }

        public UIElement FindById(string id) {
            return FindById<UIElement>(id);
        }

        [PublicAPI]
        public T FindById<T>(string id) where T : UIElement {
            if (isPrimitive || children == null) {
                return null;
            }

            for (int i = 0; i < children.Count; i++) {
                if (children[i].GetAttribute("id") == id) {
                    return children[i] as T;
                }

                UIElement childResult = children[i].FindById(id);

                if (childResult != null) {
                    return childResult as T;
                }
            }

            return null;
        }

        [PublicAPI]
        public T FindFirstByType<T>() where T : UIElement {
            if (isPrimitive || children == null) {
                return null;
            }

            for (int i = 0; i < children.Count; i++) {
                if (children[i] is T) {
                    return (T) children[i];
                }

                if (children[i] is UIChildrenElement) {
                    continue;
                }

                if (children[i]?.OriginTemplate is UIElementTemplate) {
                    continue;
                }

                UIElement childResult = children[i].FindFirstByType<T>();
                if (childResult != null) {
                    return (T) childResult;
                }
            }

            return null;
        }

        public List<T> FindByType<T>(List<T> retn = null) where T : UIElement {
            retn = retn ?? new List<T>();
            if (isPrimitive || children == null) {
                return retn;
            }

            for (int i = 0; i < children.Count; i++) {
                if (children[i] is T) {
                    retn.Add((T) children[i]);
                }


                if (children[i] is UIChildrenElement) {
                    continue;
                }

                if (children[i]?.OriginTemplate is UIElementTemplate) {
                    continue;
                }

                children[i].FindByType<T>(retn);
            }

            return retn;
        }

        public override string ToString() {
            if (HasAttribute("id")) {
                return "<" + GetDisplayName() + ":" + GetAttribute("id") + " " + id + ">";
            }

            if (style != null && style.HasBaseStyles) {
                return "<" + GetDisplayName() + ">"; // + style.BaseStyleNames;
            }
            else {
                return "<" + GetDisplayName() + " " + id + ">";
            }
        }

        public virtual string GetDisplayName() {
            return GetType().Name;
        }

        public List<ElementAttribute> GetAttributes(List<ElementAttribute> retn = null) {
            retn = retn ?? new List<ElementAttribute>();
            if (attributes == null || attributes.size == 0) {
                return retn;
            }

            for (int i = 0; i < attributes.size; i++) {
                retn.Add(attributes.array[i]);
            }

            return retn;
        }

        public void SetAttribute(string name, string value) {
            if (attributes == null) {
                attributes = StructList<ElementAttribute>.Get();
            }

            ElementAttribute[] attrs = attributes.array;
            int attrCount = attributes.size;
            
            for (int i = 0; i < attrCount; i++) {
                if (attrs[i].name == name) {
                    if (attrs[i].value == value) {
                        return;
                    }
                    else {
                        string oldValue = attrs[i].value;
                        attrs[i] = new ElementAttribute(name, value);
                        Application.OnAttributeSet(this, name, value, oldValue);
                        return;
                    }
                }    
            }
            
            attributes.Add(new ElementAttribute(name, value));
            Application.OnAttributeSet(this, name, value, null);
        }

        public bool TryGetAttribute(string key, out string value) {
            if (attributes == null) {
                value = null;
                return false;
            }

            ElementAttribute[] attrs = attributes.array;
            int attrCount = attributes.size;
            
            for (int i = 0; i < attrCount; i++) {
                if (attrs[i].name == key) {
                    value = attrs[i].value;
                    return true;
                }    
            }

            value = null;
            return false;
        }

        public string GetAttribute(string attr) {
            if (attributes == null) {
                return null;
            }

            ElementAttribute[] attrs = attributes.array;
            int attrCount = attributes.size;
            
            for (int i = 0; i < attrCount; i++) {
                if (attrs[i].name == attr) {
                    return attrs[i].value;
                }    
            }

            return null;
        }

        public bool HasAttribute(string name) {
            return GetAttribute(name) != null;
        }
        
        public int UniqueId => id;
        public IHierarchical Element => this;
        public IHierarchical Parent => parent;

        public List<UIElement> GetChildren(List<UIElement> retn = null) {
            retn = ListPool<UIElement>.Get();

            if (children == null) {
                return retn;
            }

            UIElement[] childArray = children.Array;
            for (int i = 0; i < children.Count; i++) {
                retn.Add(childArray[i]);
            }

            return retn;
        }

        internal void InternalDestroy() {
            ElementColdData coldData = s_ColdDataMap.GetOrDefault(id);
            coldData.Destroy();
            s_ColdDataMap.Remove(id);
            LightList<UIElement>.Release(ref children);
            parent = null;
        }

        public bool IsAncestorOf(UIElement potentialParent) {
            if (potentialParent == this || potentialParent == null) {
                return false;
            }

            UIElement ptr = this;
            while (ptr != null) {
                if (ptr.parent == potentialParent) {
                    return true;
                }

                ptr = ptr.parent;
            }

            return false;
        }

        
    }

}


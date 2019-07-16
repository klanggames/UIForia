using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Util;
using UnityEngine;
using LinqBinding = System.Action<UIForia.Elements.UIElement, UIForia.Elements.UIElement, UIForia.Util.StructStack<UIForia.Compilers.TemplateContextWrapper>>;

namespace UIForia.Systems {

    internal class LinqBindingNode {

        internal UIElement root;
        internal UIElement element;
        internal int iteratorIndex;
        internal LightList<LinqBindingNode> children;
        internal LightList<LinqBinding>.ListSpan bindings;

        internal int phase;

        internal LinqBinding enabledBinding;
        internal LinqBindingSystem system;

        internal TemplateContextWrapper contextWrapper;

        public LinqBindingNode() {
            this.children = new LightList<LinqBindingNode>(4);
        }

        public void AddChild(LinqBindingNode childNode) {
            children.Add(childNode);
        }

        public void SetContextProvider(TemplateContext context, int id) {
            contextWrapper = new TemplateContextWrapper(context, id);
        }

        public void Update(StructStack<TemplateContextWrapper> contextStack) {
            system.currentlyActive = this;

            enabledBinding?.Invoke(root, element, contextStack);

            if (!element.isEnabled) {
                return;
            }

            // todo -- might eventually support adding / removing bindings, will need to handle that here
            LinqBinding[] bindingArray = bindings.list.array;
            int bindingStart = bindings.start;
            int bindingsEnd = bindings.end;

            for (int i = bindingStart; i < bindingsEnd; i++) {
                bindingArray[i].Invoke(root, element, contextStack);
            }

            if (!element.isEnabled) {
                return;
            }

            iteratorIndex = 0;

            int activePhase = system.currentPhase;

            // todo if doing an out of order binding run we need to be sure the context stack is saved / restored 
            
            if (contextWrapper.context != null) {
                contextStack.Push(contextWrapper);
            }

            while (iteratorIndex < children.Count) {
                LinqBindingNode child = children.Array[iteratorIndex++];

                if (child.phase != activePhase) {
                    child.Update(contextStack);
                    system.currentlyActive = this;
                }
            }

            if (contextWrapper.context != null) {
                contextStack.Pop();
            }

            iteratorIndex = -1;
        }

        public void InsertChild(LinqBindingNode bindingNode) {
            Debug.Assert(bindingNode != null, "bindingNode != null");
            // iterate children until index found where traversal index < element.traversal index (basically use depth comparer)
            // todo insert from correct index, not at the end
            int index = children.Count;

            bindingNode.phase = system.previousPhase;
            children.Add(bindingNode);
            if (iteratorIndex >= 0 && iteratorIndex > index) {
                iteratorIndex = index;
            }
        }

        public void InsertChildren(LightList<LinqBindingNode> childrenToAdd) {
            // children will already be in the correct order, so index is the first index
            // todo insert from correct index, not at the end
            int index = children.Count;

            for (int i = 0; i < childrenToAdd.Count; i++) {
                childrenToAdd[i].phase = system.previousPhase;
                children.Add(childrenToAdd[i]);
            }

            if (iteratorIndex >= 0 && iteratorIndex > index) {
                iteratorIndex = index;
            }
        }

        public void RemoveChild(int index) {
            children.RemoveAt(index);
            if (iteratorIndex >= 0 && iteratorIndex >= index) {
                iteratorIndex = index;
            }
        }

        public static LinqBindingNode Get(Application application, UIElement element, UIElement rootElement) {
            LinqBindingNode node = new LinqBindingNode();
            node.root = rootElement;
            node.element = element;
            node.system = application.LinqBindingSystem;
            node.phase = node.system.previousPhase;
            node.iteratorIndex = -1;
            node.bindings = default;
            return node;
        }

    }

}
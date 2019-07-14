using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Util;

namespace UIForia.Systems {

    public class LinqBindingSystem : ISystem {

        internal int currentPhase;
        internal int previousPhase;
        internal LinqBindingNode currentlyActive;
        internal StructStack<TemplateContextWrapper> contextStack;
        
        private readonly LinqBindingNode rootNode;

        public LinqBindingSystem() {
            this.contextStack = new StructStack<TemplateContextWrapper>();
            this.currentPhase = 0;
            this.previousPhase = -1;
            this.rootNode = new LinqBindingNode();
        }
        
        public void OnReset() { }

        public void OnUpdate() {
            rootNode.Update(contextStack);
        }

        public void OnDestroy() { }

        public void OnViewAdded(UIView view) { }

        public void OnViewRemoved(UIView view) { }

        public void OnElementCreated(UIElement element) {
            
            if (element.bindingNode != null) {
                // binding node might be empty. if it is, add the children instead
                if (element.bindingNode.bindings.list != null) {
                    GetBindingNode(element.parent).InsertChild(element.bindingNode);
                }
                else {
                    GetBindingNode(element.parent).InsertChildren(element.bindingNode.children);
                }
            }
            
        }

        private LinqBindingNode GetBindingNode(UIElement element) {
            if (element.bindingNode != null) {
                return element.bindingNode;
            }

            UIElement ptr = element.parent;
            
            while (ptr != null) {
                if (ptr.bindingNode != null) {
                    return ptr.bindingNode;
                }
                ptr = ptr.parent;
            }

            return rootNode;
        }
        
        public void OnElementEnabled(UIElement element) { }

        public void OnElementDisabled(UIElement element) { }

        public void OnElementDestroyed(UIElement element) { }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string previousValue) { }


    }

}
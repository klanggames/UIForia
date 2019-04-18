﻿using System;
using UIForia.Bindings;
using UIForia.Elements;
using UIForia.Expressions;
using UIForia.Templates;
using UIForia.Util;

namespace UIForia.Systems {

    public class BindingSystem : ISystem {

        private readonly SkipTree<BindingNode> m_ReadBindingTree;
        private readonly SkipTree<BindingNode> m_WriteBindingTree;

        public BindingSystem() {
            this.m_ReadBindingTree = new SkipTree<BindingNode>();
            this.m_WriteBindingTree = new SkipTree<BindingNode>();
        }

        public void OnReset() {
            m_ReadBindingTree.Clear();
            m_WriteBindingTree.Clear();
        }

        public void OnUpdate() {
            m_ReadBindingTree.TraversePreOrder((node) => { node.OnUpdate(); });
        }

        public void OnLateUpdate() {
            m_WriteBindingTree.TraversePreOrder((node) => { node.OnUpdate(); });
        }

        public void OnDestroy() { }

        public void OnViewAdded(UIView view) { }

        public void OnViewRemoved(UIView view) { }

        public void OnElementCreated(UIElement element) {
            UITemplate template = element.OriginTemplate;

            for (int i = 0; i < template.triggeredBindings.Length; i++) {
                if (template.triggeredBindings[i].bindingType == BindingType.Constant) {
                    template.triggeredBindings[i].Execute(element, element.templateContext);
                }
            }

            if (element is UIRepeatElement repeat) {
                ReflectionUtil.TypeArray2[0] = repeat.listType;
                ReflectionUtil.TypeArray2[1] = repeat.itemType;

                ReflectionUtil.ObjectArray2[0] = repeat;
                ReflectionUtil.ObjectArray2[1] = repeat.listExpression;

                RepeatBindingNode node = (RepeatBindingNode) ReflectionUtil.CreateGenericInstanceFromOpenType(
                    typeof(RepeatBindingNode<,>),
                    ReflectionUtil.TypeArray2,
                    ReflectionUtil.ObjectArray2
                );

                node.bindings = template.perFrameBindings;
                node.element = repeat;
                node.template = repeat.template;
                node.context = repeat.templateContext;
                m_ReadBindingTree.AddItem(node);
            }
            else if (element is RenderBlockElement renderBlock) {
                // find the binding for the render block id
                // if binding is constant we don't need to register a binding node unless 
                // if binding is dynamic we need to run a binding that will replace the block with what it should be
                throw new NotImplementedException("<RenderBlock> is not yet supported, need to figure out how to handle bindings");
//                RenderBlockIdBinding node = new RenderBlockIdBinding();
//                node.bindings = template.perFrameBindings;
//                node.element = renderBlock;
//                node.context = element.templateContext;
//                m_ReadBindingTree.AddItem(node);
            }
            else {
                if (template.perFrameBindings.Length > 0) {
                    BindingNode node = new BindingNode();
                    node.bindings = template.perFrameBindings;
                    node.element = element;
                    node.context = element.templateContext;
                    m_ReadBindingTree.AddItem(node);
                }

                if (template.writeBindings != null && template.writeBindings.Length > 0) {
                    BindingNode node = new BindingNode();
                    node.bindings = template.writeBindings;
                    node.element = element;
                    node.context = element.templateContext;
                    m_WriteBindingTree.AddItem(node);
                }
                
                if (element.children != null) {
                    for (int i = 0; i < element.children.Count; i++) {
                        OnElementCreated(element.children[i]);
                    }
                }
            }
        }

        public void OnElementDestroyed(UIElement element) {
            m_ReadBindingTree.RemoveHierarchy(element);
            m_WriteBindingTree.RemoveHierarchy(element);
        }

        public void OnElementEnabled(UIElement element) {
            
        }

        public void OnElementDisabled(UIElement element) { }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string attributeValue) { }

        // todo -- expose to editor but not user
        public SkipTree<BindingNode> GetReadTree() {
            return m_ReadBindingTree;
        }
    }

}
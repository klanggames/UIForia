using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UIForia.Util;

namespace UIForia.Elements {

    public struct ElementPool2 {

        public StructList<DepthKeyedElement> activeElements;
        public LightList<UIElement> pooledElements;

    }

    public struct DepthKeyedElement {

        public int depth;
        public UIElement element;

    }

    public class ElementPool {

        public Dictionary<Type, LightList<UIElement>> poolMap = new Dictionary<Type, LightList<UIElement>>();
        public StructList<DepthKeyedElement> activeElements;

        // traversal index could be super helpful here but would dereference of elements to use it. profile this
        
        public void FindDescendents(UIElement element, LightList<UIElement> output) {
            int depth = element.depth;
            int count = activeElements.size;
            DepthKeyedElement[] active = activeElements.array;

            for (int i = 0; i < count; i++) {
                if (active[i].depth <= depth) {
                    continue;
                }

                UIElement ptr = active[i].element;
                while (ptr != null && ptr.depth < depth) {
                    if (ptr == element) {
                        output.Add(active[i].element);
                        break;
                    }

                    ptr = ptr.parent;
                }
            }
        }

        public UIElement Get(Type type, int depth) {
            if (poolMap.TryGetValue(type, out LightList<UIElement> elements)) {
                if (elements.Count > 0) {
                    return elements.RemoveLast();
                }

                return (UIElement) FormatterServices.GetUninitializedObject(type);
            }
            else {
                return (UIElement) FormatterServices.GetUninitializedObject(type);
            }
        }

        public void Release(UIElement element) {
            Type type = element.GetType();

            if (poolMap.TryGetValue(type, out LightList<UIElement> elements)) {
                elements.Add(element);
            }
            else {
                LightList<UIElement> list = new LightList<UIElement>();
                list.Add(element);
                poolMap[type] = list;
            }
        }

    }

}
using System;
using UIForia.Parsing.Expression;
using UIForia.Util;

namespace UIForia.Compilers {

    public class TemplateNode {

        public LightList<DirectiveDefinition> directives;
        public StructList<AttributeDefinition2> attributes;
        public LightList<TemplateNode> children;
        public TemplateAST astRoot;
        public TemplateNode parent;
        public LightList<string> textContent;
        public ProcessedType processedType;
        public string slotName;

        [ThreadStatic] private static LightList<TemplateNode> s_Pool;

        public TemplateNode() {
            this.directives = new LightList<DirectiveDefinition>(4);
            this.attributes = new StructList<AttributeDefinition2>();
            this.children = new LightList<TemplateNode>();
        }

        internal static TemplateNode Get() {
            if (s_Pool == null) {
                s_Pool = new LightList<TemplateNode>(32);
            }

            if (s_Pool.Count != 0) {
                return s_Pool.RemoveLast();
            }

            return new TemplateNode();
        }


        internal static void Release(ref TemplateNode node) {
            if (s_Pool == null) {
                s_Pool = new LightList<TemplateNode>(32);
            }

            node.processedType = default;
            node.directives.Clear();
            node.attributes.Clear();
            node.children.Clear();
            node.parent = null;
            node.textContent = null;
            s_Pool.Add(node);
            node = null;
        }

        public bool HasAttribute(string attrName) {
            if (attributes == null) return false;
            for (int i = 0; i < attributes.Count; i++) {
                if (attributes[i].key == attrName) {
                    return true;
                }
            }

            return false;
        }

        public string GetStringContent() {
            string retn = "";
            if (textContent == null) return retn;
            
            for (int i = 0; i < textContent.Count; i++) {
                retn += textContent[i];
            }

            return retn;
        }

    }

}
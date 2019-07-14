using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Parsing.Expression;
using UIForia.Templates;
using UIForia.Util;

namespace UIForia.Compilers {

    // namespaced elements
    // using declarations

    // <Using namespace=""/>
    // <Slot:Name>
    // <SlotContent:Name>
    // <Layout:Element>
    // <Transclude:
    // <Slot:Children>
    // <Dynamic:ElementType type="" data="">
    // <Repeat:
    // <LazyLoad:
    // <Virtual:
    // <NameSpace.Whatever.Element>
    // <Const
    // <RecursiveConst: 
    // <ConstTree
    // <Shadow:
    // elements can start & end with : so we can have anonymous elements


    public class XMLTemplateParser {

        public Application application;

        private readonly XmlParserContext parserContext;

        [ThreadStatic] private static string[] s_NamespaceLookup;

        private readonly string[] s_Directives = {
            "DefineSlot",
            "Slot",
            "LazyLoad",
            "Dynamic",
            "Virtual",
            "Const",
            "ConstRecursive",
            "Shadow",
            "Repeat",
        };

        public XMLTemplateParser(Application application) {
            this.application = application;
            XmlNamespaceManager nameSpaceManager = new XmlNamespaceManager(new NameTable());
            nameSpaceManager.AddNamespace("attr", "attr");
            nameSpaceManager.AddNamespace("evt", "evt");
            nameSpaceManager.AddNamespace("style", "style");
            nameSpaceManager.AddNamespace("ctx", "ctx");
            nameSpaceManager.AddNamespace("ctxvar", "ctxvar");
            for (int i = 0; i < s_Directives.Length; i++) {
                nameSpaceManager.AddNamespace(s_Directives[i], s_Directives[i]);
            }

            this.parserContext = new XmlParserContext(null, nameSpaceManager, null, XmlSpace.None);
        }

        internal TemplateAST Parse(ProcessedType processedType) {
            
            string template = processedType.GetTemplateFromApplication(application);
            
            XElement root = XElement.Load(new XmlTextReader(template, XmlNodeType.Element, parserContext));

            root.MergeTextNodes();

            IEnumerable<XElement> styleElements = root.GetChildren("Style");
            IEnumerable<XElement> usingElements = root.GetChildren("Using");
            IEnumerable<XElement> contentElements = root.GetChildren("Content");

            TemplateAST retn = new TemplateAST();

            StructList<UsingDeclaration> usings = StructList<UsingDeclaration>.Get();
            StructList<StyleDefinition> styles = StructList<StyleDefinition>.Get();

            LightList<string> namespaces = LightList<string>.Get();

            foreach (XElement usingElement in usingElements) {
                usings.Add(ParseUsing(usingElement));
            }

            for (int i = 0; i < usings.Count; i++) {
                namespaces.Add(usings[i].namespaceName);
            }

            foreach (XElement styleElement in styleElements) {
                styles.Add(ParseStyleSheet("someId", styleElement));
            }

            if (contentElements.Count() != 1) { }

            XElement contentElement = contentElements.First();

            TemplateNode rootNode = TemplateNode.Get();
            rootNode.astRoot = retn;

            rootNode.processedType = processedType;

            ParseAttributes(rootNode, contentElement);
            ParseChildren(rootNode, contentElement.Nodes(), namespaces);

            retn.fileName = processedType.GetTemplatePath();
            retn.root = rootNode;
            retn.usings = usings;
            retn.styles = styles;
            LightList<string>.Release(ref namespaces);
            //retn.extends = contentElement.GetAttribute("x-inherited") != null || contentElement.GetAttribute("attr:inherited") != null;
            return retn;
        }

        private static readonly char[] s_DotArray = {'.'};

        private static void ParseElementTag(TemplateNode templateNode, XElement element, LightList<string> namespaces) {
            string directives = element.Name.Namespace.NamespaceName;
            string tagName = element.Name.LocalName;

            if (directives.Contains('.')) {
                string[] directiveList = directives.Split(s_DotArray, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < directiveList.Length; i++) {
                    templateNode.directives.Add(new DirectiveDefinition(directiveList[i]));
                }
            }
            else if (!string.IsNullOrWhiteSpace(directives) && !string.IsNullOrEmpty(directives)) {
                templateNode.directives.Add(new DirectiveDefinition(directives));
            }

            if (directives.Contains("DefineSlot")) {
                templateNode.slotName = element.Name.LocalName;
                templateNode.processedType = TypeProcessor.GetProcessedType(typeof(UISlotDefinition));
                return;
            }

            if (directives.Contains("Slot")) {
                templateNode.slotName = element.Name.LocalName;
                templateNode.processedType = TypeProcessor.GetProcessedType(typeof(UISlotContent));
                return;
            }

            int lastIdx = tagName.LastIndexOf('.');

            if (lastIdx > 0) {
                s_NamespaceLookup = s_NamespaceLookup ?? new string[1];
                s_NamespaceLookup[0] = tagName.Substring(0, lastIdx);
                templateNode.processedType = TypeProcessor.ResolveTagName(tagName.Substring(lastIdx), s_NamespaceLookup);
            }
            else {
                templateNode.processedType = TypeProcessor.ResolveTagName(tagName, namespaces);
            }

            if (templateNode.processedType.rawType == null) {
                throw new Exception("Unresolved tag name: " + element.Name.LocalName);
            }
        }

        private static void ParseAttributes(TemplateNode templateNode, XElement node) {
            foreach (XAttribute attr in node.Attributes()) {
                string prefix = attr.Name.NamespaceName;
                string name = attr.Name.LocalName.Trim();

                int line = ((IXmlLineInfo) attr).LineNumber;
                int column = ((IXmlLineInfo) attr).LinePosition;

                AttributeType attributeType = AttributeType.Property;

                if (prefix == string.Empty) {
                    if (attr.Name.LocalName.StartsWith("style.")) {
                        attributeType = AttributeType.Style;
                        name = attr.Name.LocalName.Substring("style.".Length);
                    }

                    if (attr.Name.LocalName.StartsWith("x-")) {
                        attributeType = AttributeType.Attribute;
                        name = attr.Name.LocalName.Substring("x-.".Length);
                    }
                }
                else {
                    switch (prefix) {
                        case "attr":
                            attributeType = AttributeType.Attribute;
                            break;
                        case "style":
                            attributeType = AttributeType.Style;
                            break;
                        case "evt":
                            attributeType = AttributeType.Event;
                            break;
                        case "ctx":
                            attributeType = AttributeType.Context;
                            break;
                        case "ctxvar":
                            attributeType = AttributeType.ContextVariable;
                            break;
                        case "alias":
                            attributeType = AttributeType.Alias;
                            break;
                        
                        default:
                            throw new ArgumentOutOfRangeException("Unknown attribute prefix: " + prefix);
                    }
                }

                // todo -- set flag properly
                templateNode.attributes.Add(new AttributeDefinition2(attributeType, 0, name, attr.Value.Trim(), line, column));
            }
        }

        private static void ParseChildren(TemplateNode parent, IEnumerable<XNode> nodes, LightList<string> namespaces) {
            foreach (XNode node in nodes) {
                switch (node.NodeType) {
                    case XmlNodeType.Text: {
                        XText textNode = (XText) node;
                        if (string.IsNullOrWhiteSpace(textNode.Value)) {
                            continue;
                        }

                        if (typeof(UITextElement).IsAssignableFrom(parent.processedType.rawType)) {
                            parent.textContent += textNode.Value;
                            continue;
                        }

                        TemplateNode templateNode = TemplateNode.Get();
                        templateNode.parent = parent;
                        templateNode.astRoot = parent.astRoot;
                        templateNode.processedType = TypeProcessor.GetProcessedType(typeof(UITextElement));
                        templateNode.textContent = textNode.Value;
                        parent.children.Add(templateNode);
                        templateNode = TemplateNode.Get();
                        continue;
                    }

                    case XmlNodeType.Element: {
                        XElement element = (XElement) node;
                        TemplateNode templateNode = TemplateNode.Get();
                        templateNode.parent = parent;
                        templateNode.astRoot = parent.astRoot;

                        ParseElementTag(templateNode, element, namespaces);

                        ParseAttributes(templateNode, element);

                        if (!parent.processedType.requiresTemplateExpansion && templateNode.processedType == typeof(UISlotContent)) {
                            throw new TemplateParseException(node, $"Slot cannot be added for type {parent.processedType.rawType} because it is a text or container type and does not accept slots.");
                        }

                        parent.children.Add(templateNode);

                        ParseChildren(templateNode, element.Nodes(), namespaces);

                        templateNode = TemplateNode.Get();
                        continue;
                    }

                    case XmlNodeType.Comment:
                        continue;
                }

                throw new TemplateParseException(node, $"Unable to handle node type: {node.NodeType}");
            }

            if (parent.parent != null && parent.processedType.requiresTemplateExpansion && parent.children.Count > 0) {
                TemplateNode childrenSlotNode = TemplateNode.Get();
                for (int i = 0; i < parent.children.size; i++) {
                    Type type = parent.children[i].processedType.rawType;

                    if (type != typeof(UISlotContent)) {
                        childrenSlotNode.children.Add(parent.children[i]);
                        parent.children.RemoveAt(i--);
                    }
                }

                if (childrenSlotNode.children.Count > 0) {
                    childrenSlotNode.astRoot = parent.astRoot;
                    childrenSlotNode.parent = parent;
                    childrenSlotNode.processedType = TypeProcessor.GetProcessedType(typeof(UIChildrenElement));
                    childrenSlotNode.slotName = "Children";
                    childrenSlotNode.directives.Add(new DirectiveDefinition("Slot"));
                    parent.children.Add(childrenSlotNode);
                }
                else {
                    TemplateNode.Release(ref childrenSlotNode);
                }
            }
        }

        private UsingDeclaration ParseUsing(XElement element) {
            XAttribute namespaceAttr = element.GetAttribute("namespace");
            if (namespaceAttr == null) {
                throw new TemplateParseException(element, "<Using/> tags require a 'namespace' attribute");
            }

            string value = namespaceAttr.Value.Trim();
            if (string.IsNullOrEmpty(value)) {
                throw new TemplateParseException(element, "<Using/> tags require a 'namespace' attribute with a value");
            }

            return new UsingDeclaration() {
                namespaceName = value
            };
        }

        private StyleDefinition ParseStyleSheet(string templateId, XElement styleElement) {
            XAttribute aliasAttr = styleElement.GetAttribute("alias");
            XAttribute importPathAttr = styleElement.GetAttribute("path") ?? styleElement.GetAttribute("src");

            string rawText = string.Empty;
            // styles can have either a class path or a body
            foreach (XNode node in styleElement.Nodes()) {
                switch (node.NodeType) {
                    case XmlNodeType.Text:
                        rawText += ((XText) node).Value;
                        continue;

                    case XmlNodeType.Element:
                        throw new TemplateParseException(node, "<Style> can only have text children, no elements");

                    case XmlNodeType.Comment:
                        continue;
                }

                throw new TemplateParseException(node, $"Unable to handle node type: {node.NodeType}");
            }

            string alias = StyleDefinition.k_EmptyAliasName;

            if (aliasAttr != null && !string.IsNullOrEmpty(aliasAttr.Value)) {
                alias = aliasAttr.Value.Trim();
            }

            // if we have a body, expect import path to be null
            if (!string.IsNullOrEmpty(rawText) && !string.IsNullOrWhiteSpace(rawText)) {
                if (importPathAttr != null && !string.IsNullOrEmpty(importPathAttr.Value)) {
                    throw new TemplateParseException(styleElement, "Expected 'path' or 'src' to be null when a body is provided to a style tag");
                }

                return new StyleDefinition(alias, templateId, rawText);
            }

            // if we have no body then expect path to be set
            if (importPathAttr == null || string.IsNullOrEmpty(importPathAttr.Value)) {
                throw new TemplateParseException(styleElement, "Expected 'path' or 'src' to be provided when a body is not provided in a style tag");
            }

            return new StyleDefinition(alias, importPathAttr.Value.Trim());
        }

    }

}
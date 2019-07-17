using System;
using System.Collections.Generic;
using UIForia.Compilers;
using UIForia.Compilers.ExpressionResolvers;
using UIForia.Compilers.Style;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Parsing.Expression;
using UIForia.Rendering;
using UIForia.Util;

namespace UIForia.Templates {
    
    /// <summary>
    /// This represents the result of a parsed UITemplate. Invoking 'Create()' will create an instance of the
    /// template that was parsed. 
    /// </summary>
    public class ParsedTemplate {

        private bool isCompiled;

        // todo: write custom comparer 
        internal Dictionary<string, AliasedUIStyleGroupContainer> sharedStyleMap;
        internal Dictionary<string, UIStyleGroupContainer> implicitStyleMap;
        private UIStyleGroupContainer implicitRootStyle;

        public readonly List<string> usings;
        private readonly List<UISlotContentTemplate> inheritedContent;
        private readonly List<StyleDefinition> styleDefinitions;
        public readonly UIElementTemplate rootElementTemplate;
        public readonly ParsedTemplate baseTemplate;
        public readonly Application app;
        public readonly string templatePath;
        
        public readonly ExpressionCompiler compiler = new ExpressionCompiler(true);

        public static readonly ExpressionAliasResolver s_ElementResolver = new ElementResolver("element");
        public static readonly ExpressionAliasResolver s_ParentResolver = new ParentElementResolver("parent");
        public static readonly ExpressionAliasResolver s_RouteResolver = new RouteResolver("route");
        
        // todo -- I don't think these are used
        public static readonly ExpressionAliasResolver s_ContentSizeResolver = new ContentSizeResolver();
        public static readonly ExpressionAliasResolver s_UrlResolver = new UrlResolver("$url");
        public static readonly ExpressionAliasResolver s_RgbResolver = new ColorResolver("$rgb");
        public static readonly ExpressionAliasResolver s_SizeResolver = new SizeResolver("$size");
        public static readonly ExpressionAliasResolver s_FixedLengthResolver = new LengthResolver("$fixedLength");

        static ParsedTemplate() {
          // todo -- try to make compiler shared or at least pooled, will need to manage using and import contexts
        }
       
        public ParsedTemplate(Application app, Type type, string templatePath, List<UITemplate> contents, List<AttributeDefinition> attributes, List<string> usings, List<StyleDefinition> styleDefinitions, List<ImportDeclaration> imports) : this(null, type, usings, null, styleDefinitions, imports) {
            this.app = app;
            this.templatePath = templatePath;
            this.rootElementTemplate = new UIElementTemplate(app, type, contents, attributes);
        }

        public ParsedTemplate(ParsedTemplate baseTemplate, Type type, List<string> usings, List<UISlotContentTemplate> contentTemplates, List<StyleDefinition> styleDefinitions, List<ImportDeclaration> imports) {
            this.baseTemplate = baseTemplate;
            this.RootType = type;
            this.rootElementTemplate = null;
            this.usings = usings;
            this.inheritedContent = contentTemplates;
            this.styleDefinitions = styleDefinitions;
            this.Imports = imports;
            ValidateStyleDefinitions();
            compiler.AddNamespace("UIForia.Rendering");
            compiler.AddNamespace("UIForia.Layout");
            compiler.AddNamespace("UIForia");

            compiler.AddAliasResolver(s_ElementResolver);
            compiler.AddAliasResolver(s_ParentResolver);
            compiler.AddAliasResolver(s_RouteResolver);
            compiler.AddAliasResolver(s_ContentSizeResolver);
            compiler.AddAliasResolver(s_UrlResolver);
            compiler.AddAliasResolver(s_RgbResolver);
            compiler.AddAliasResolver(s_SizeResolver);
            compiler.AddAliasResolver(s_FixedLengthResolver);
        }

        public List<UITemplate> childTemplates => rootElementTemplate.childTemplates;

        public Type RootType { get; }
        public List<ImportDeclaration> Imports { get; }

        public UIElement Create() {
            Compile();
            UIElement retn = null;
            if (baseTemplate == null) {
                retn = rootElementTemplate.CreateUnscoped();
            }
            else {
                retn = baseTemplate.rootElementTemplate.CreateUnscoped(RootType, inheritedContent);
            }

//            LightStack<UIElement> stack = LightStack<UIElement>.Get();
//            stack.Push(retn);
//            while (stack.Count > 0) {
//                UIElement current = stack.PopUnchecked();
//                
//                current.style.Initialize();
//                
//                int childCount = current.children.Count;
//                UIElemNent[] children = current.children.Array;
//                for (int i = 0; i < childCount; i++) {
//                    stack.Push(children[i]);
//                }
//            }
//            LightStack<UIElement>.Release(ref stack);
            return retn;
        }

        public void Compile() {
            if (isCompiled) return;
            isCompiled = true;

            CompileStyles();
            
            //todo  might cause problems w/ nested usings
            compiler.AddNamespaces(usings);

            if (baseTemplate != null) {
                baseTemplate.Compile();
                for (int i = 0; i < inheritedContent.Count; i++) {
                    CompileStep(inheritedContent[i]);
                }

                return;
            }

            CompileStep(rootElementTemplate);
            if (rootElementTemplate != null && implicitRootStyle != null) {
                Array.Resize(ref rootElementTemplate.baseStyles, rootElementTemplate.baseStyles.Length + 1);
                rootElementTemplate.baseStyles[rootElementTemplate.baseStyles.Length - 1] = implicitRootStyle;
            }
            
            compiler.RemoveNamespaces(usings);

        }

        private void CompileStyles() {
            if (styleDefinitions == null || styleDefinitions.Count == 0) {
                return;
            }

            sharedStyleMap = new Dictionary<string, AliasedUIStyleGroupContainer>();

            for (int i = 0; i < styleDefinitions.Count; i++) {
                StyleSheet sheet = null;
                if (styleDefinitions[i].body != null) {
                    sheet = app.styleImporter.ImportStyleSheetFromString(templatePath, styleDefinitions[i].body);
                }
                else if (styleDefinitions[i].importPath != null) {
                    sheet = app.styleImporter.ImportStyleSheetFromFile(styleDefinitions[i].importPath);
                }

                if (sheet != null) {
                    string alias = styleDefinitions[i].alias;

                    for (int j = 0; j < sheet.styleGroupContainers.Length; j++) {
                        UIStyleGroupContainer container = sheet.styleGroupContainers[j];

                        if (container.styleType == StyleType.Implicit) {
                            // this lets us style the root element in a template implicitly
                            // this should be improved with better style system support for default & important styles
                            if (container.name == "this") {
                                implicitRootStyle = container;  
                                continue;
                            }
                            // we only take the first implicit style. This could be improved by doing a merge of some sort
                            implicitStyleMap = implicitStyleMap ?? new Dictionary<string, UIStyleGroupContainer>();
                            if (!implicitStyleMap.ContainsKey(container.name)) {
                                implicitStyleMap.Add(container.name, container);
                            }
                            continue;
                        }
                           
                        if (alias == null) {
                            sharedStyleMap[container.name] = new AliasedUIStyleGroupContainer() {
                                alias = null,
                                container = container
                            };
                        }
                        else {
                            sharedStyleMap[alias + "." + container.name ] = new AliasedUIStyleGroupContainer() {
                                alias = alias,
                                container = container
                            };
                            if (!sharedStyleMap.ContainsKey(container.name)) {
                                sharedStyleMap[container.name] = new AliasedUIStyleGroupContainer() {
                                    alias = alias,
                                    container = container
                                };
                            }
                        }
                    }
                }
            }
        }

        private void CompileStep(UITemplate template) {
            template.SourceTemplate = this;
            template.Compile(this);

            if (template.childTemplates != null) {
                for (int i = 0; i < template.childTemplates.Count; i++) {
                    CompileStep(template.childTemplates[i]);
                }
            }

            template.PostCompile(this);
        }

        private void ValidateStyleDefinitions() {
            if (styleDefinitions == null) return;
            for (int i = 0; i < styleDefinitions.Count; i++) {
                StyleDefinition current = styleDefinitions[i];
                for (int j = 0; j < styleDefinitions.Count; j++) {
                    if (j == i) {
                        continue;
                    }

                    if (styleDefinitions[j].alias == current.alias) {
                        if (current.alias == StyleDefinition.k_EmptyAliasName) {
                            throw new ParseException(templatePath, "You cannot provide multiple style tags with a default alias");
                        }

                        throw new ParseException(templatePath, "Duplicate style alias: " + current.alias);
                    }
                }
            }
        }

        public ParsedTemplate CreateInherited(Type inheritedType, List<string> usings, List<UISlotContentTemplate> contents, List<StyleDefinition> styleDefinitions, List<ImportDeclaration> importDeclarations) {
            return new ParsedTemplate(this, inheritedType, usings, contents, styleDefinitions, importDeclarations);
        }

        internal UIStyleGroupContainer GetImplicitStyle(string tagName) {
            if (implicitStyleMap == null) return null;
            implicitStyleMap.TryGetValue(tagName, out UIStyleGroupContainer retn);
            return retn;
        }

        internal UIStyleGroupContainer GetSharedStyle(string styleName) {
            if (sharedStyleMap == null) return null;
            sharedStyleMap.TryGetValue(styleName, out AliasedUIStyleGroupContainer aliasedUiStyleGroupContainer);
            return aliasedUiStyleGroupContainer.container;
        }

        public struct AliasedUIStyleGroupContainer {
            // alias will be used by the inspector
            public string alias;
            public UIStyleGroupContainer container;
        }
    }

}
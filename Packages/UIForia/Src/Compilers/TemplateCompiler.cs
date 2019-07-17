using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Mono.Linq.Expressions;
using UIForia.Compilers.ExpressionResolvers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Parsing.Expression;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;
using LinqBinding = System.Action<UIForia.Elements.UIElement, UIForia.Elements.UIElement, UIForia.Util.StructStack<UIForia.Compilers.TemplateContextWrapper>>;

namespace UIForia.Compilers {

    public class TemplateCompiler {

        public Application application;
        private int varId;
        private readonly LinqCompiler linqCompiler;

        private readonly LinqStyleCompiler styleCompiler;
        private readonly LinqPropertyCompiler propertyCompiler;
        private readonly LightStack<Type> compilationStack;
        private readonly Dictionary<Type, CompiledTemplate> templateMap;
        private readonly XMLTemplateParser xmlTemplateParser;

        private static readonly MethodInfo s_CreateFromPool = typeof(Application).GetMethod("CreateElementFromPool");
        private static readonly MethodInfo s_BindingNodePool_Get = typeof(LinqBindingNode).GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo s_GetStructList_ElementAttr = typeof(StructList<ElementAttribute>).GetMethod(nameof(StructList<ElementAttribute>.GetMinSize), new[] {typeof(int)});
        private static readonly FieldInfo s_StructList_ElementAttr_Size = typeof(StructList<ElementAttribute>).GetField("size");
        private static readonly FieldInfo s_StructList_ElementAttr_Array = typeof(StructList<ElementAttribute>).GetField("array");
        private static readonly FieldInfo s_Scope_ApplicationField = typeof(TemplateScope2).GetField("application");
        private static readonly FieldInfo s_ScopeBindingNodeField = typeof(TemplateScope2).GetField(nameof(TemplateScope2.bindingNode));
        private static readonly FieldInfo s_Scope_CompiledTemplate = typeof(TemplateScope2).GetField(nameof(TemplateScope2.compiledTemplate));
        private static readonly FieldInfo s_Element_Parent = typeof(UIElement).GetField(nameof(UIElement.parent), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo s_Element_Application = typeof(UIElement).GetProperty(nameof(UIElement.Application), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly ConstructorInfo s_ElementAttributeCtor = typeof(ElementAttribute).GetConstructor(new[] {typeof(string), typeof(string)});
        private static readonly ConstructorInfo s_TemplateScope_Ctor = typeof(TemplateScope2).GetConstructor(new[] {typeof(Application), typeof(LinqBindingNode), typeof(StructList<SlotUsage>)});
        private static readonly FieldInfo s_TemplateScope_SlotInputList = typeof(TemplateScope2).GetField(nameof(TemplateScope2.slotInputs));
        private static readonly ConstructorInfo s_LexicalScope_Ctor = typeof(LexicalScope).GetConstructor(new[] {typeof(UIElement), typeof(CompiledTemplate)});
        private static readonly FieldInfo s_StructList_SlotUsage_Array = typeof(StructList<SlotUsage>).GetField(nameof(StructList<SlotUsage>.array));

        private static readonly FieldInfo s_ElementAttributeList = typeof(UIElement).GetField("attributes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo s_Element_ChildrenList = typeof(UIElement).GetField(nameof(UIElement.children), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_LightList_Element_Array = typeof(LightList<UIElement>).GetField(nameof(LightList<UIElement>.array), BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo s_Application_HydrateTemplate = typeof(Application).GetMethod(nameof(Application.HydrateTemplate), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_TextElement_Text = typeof(UITextElement).GetField(nameof(UITextElement.text), BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo s_Application_CreateSlot = typeof(Application).GetMethod(nameof(Application.CreateSlot), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_Application_TemplateData = typeof(Application).GetField(nameof(Application.templateData), BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo s_TemplateData_ContextProviderFns = typeof(TemplateData).GetField(nameof(TemplateData.contextProviderFns), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo s_TemplateData_SharedBindingList = typeof(TemplateData).GetField(nameof(TemplateData.sharedBindingFns), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        private static readonly MethodInfo s_BindingNode_AddChild = typeof(LinqBindingNode).GetMethod(nameof(LinqBindingNode.AddChild), BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo s_BindingNode_SetContextProvider = typeof(LinqBindingNode).GetMethod(nameof(LinqBindingNode.SetContextProvider), BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo s_BindingNode_BindingList = typeof(LinqBindingNode).GetField(nameof(LinqBindingNode.bindings), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly ConstructorInfo s_LightList_LinqBinding_SpanCtor = typeof(LightList<LinqBinding>.ListSpan).GetConstructor(new[] {typeof(LightList<LinqBinding>), typeof(int), typeof(int)});
        private static readonly MethodInfo s_LightList_LinqBinding_GetMinSize = typeof(LightList<LinqBinding>).GetMethod(nameof(LightList<LinqBinding>.GetMinSize));
        private static readonly ConstructorInfo s_SlotUsage_Ctor = typeof(SlotUsage).GetConstructor(new[] {typeof(string), typeof(int), typeof(LexicalScope)});

        private static readonly FieldInfo s_LexicalScope_root = typeof(LexicalScope).GetField(nameof(LexicalScope.root), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo s_LexicalScope_data = typeof(LexicalScope).GetField(nameof(LexicalScope.data), BindingFlags.Instance | BindingFlags.Public);

        public const string k_ContextStackVarName = "__contextStack";
        public const string k_RootElementVarName = "__root";
        public const string k_CurrentElementVarName = "__element";

        public TemplateCompiler(Application application) {
            this.application = application;
            this.linqCompiler = new LinqCompiler();
            this.propertyCompiler = new LinqPropertyCompiler(linqCompiler);
            this.templateMap = new Dictionary<Type, CompiledTemplate>();
            this.compilationStack = new LightStack<Type>();
            this.xmlTemplateParser = new XMLTemplateParser(application);
            
//            linqCompiler.PushAliasResolver("$siblingIndex", new AliasResolver());
//            linqCompiler.PushAliasResolver("$parent", new AliasResolver());
//            linqCompiler.PushAliasResolver("$root", new AliasResolver());
//            linqCompiler.PushAliasResolver("$element", new AliasResolver());
            
        }

        private void VisitChildren(TemplateNode node, CompilationContext ctx, CompiledTemplate retn) {
            if (node.children == null || node.children.Count <= 0) {
                return;
            }

            ctx.PushScope();
            Expression parentChildList = Expression.Field(ctx.ParentExpr, s_Element_ChildrenList);
            Expression parentChildListArray = Expression.Field(parentChildList, s_LightList_Element_Array);

            // childList[idx] = Visit()
            for (int i = 0; i < node.children.Count; i++) {
                Expression visit = Visit(node.children[i], ctx, retn);
                // will be null for templates
                if (visit != null) {
                    ctx.AddStatement(
                        Expression.Assign(
                            Expression.ArrayAccess(parentChildListArray, Expression.Constant(i)),
                            visit
                        )
                    );
                }
            }

            ctx.PopScope();
        }

        public CompiledTemplate GetCompiledTemplate(Type type) {
            return GetCompiledTemplate(TypeProcessor.GetProcessedType(type));
        }

        public CompiledTemplate GetCompiledTemplate(ProcessedType processedType) {
            if (typeof(UIContainerElement).IsAssignableFrom(processedType.rawType)) {
                return null;
            }

            if ((typeof(UITextElement).IsAssignableFrom(processedType.rawType))) {
                return null;
            }

            if (templateMap.TryGetValue(processedType.rawType, out CompiledTemplate retn)) {
                return retn;
            }

            if (compilationStack.Contains(processedType.rawType)) {
                string recursion = "";
                for (int i = 0; i < compilationStack.Count; i++) {
                    recursion += compilationStack.PeekAtUnchecked(i);
                    if (i != compilationStack.Count) {
                        recursion += " -> ";
                    }
                }

                throw new CompileException("Template Recursion detected: " + recursion);
            }

            compilationStack.Push(processedType.rawType);

            TemplateAST ast = xmlTemplateParser.Parse(processedType);

            CompiledTemplate compiledTemplate = Compile(ast);

            TemplateNode.Release(ref ast.root);

            compilationStack.Pop();

            templateMap[processedType.rawType] = compiledTemplate;

            return compiledTemplate;
        }

        private CompiledTemplate Compile(TemplateAST ast) {
            CompiledTemplate retn = new CompiledTemplate();
            retn.childCount = ast.root.children.size;

            TemplateNode root = ast.root;
            LightList<string> namespaces = LightList<string>.Get();

            if (ast.usings != null) {
                for (int i = 0; i < ast.usings.size; i++) {
                    namespaces.Add(ast.usings[i].namespaceName);
                }
            }

            ProcessedType processedType = ast.root.processedType;

            ParameterExpression rootParam = Expression.Parameter(typeof(UIElement), "root");
            ParameterExpression scopeParam = Expression.Parameter(typeof(TemplateScope2), "scope");

            CompilationContext ctx = new CompilationContext();
            ctx.rootType = processedType;
            ctx.rootParam = rootParam;
            ctx.templateScope = scopeParam;
            ctx.elementType = processedType;
            ctx.applicationExpr = Expression.Field(scopeParam, s_Scope_ApplicationField);
            ctx.templateData = Expression.Field(scopeParam, s_Scope_CompiledTemplate);

            Expression scopeBindingNode = Expression.Field(scopeParam, s_ScopeBindingNodeField);

            ctx.Initialize(rootParam, scopeBindingNode);

            {
                ctx.PushBlock();

                Expression createRootExpression = Expression.Call(ctx.applicationExpr, s_CreateFromPool,
                    Expression.Constant(processedType.rawType),
                    Expression.Default(typeof(UIElement)),
                    Expression.Constant(ast.root.children.size)
                );

                ctx.AddStatement(Expression.Assign(ctx.rootParam, createRootExpression));

                ProcessBindings(ast.root, ctx, false);

                BlockExpression createUnscopedBlock = ctx.PopBlock();

                ctx.AddStatement(Expression.IfThen(Expression.Equal(ctx.rootParam, Expression.Constant(null)), createUnscopedBlock));
            }

            VisitChildren(root, ctx, retn);

            ctx.AddStatement(ctx.rootParam); // this is the return value

            retn.templateId = application.templateData.AddTemplate(Expression.Lambda(ctx.Finalize(typeof(UIElement)), rootParam, scopeParam));
            
            retn.elementType = ast.root.processedType;
            retn.attributes = ast.root.attributes.ToArray();

            LightList<string>.Release(ref namespaces);

            //todo  release context
            
            return retn;
        }

        private int CompileSlot(TemplateNode templateNode, CompilationContext parentCtx, CompiledTemplate template) {
            // want a fresh set of variables but keep the style / file / binding data / etc contexts
            CompilationContext ctx = new CompilationContext();

            // todo -- use parent context for style & imports n stuff

            ParameterExpression bindingNodeParam = Expression.Parameter(typeof(LinqBindingNode), "bindingNode");
            ParameterExpression parentParam = Expression.Parameter(typeof(UIElement), "parent");
            ParameterExpression appParam = Expression.Parameter(typeof(Application), "application");
            ParameterExpression lexicalScopeParam = Expression.Parameter(typeof(LexicalScope), "lexicalScope");
            ParameterExpression retnVal = Expression.Parameter(typeof(UIElement), "retn");

            ctx.applicationExpr = appParam;

            ctx.templateData = Expression.Field(lexicalScopeParam, s_LexicalScope_data);
            ctx.rootParam = Expression.Parameter(typeof(UIElement), "root");

            ctx.variables.Add((ParameterExpression) ctx.rootParam);
            ctx.variables.Add(retnVal);

            ctx.Initialize(retnVal, bindingNodeParam);

            ctx.AddStatement(Expression.Assign(ctx.rootParam, Expression.Field(lexicalScopeParam, s_LexicalScope_root)));

            Expression createRootExpression = Expression.Call(ctx.applicationExpr, s_CreateFromPool,
                Expression.Constant(templateNode.processedType.rawType == typeof(UIChildrenElement)
                    ? typeof(UIChildrenElement)
                    : typeof(UISlotContent)),
                parentParam,
                Expression.Constant(templateNode.children.size)
            );

            ctx.AddStatement(Expression.Assign(retnVal, createRootExpression));

            VisitChildren(templateNode, ctx, template);

            ctx.AddStatement(retnVal);

            Expression<SlotUsageTemplate> lambda = Expression.Lambda<SlotUsageTemplate>(
                ctx.Finalize(typeof(UIElement)),
                appParam,
                bindingNodeParam,
                parentParam,
                lexicalScopeParam
            );

            LogCode("SlotUsage: " + templateNode.slotName, lambda);
            return application.AddSlotUsageTemplate(lambda);
        }

        // todo -- process attributes! need to save them on SlotUsage?
        private Expression CompileSlotUsage(TemplateNode templateNode, CompilationContext parentCtx, CompiledTemplate template) {
            int slotTemplateId = CompileSlot(templateNode, parentCtx, template);

            return Expression.New(s_SlotUsage_Ctor,
                Expression.Constant(templateNode.slotName),
                Expression.Constant(slotTemplateId),
                Expression.New(s_LexicalScope_Ctor,
                    parentCtx.rootParam,
                    Expression.Default(typeof(CompiledTemplate)) // todo -- need to reference the compiled template to get data for styles & bindings
                )
            );
        }

        private void CompileToStoredSlot(TemplateNode templateNode, CompilationContext parentCtx, CompiledTemplate template) {
            // traverse to find slot definitions
            // if has slot definitions, emit StoreTemplate(new string[] { slotIds }, slotName, templateId, root, template);
            // todo -- attributes? probably part of the template itself, just need to store context
            // element.StoreTemplate(templateNode.slotName, templateId, new LexicalScope(root, template));
            // templates can only exist at the root level (or better maybe outside in a <Templates> tag
            // need to store incoming template scope, or at minimum the slotInputs on it if there are any
            // best to gather a list of templates to store and do them all at once, emit a single call to element.StoreTemplates(slotInputs, root (which is always == this), compiledTemplate (which should maybe be made available on element) new [] { templatesToStore })

            // since I know where the slotusage came from maybe i don't need to alloc arrays for them, handled by template code itself
            // attributes
            // styles
            // bindings
            // context
            // element.StoreTemplates(compiledTemplate, scope.inputSlots.ToArray(), new int[] { new StoredTemplate("TemplateName1", templateFnId1), new StoredTemplate("TemplateName2", templateFnId2) });

            // this method should return which input slots it requires so they can be saved.
            // do a gather phase, then output data as code.
        }

        private ParameterExpression VisitSlotDefinition(TemplateNode templateNode, CompilationContext ctx, CompiledTemplate template) {
            ProcessedType processedType = templateNode.processedType;

            ctx.elementType = processedType;

            if (template.TryGetSlotData(templateNode.slotName, out SlotDefinition slotData)) {
                // fail
                throw new TemplateParseException("todo -- file", "Duplicate slot detected: " + templateNode.slotName);
            }

            int idx = 0;
            TemplateNode ptr = templateNode.parent;
            slotData = new SlotDefinition(templateNode.slotName);
            slotData.slotType = templateNode.HasAttribute("template") ? SlotType.Template : SlotType.Element;

            while (ptr != null) {
                // more than 4 levels of nesting will explode fosho
                if (ptr.processedType == typeof(UISlotDefinition)) {
                    slotData[idx++] = template.GetSlotId(ptr.slotName);

                    if (slotData.slotType == SlotType.Element && ptr.HasAttribute("template")) {
                        slotData.slotType = SlotType.Template;
                    }
                }

                ptr = ptr.parent;
            }

            if (slotData.slotType == SlotType.Template) {
                CompileToStoredSlot(templateNode, ctx, template);
                return null;
            }

            ParameterExpression nodeExpr = ctx.ElementExpr;
            
            // todo process attributes etc

            template.AddSlotData(slotData);

            int slotTemplateId = CompileSlot(templateNode, ctx, template);

            ctx.AddStatement(
                Expression.Assign(
                    ctx.ElementExpr,
                    Expression.Call(ctx.applicationExpr, s_Application_CreateSlot,
                        Expression.Field(ctx.templateScope, s_TemplateScope_SlotInputList),
                        Expression.Constant(templateNode.slotName),
                        ctx.BindingNodeExpr, //Expression.Default(typeof(LinqBindingNode)), // todo -- linq node!
                        ctx.ParentExpr,
                        ctx.rootParam,
                        ctx.templateData,
                        Expression.Constant(slotTemplateId)
                    )
                )
            );
            

            return nodeExpr;
        }


        private ParameterExpression Visit(TemplateNode templateNode, in CompilationContext ctx, CompiledTemplate template) {
            ProcessedType processedType = templateNode.processedType;

            if (processedType.rawType == typeof(UISlotDefinition)) {
                return VisitSlotDefinition(templateNode, ctx, template);
            }

//            if (templateNode.isStoredTemplate) {
//                if (templateNode.parent != templateNode.astRoot.root) {
//                    // throw new InvalidTemplateException();
//                }
//            }

            Type type = processedType.rawType;
            ctx.elementType = processedType;

            ParameterExpression nodeExpr = ctx.ElementExpr;


            ctx.AddStatement(
                Expression.Assign(nodeExpr, Expression.Call(ctx.applicationExpr, s_CreateFromPool, Expression.Constant(type), ctx.ParentExpr, Expression.Constant(templateNode.children.size)))
            );

            bool hasTextBindings = HasTextBinding(templateNode);

            bool requiresBindingNode = hasTextBindings || RequiresBindingNode(templateNode.attributes, ctx);

            if (requiresBindingNode) {
                ctx.PushBinding();
                ctx.AddStatement(Expression.Assign(ctx.BindingNodeExpr,
                        Expression.Call(null, s_BindingNodePool_Get,
                            ctx.applicationExpr,
                            ctx.rootParam,
                            ctx.ElementExpr
                        )
                    )
                );
            }

            bool hasContextProvider = UpdateContextTree(templateNode, ctx, out int aliasPopCount);

            if (processedType.requiresTemplateExpansion) {
                CompiledTemplate compiled = GetCompiledTemplate(processedType);

                Expression bindingNode = ctx.BindingNodeExpr;

                // merge bindings, outer ones win, take the base bindings and replace duplicates with outer ones
                MergeAttributes(templateNode, compiled.attributes);

                ProcessBindings(templateNode, ctx, hasTextBindings);

                Expression templateScopeCtor;

                // All children are slots, children added to the element that are not <Slot> elements themselves are wrapped in a <Slot:Children> implicitly.

                if (templateNode.children != null && templateNode.children.Count > 0) {
                    Expression slotUsage = ctx.GetSlotUsageArray(templateNode.children.size);

                    ctx.PushScope();

                    LightList<string> slotList = LightList<string>.GetMinSize(templateNode.children.size);

                    for (int i = 0; i < templateNode.children.size; i++) {
                        ctx.AddStatement(
                            Expression.Assign(
                                Expression.ArrayAccess(Expression.Field(slotUsage, s_StructList_SlotUsage_Array), Expression.Constant(i)),
                                // slot usage[i] = new SlotUsage("slotName, templateId, new LexicalScope(root, template));
                                CompileSlotUsage(templateNode.children[i], ctx, template))
                        );
                        slotList.Add(templateNode.children[i].slotName);
                    }

                    compiled.ValidateSlotHierarchy(slotList);

                    LightList<string>.Release(ref slotList);

                    ctx.PopScope();

                    // templateScope = new TemplateScope2(application, bindingNode, slotInput);
                    templateScopeCtor = Expression.New(s_TemplateScope_Ctor, ctx.applicationExpr, bindingNode, slotUsage);
                    // scope.application.HydrateTemplate(templateId, targetElement, templateScope)
                    ctx.AddStatement(Expression.Call(ctx.applicationExpr, s_Application_HydrateTemplate, Expression.Constant(compiled.templateId), nodeExpr, templateScopeCtor));

                    ctx.ReleaseSlotUsage();
                }
                else {
                    // templateScope = new TemplateScope2(application, bindingNode, null);
                    templateScopeCtor = Expression.New(s_TemplateScope_Ctor, ctx.applicationExpr, bindingNode, Expression.Default(typeof(StructList<SlotUsage>)));
                    // scope.application.HydrateTemplate(templateId, targetElement, templateScope)
                    ctx.AddStatement(Expression.Call(ctx.applicationExpr, s_Application_HydrateTemplate, Expression.Constant(compiled.templateId), nodeExpr, templateScopeCtor));
                }
            }
            else {
                // if all bindings for the element can be shared then we use a list span over the global array
                // otherwise we get our own array of bindings. 

                ProcessBindings(templateNode, ctx, hasTextBindings);

                VisitChildren(templateNode, ctx, template);
            }

            if (hasContextProvider) {
                ctx.contextProviderStack.Pop();
            }

            for (int i = 0; i < aliasPopCount; i++) {
                linqCompiler.PopAliasResolver();
            }

            if (!hasTextBindings && templateNode.textContent != null) {
                ctx.AddStatement(Expression.Assign(
                    Expression.MakeMemberAccess(
                        Expression.Convert(nodeExpr, typeof(UITextElement)),
                        s_TextElement_Text
                    ),
                    Expression.Constant(templateNode.GetStringContent())
                ));
            }

            if (requiresBindingNode) {
                ctx.AddStatement(Expression.Call(ctx.ParentBindingNodeExpr, s_BindingNode_AddChild, ctx.BindingNodeExpr));
                ctx.PopBinding();
            }

            return nodeExpr;
        }

        private struct BindingDefinition {

            public bool isShared;
            public bool isConstant;
            public int id;

        }

        private void ProcessBindings(TemplateNode templateNode, CompilationContext ctx, bool hasTextBindings) {
            StructList<BindingDefinition> bindings = StructList<BindingDefinition>.Get();

            BindingDefinition? enabledBinding = default;

            if (templateNode.attributes != null && templateNode.attributes.size > 0) {
                AttributeDefinition2[] attributeDefinitions = templateNode.attributes.array;
                int attrCount = templateNode.attributes.size;

                int startIdx = 0;

                for (int i = 0; i < attrCount; i++) {
                    if (attrCount - 1 == i || attributeDefinitions[i].type != attributeDefinitions[i + 1].type) {
                        switch (attributeDefinitions[i].type) {
                            case AttributeType.Attribute:
                                EmitAttributes(attributeDefinitions, ctx, startIdx, i + 1, bindings);
                                break;

                            case AttributeType.Property:
                                EmitProperties(attributeDefinitions, ctx, startIdx, i + 1, bindings, out enabledBinding);
                                break;

                            case AttributeType.Style:
                                EmitStyles(attributeDefinitions, ctx, startIdx, i + 1);
                                break;
                        }

                        startIdx = i + 1;
                    }
                }
            }

            if (enabledBinding != null) { }

            if (hasTextBindings) {
                bindings.Add(CompileTextBinding(templateNode));
            }

            if (ctx.elementType.requiresUpdateFn) {
                // null is a stand in for 'use the default update binding'
                bindings.Add(new BindingDefinition() {
                    id = application.templateData.AddSharedBindingLambda(null),
                    isShared = true,
                    isConstant = false
                });
            }

            bool requiresOwnList = false;
            BindingDefinition[] definitions = bindings.array;
            for (int i = 0; i < bindings.size; i++) {
                if (!definitions[i].isShared) {
                    requiresOwnList = true;
                    break;
                }
            }

            for (int i = 0; i < bindings.size; i++) {
                if (bindings.array[i].isConstant) {
                    // todo -- get value & remove from shared or instance list, assign value directly to target property.
                }
            }

            if (requiresOwnList) {
                // todo -- this, but I think event subscriptions are now the only bindings requiring their own data and they probably work differently (ie not a real binding)
                // bindingNode_1.bindings = new ListSpan(LightList<LinqBindingNode>.GetMinSize(4), 0, 4);
                // bindingNode_1.bindings.array[0] = templateData.sharedBindings[252];
                // bindingNode_1.bindings.array[1] = templateData.sharedBindings[253];
                // bindingNode_1.bindings.array[2] = templateData.instanceBindings[14].Get();
                // bindingNode_1.bindings.array[3] = templateData.sharedBindings[254];
                ctx.AddStatement(
                    Expression.Assign(
                        Expression.Field(ctx.BindingNodeExpr, s_BindingNode_BindingList),
                        Expression.New(s_LightList_LinqBinding_SpanCtor,
                            Expression.Call(null, s_LightList_LinqBinding_GetMinSize, Expression.Constant(bindings.size)),
                            Expression.Constant(0),
                            Expression.Constant(bindings.size)
                        )
                    )
                );
            }
            else {
                ctx.AddStatement(
                    Expression.Assign(
                        Expression.Field(ctx.BindingNodeExpr, s_BindingNode_BindingList),
                        Expression.New(s_LightList_LinqBinding_SpanCtor,
                            Expression.Field(Expression.Field(ctx.applicationExpr, s_Application_TemplateData), s_TemplateData_SharedBindingList),
                            Expression.Constant(0),
                            Expression.Constant(bindings.size)
                        )
                    )
                );
                // bindingNode_1.bindings = new ListSpan(templateData.sharedBindings, 242, 246);
            }

            bindings.Release();
        }

        private static bool HasTextBinding(TemplateNode templateNode) {
            if (templateNode.textContent == null) {
                return false;
            }

            for (int i = 0; i < templateNode.textContent.size; i++) {
                if (templateNode.textContent.array[i][0] == '{' && templateNode.textContent.array[i][templateNode.textContent.array[i].Length - 1] == '}') {
                    return true;
                }
            }

            return false;
        }

        private BindingDefinition CompileEnabledBinding(CompilationContext ctx, string input) {
            SetCompilerSignature();

            // todo -- allow loose falsy check here

            linqCompiler.Statement($"__element.SetEnabled({input})");

            LambdaExpression lambda = linqCompiler.BuildLambda();
            linqCompiler.Reset();

            return new BindingDefinition() {
                isConstant = false,
                isShared = true,
                id = application.templateData.AddSharedBindingLambda(lambda)
            };
        }

        private BindingDefinition CompileTextBinding(TemplateNode templateNode) {
            LightList<string> expressionParts = templateNode.textContent;

            SetCompilerSignature();

            linqCompiler.AddNamespace("UIForia.Util");
            linqCompiler.AddVariable(new Parameter<UITextElement>("__textElement", ParameterFlags.NeverNull), Expression.Convert(linqCompiler.GetParameter("__element"), typeof(UITextElement)));
            linqCompiler.AddVariable(new Parameter<StringBuilder>("__stringBuilder", ParameterFlags.NeverNull), "TextUtil.StringBuilder");

            // todo -- more can be done here to get rid of allocations
            // todo -- don't always call set text
            // if binding is not shared we can store last frame's evaluation results & diff them against this frames w/o doing a string join

            for (int i = 0; i < expressionParts.size; i++) {
                // text joiner
                // convert text expression outputs to an array 
                // output = ["text", expression, "here"].Join();
                // later -> visit any non const expressions and break apart by top level string-to-string + operator

                if (expressionParts[i][0] == '{' && expressionParts[i][expressionParts[i].Length - 1] == '}') {
                    linqCompiler.Statement("__stringBuilder.Append(" + expressionParts[i].Substring(1, expressionParts[i].Length - 2) + ")");
                }
                else {
                    linqCompiler.Statement("__stringBuilder.Append('" + expressionParts[i] + "')");
                }
            }

            linqCompiler.Statement("__textElement.SetText(__stringBuilder.ToString())");
            linqCompiler.Log();

            int bindingId = application.templateData.AddSharedBindingLambda(linqCompiler.BuildLambda());

            linqCompiler.Reset();

            // todo -- revisit shared & constant
            return new BindingDefinition() {
                id = bindingId,
                isConstant = false,
                isShared = true
            };
        }

        private static bool RequiresBindingNode(StructList<AttributeDefinition2> attributes, in CompilationContext ctx) {
            if (ctx.elementType.requiresUpdateFn) {
                return true;
            }

            int attrCount = attributes.size;
            AttributeDefinition2[] attributeDefinitions = attributes.array;

            for (int i = 0; i < attrCount; i++) {
                switch (attributeDefinitions[i].type) {
                    case AttributeType.Attribute:
                        break; // todo -- only true if dynamic

                    case AttributeType.Property:
                        return true;

                    case AttributeType.Style:
                        return true; // todo -- only true if dynamic

                    case AttributeType.Context:
                        return true;

                    case AttributeType.ContextVariable:
                        break;

                    case AttributeType.Alias:
                        break;
                }
            }

            return false;
        }


        private bool UpdateContextTree(TemplateNode templateNode, CompilationContext ctx, out int aliasCount) {
            int contextIdx = -1;
            aliasCount = 0;

            StructList<TemplateContextVariable> contextVars = StructList<TemplateContextVariable>.Get();

            for (int i = 0; i < templateNode.attributes.size; i++) {
                if (templateNode.attributes[i].type == AttributeType.Context) {
                    if (contextIdx != -1) {
                        throw new Exception("We only support 1 context currently");
                    }

                    contextIdx = i;
                }
                else if (templateNode.attributes[i].type == AttributeType.ContextVariable) {
                    contextVars.Add(new TemplateContextVariable(
                        templateNode.attributes.array[i].key,
                        templateNode.attributes.array[i].value
                    ));
                }
                else if (templateNode.attributes[i].type == AttributeType.Alias) {
                    // todo 
                }
            }

            if (contextIdx != -1) {
                AttributeDefinition2 attr = templateNode.attributes.array[contextIdx];

                ctx.contextProviderStack = ctx.contextProviderStack ?? new LightStack<TemplateContextDefinition>();

                // currently don't support aliases or other refs in this expression type
                LambdaExpression expression = CompileContextExpression(ctx.rootType.rawType, attr.value, out Type contextType);

                int expressionId = application.templateData.AddContextProviderLambda(expression);
                int contextId = TemplateContextDefinition.IdGenerator++;

                if (contextVars.size == 0) {
                    contextVars.Release();
                }

                ctx.contextProviderStack.Push(new TemplateContextDefinition() {
                    name = attr.key,
                    expressionId = expressionId,
                    type = expression.Type,
                    id = contextId,
                    variables = contextVars
                });

                Expression templateData = Expression.Field(ctx.applicationExpr, s_Application_TemplateData);
                Expression array = Expression.Field(templateData, s_TemplateData_ContextProviderFns);
                Expression arrayLookup = Expression.ArrayAccess(array, Expression.Constant(expressionId));
                Expression createContext = Expression.Invoke(arrayLookup, ctx.rootParam, ctx.ElementExpr);

                // bindingNode.SetContextProvider(scope.application.templateData[24](root, element), id);
                ctx.AddStatement(
                    Expression.Call(ctx.BindingNodeExpr, s_BindingNode_SetContextProvider, createContext, Expression.Constant(ctx.contextProviderStack.PeekUnchecked().id))
                );

                linqCompiler.PushAliasResolver(attr.key, new ContextAliasResolver(contextType, attr.key, contextId));

                for (int i = 0; i < contextVars.size; i++) {
                    linqCompiler.PushAliasResolver("$" + contextVars[i].name, new ContextVariableResolver(contextType, attr.key, contextId, contextVars.array[i]));
                }

                aliasCount = contextVars.size + 1;
            }

            if (contextVars.size > 0 && contextIdx == -1) {
                throw new Exception("You cannot use context variables without defining a context. If you mean to alias a context or context variable use an alias attribute specifier instead.");
            }

            return contextIdx != -1;
        }

        private LambdaExpression CompileContextExpression(Type rootType, string input, out Type contextType) {
            linqCompiler.SetSignature<TemplateContext>(
                new Parameter(typeof(UIElement), k_RootElementVarName, ParameterFlags.NeverNull),
                new Parameter(typeof(UIElement), k_CurrentElementVarName, ParameterFlags.NeverNull)
            );

            ParameterExpression rootExpr = linqCompiler.AddVariable(
                new Parameter(rootType, "_castRoot", ParameterFlags.NeverNull),
                Expression.Convert(linqCompiler.GetParameter(k_RootElementVarName), rootType)
            );

            linqCompiler.SetImplicitContext(rootExpr, ParameterFlags.NeverNull);

            linqCompiler.Return(input, out contextType);

            LambdaExpression lambdaExpression = linqCompiler.BuildLambda();

            linqCompiler.Log();
            linqCompiler.Reset();

            return lambdaExpression;
        }

        private void EmitProperties(StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex, StructList<BindingDefinition> outputList, out BindingDefinition? enabledBinding) {
            AttributeDefinition2[] attributeDefinitions = attributes.array;

            enabledBinding = default;

            for (int i = startIdx; i < endIndex; i++) {
                if (attributeDefinitions[i].key == "if") {
                    enabledBinding = CompileEnabledBinding(ctx, attributeDefinitions[i].value);
                    continue;
                }

                LambdaExpression bindingExpression = propertyCompiler.BuildLambda(ctx, attributeDefinitions[i]);

                int bindingId = application.templateData.AddSharedBindingLambda(bindingExpression);

                outputList.Add(new BindingDefinition() {
                    id = bindingId,
                    isConstant = false,
                    isShared = true
                });
            }
        }

        private void EmitStyles(StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex) { }

        private LambdaExpression CompileAttributeBinding(CompilationContext ctx, in AttributeDefinition2 attr) {
            // output = element.SetAttribute(attr.key, expression);    
            SetCompilerSignature();

            // todo -- force string cast
            // todo -- probably some deconstruction we can do here to reduce allocations from concatenation
            // then compare character per character with current value and only call set if changed.
            linqCompiler.Statement(k_CurrentElementVarName + ".SetAttribute('" + attr.key + "', " + attr.value + ")");

            LambdaExpression retn = linqCompiler.BuildLambda();

            linqCompiler.Reset();
            return retn;
        }

        private void SetCompilerSignature() {
            linqCompiler.SetSignature(
                new Parameter(typeof(UIElement), k_RootElementVarName, ParameterFlags.NeverNull),
                new Parameter(typeof(UIElement), k_CurrentElementVarName, ParameterFlags.NeverNull),
                new Parameter(typeof(StructStack<TemplateContextWrapper>), k_ContextStackVarName, ParameterFlags.NeverNull)
            );
        }

        private void EmitAttributes(StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex, StructList<BindingDefinition> bindingOutput) {
            AttributeDefinition2[] attributeDefinitions = attributes.array;

            int cnt = endIndex - startIdx;

            if (cnt == 0) return;

            int attrBindingCount = 0;

            for (int i = startIdx; i < endIndex; i++) {
                ref AttributeDefinition2 attr = ref attributeDefinitions[i];

                // if attr is wrapped in {} treat as a binding of type string. 
                if (attr.value[0] == '{' && attr.value[attr.value.Length - 1] == '}') {
                    attrBindingCount++;

                    LambdaExpression lambda = CompileAttributeBinding(ctx, attr);

                    bindingOutput.Add(new BindingDefinition() {
                        isConstant = false,
                        isShared = true,
                        id = application.templateData.AddSharedBindingLambda(lambda)
                    });
                }
            }

            // still want to allocate the right number of attributes in our array if we have bindings.
            if (attrBindingCount == cnt) {
                return;
            }

            ParameterExpression attributeList = ctx.GetVariable(typeof(StructList<ElementAttribute>), "attributeList");
            ParameterExpression array = ctx.GetVariable(typeof(ElementAttribute[]), "attributeArray");

            ctx.AddStatement(
                Expression.Assign(attributeList, Expression.Call(null, s_GetStructList_ElementAttr, Expression.Constant(cnt)))
            );

            Expression attrListAccess = Expression.MakeMemberAccess(attributeList, s_StructList_ElementAttr_Array);

            int idx = 0;

            ctx.AddStatement(
                Expression.Assign(
                    Expression.Field(attributeList, s_StructList_ElementAttr_Size), Expression.Constant(cnt)
                )
            );

            ctx.AddStatement(Expression.Assign(array, attrListAccess));

            for (int i = startIdx; i < endIndex; i++) {
                ref AttributeDefinition2 attr = ref attributeDefinitions[i];

                if (attr.value[0] == '{' && attr.value[attr.value.Length - 1] == '}') {
                    continue;
                }

                // attributeArray[idx] = new ElementAttribute(attr.key, attr.value);
                NewExpression newExpression = Expression.New(s_ElementAttributeCtor, Expression.Constant(attr.key), Expression.Constant(attr.value));
                Expression arrayIndex = Expression.ArrayAccess(array, Expression.Constant(idx++));

                ctx.AddStatement(
                    Expression.Assign(arrayIndex, newExpression)
                );
            }

            ctx.AddStatement(
                Expression.Assign(
                    Expression.Field(ctx.ElementExpr, s_ElementAttributeList),
                    attributeList
                )
            );
        }

        private static void MergeAttributes(TemplateNode templateNode, StructList<AttributeDefinition2> outer) {
            StructList<AttributeDefinition2> mergedAttributes = StructList<AttributeDefinition2>.GetMinSize(templateNode.attributes.size + outer.size);

            // match on type & name, might have to track source also in case of binding context

            // add all outer ones
            mergedAttributes.AddRange(outer);

            int outerCount = outer.size;
            AttributeDefinition2[] mergedArray = mergedAttributes.array;
            AttributeDefinition2[] inner = templateNode.attributes.array;

            for (int i = 0; i < inner.Length; i++) {
                // for each inner attribute
                // if no match found, add it, be sure to set context flag
                string key = inner[i].key;
                AttributeType attributeType = inner[i].type;

                bool contains = false;
                for (int j = 0; j < outerCount; j++) {
                    // if key and type matches, break out of the loop
                    if (mergedArray[j].key == key && mergedArray[j].type == attributeType) {
                        contains = true;
                        break;
                    }
                }

                if (!contains) {
                    AttributeDefinition2 attr = inner[i];
                    attr.flags |= AttributeFlags.RootContext;
                    mergedAttributes.Add(attr);
                }
            }

            templateNode.attributes.Release();
            templateNode.attributes = mergedAttributes;
        }


        private static void LogCode(Expression expression, bool printNamespaces = false) {
            bool old = CSharpWriter.printNamespaces;
            CSharpWriter.printNamespaces = printNamespaces;
            string retn = expression.ToCSharpCode();
            CSharpWriter.printNamespaces = old;
            Debug.Log(retn);
        }

        private static void LogCode(string comment, Expression expression, bool printNamespaces = false) {
            bool old = CSharpWriter.printNamespaces;
            CSharpWriter.printNamespaces = printNamespaces;
            string retn = expression.ToCSharpCode();
            CSharpWriter.printNamespaces = old;
            Debug.Log(comment);
            Debug.Log(retn + "\n");
        }

    }

}
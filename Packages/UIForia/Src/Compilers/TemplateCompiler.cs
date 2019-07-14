using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Linq.Expressions;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.LinqExpressions;
using UIForia.Parsing.Expression;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Compilers {

    public class TemplateContextTreeDefinition { }

    public class TemplateCompiler {

        public Application application;
        private int varId;
        private LinqCompiler linqCompiler;

        public LinqStyleCompiler styleCompiler;
        public LinqPropertyCompiler propertyCompiler;
        private LightStack<Type> compilationStack;
        private Dictionary<Type, CompiledTemplate> templateMap;
        private XMLTemplateParser xmlTemplateParser;

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
        
        private static readonly MethodInfo s_BindingNode_AddChild = typeof(LinqBindingNode).GetMethod(nameof(LinqBindingNode.AddChild), BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo s_BindingNode_SetContextProvider = typeof(LinqBindingNode).GetMethod(nameof(LinqBindingNode.SetContextProvider), BindingFlags.Public | BindingFlags.Instance);
    
        private static readonly ConstructorInfo s_SlotUsage_Ctor = typeof(SlotUsage).GetConstructor(new[] {typeof(string), typeof(int), typeof(LexicalScope)});

        private static readonly FieldInfo s_LexicalScope_root = typeof(LexicalScope).GetField(nameof(LexicalScope.root), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo s_LexicalScope_data = typeof(LexicalScope).GetField(nameof(LexicalScope.data), BindingFlags.Instance | BindingFlags.Public);

        public TemplateCompiler(Application application) {
            this.application = application;
            this.propertyCompiler = new LinqPropertyCompiler();
            this.templateMap = new Dictionary<Type, CompiledTemplate>();
            this.compilationStack = new LightStack<Type>();
            this.xmlTemplateParser = new XMLTemplateParser(application);
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
            ParameterExpression templateParam = Expression.Parameter(typeof(CompiledTemplate), "templateData");

            CompilationContext ctx = new CompilationContext();
            ctx.rootType = processedType.rawType;
            ctx.rootParam = rootParam;
            ctx.templateScope = scopeParam;
            ctx.elementType = processedType.rawType;
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

                ProcessAttributes(processedType.rawType, retn, null, ast.root.attributes, ctx, out bool hasBindings, out bool hasContextProvider);

                BlockExpression createUnscopedBlock = ctx.PopBlock();

                ctx.AddStatement(Expression.IfThen(Expression.Equal(ctx.rootParam, Expression.Constant(null)), createUnscopedBlock));
            }


            VisitChildren(root, ctx, retn);

            ctx.AddStatement(ctx.rootParam); // this is the return value

            retn.buildExpression = Expression.Lambda<Func<UIElement, TemplateScope2, CompiledTemplate, UIElement>>(ctx.Finalize(typeof(UIElement)), rootParam, scopeParam, templateParam);
            retn.elementType = ast.root.processedType;
            retn.attributes = ast.root.attributes.ToArray();

            LightList<string>.Release(ref namespaces);

            //todo  release context

            application.templateCache.Add(retn);

            LogCode(retn.buildExpression);

            return retn;
        }

        private int CompileSlot(TemplateNode templateNode, CompilationContext parentCtx, CompiledTemplate template) {
            // want a fresh set of variables but keep the style / file / binding data / etc contexts
            CompilationContext ctx = new CompilationContext();

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
            Type type = processedType.rawType;

            ctx.elementType = type;

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

            bool hasBindings = false;

            // todo process attributes etc

            template.AddSlotData(slotData);

            int slotTemplateId = CompileSlot(templateNode, ctx, template);

            ctx.AddStatement(
                Expression.Assign(
                    ctx.ElementExpr,
                    Expression.Call(ctx.applicationExpr, s_Application_CreateSlot,
                        Expression.Field(ctx.templateScope, s_TemplateScope_SlotInputList),
                        Expression.Constant(templateNode.slotName),
                        Expression.Default(typeof(LinqBindingNode)), // todo -- linq node!
                        ctx.ParentExpr,
                        ctx.rootParam,
                        ctx.templateData,
                        Expression.Constant(slotTemplateId)
                    )
                )
            );

            if (hasBindings) {
                ctx.bindingNodeStack.Pop();
            }

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
            ctx.elementType = type;

            ParameterExpression nodeExpr = ctx.ElementExpr;

            bool hasBindings;
            bool hasContextProvider;

            ctx.AddStatement(
                Expression.Assign(nodeExpr, Expression.Call(ctx.applicationExpr, s_CreateFromPool, Expression.Constant(type), ctx.ParentExpr, Expression.Constant(templateNode.children.size)))
            );

            if (typeof(UITextElement).IsAssignableFrom(type)) {
                ctx.AddStatement(Expression.Assign(
                    Expression.MakeMemberAccess(
                        Expression.Convert(nodeExpr, typeof(UITextElement)),
                        s_TextElement_Text
                    ),
                    Expression.Constant(templateNode.textContent)
                ));
            }

            if (processedType.requiresTemplateExpansion) {
                CompiledTemplate compiled = GetCompiledTemplate(processedType);

                // todo -- binding
                Expression bindingNode = Expression.Default(typeof(LinqBindingNode));

                // merge bindings, outer ones win, take the base bindings and replace duplicates with outer ones
                StructList<AttributeDefinition2> attributes = MergeAttributes(compiled.attributes, templateNode.attributes);

                ProcessAttributes(template, compiled, attributes, ctx, out hasBindings, out hasContextProvider);

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
                ProcessAttributes(template, null, templateNode.attributes, ctx, out hasBindings, out hasContextProvider);
                VisitChildren(templateNode, ctx, template);
            }

            if (hasBindings) {
                ctx.bindingNodeStack.Pop();
            }

            if (hasContextProvider) {
                ctx.contextProviderStack.Pop();
            }

            return nodeExpr;
        }


        private void ProcessAttributes(ProcessedType processedType, CompiledTemplate outerTemplate, CompiledTemplate innerTemplate, StructList<AttributeDefinition2> attributes, in CompilationContext ctx, out bool hasBindings, out bool hasContextProvider) {
            int attrCount = attributes.size;

            hasBindings = false;
            hasContextProvider = false;

            if (attrCount == 0) {
                return;
            }

            attributes.Sort((a, b) => a.type - b.type);

            AttributeDefinition2[] attributeDefinitions = attributes.array;

            int statementId = ctx.statementStacks.PeekUnchecked().size;
            // always get it, if not used, remove it again
            
            ctx.AllocateBindingNode();
            
            int startIdx = 0;
            bool hasAttrBindings = false;
            bool hasPropertyBindings = false;
            bool hasStyleBindings = false;
            for (int i = 0; i < attrCount; i++) {
                if (attrCount - 1 == i || attributeDefinitions[i].type != attributeDefinitions[i + 1].type) {
                    switch (attributeDefinitions[i].type) {
                        case AttributeType.Attribute:
                            EmitAttributes(attributes, ctx, startIdx, i + 1, out hasAttrBindings);
                            break;

                        case AttributeType.Property:
                            EmitProperties(outerTemplate, innerTemplate, attributes, ctx, startIdx, i + 1, out hasPropertyBindings);
                            break;

                        case AttributeType.Style:
                            EmitStyles(attributes, ctx, startIdx, i + 1, out hasStyleBindings);
                            break;

                        case AttributeType.Context:
                            EmitContexts(attributeDefinitions, ctx, startIdx, i + 1, out hasContextProvider);
                            break;

                        case AttributeType.ContextVariable:
                            break;

                        case AttributeType.Alias:
                            break;
                    }

                    startIdx = i + 1;
                }
            }


            if (processedType.requiresUpdateFn) {
                
            }
            
            hasBindings = hasAttrBindings || hasPropertyBindings || hasStyleBindings;

            if (!hasBindings && !hasContextProvider) {
                ctx.RemoveStatement(statementId);
            }
            
        }

        private void EmitContexts(AttributeDefinition2[] attributeDefinitions, CompilationContext ctx, int start, int end, out bool hasContextProvider) {
            hasContextProvider = end - start == 1;
            // assume only 1 for now
            if (end - start != 1) {
                throw new Exception("We only support 1 context currently");
            }

            AttributeDefinition2 attr = attributeDefinitions[start];

            ctx.contextProviderStack = ctx.contextProviderStack ?? new LightStack<TemplateContextDefinition>();

            // currently don't support aliases or other refs in this expression type
            LambdaExpression expression = CompileContextExpression(attr.value);

            int expressionId = application.templateData.AddContextProviderLambda(expression);
            
            ctx.contextProviderStack.Push(new TemplateContextDefinition() {
                name = attr.key,
                expressionId = expressionId,
                type = expression.Type,
                id = TemplateContextDefinition.IdGenerator++,
            });

            Expression fnLookup = Expression.Field(ctx.applicationExpr, s_Application_TemplateData);
            Expression arrayLookup = Expression.ArrayAccess(fnLookup, Expression.Constant(expressionId));
            Expression createContext = Expression.Invoke(arrayLookup, ctx.rootParam, ctx.ElementExpr);
            
            // bindingNode.SetContextProvider(scope.application.templateData[24](root, element), id);
            ctx.AddStatement(
                Expression.Call(ctx.GetBindingNode(), s_BindingNode_SetContextProvider, createContext, Expression.Constant(ctx.contextProviderStack.PeekUnchecked().id))
            );
            
        }

        private LambdaExpression CompileContextExpression(string input) {
            linqCompiler = linqCompiler ?? new LinqCompiler();

            linqCompiler.SetSignature<TemplateContext>(
                new Parameter(typeof(UIElement), "root", ParameterFlags.NeverNull),
                new Parameter(typeof(UIElement), "element", ParameterFlags.NeverNull)
            );
            linqCompiler.Return(input);

            LambdaExpression lambdaExpression = linqCompiler.BuildLambda();

            linqCompiler.Reset();

            return lambdaExpression;
        }

        private void EmitProperties(CompiledTemplate template, CompiledTemplate innerTemplate, StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex, out bool hasBindings) {
            int cnt = endIndex - startIdx;

            hasBindings = false;
            if (cnt == 0) return;

            AttributeDefinition2[] attributeDefinitions = attributes.array;

            // todo -- if a template holds its own array for bindings and that array is indexed into instead of shared... we need to do a pre-pass to collect sizes.
            // this means compilation should happen in 2 phases, first gather data & build binding array, second index into that array with LightList.ListSpan
            int startBindingIndex = template.sharedBindings.size;

            bool hasNonSharedBindings = false;
            for (int i = startIdx; i < endIndex; i++) {
                ref AttributeDefinition2 attr = ref attributeDefinitions[i];

                // assume not const & shared for now
                // LambdaExpression bindingExpression = propertyCompiler.BuildLambda(ctx.rootType, ctx.elementType, ctx.contextTree, attr);

                LinqBinding binding = new LinqPropertyBinding();

                // todo -- if binding is const, get its value, store it outside the bindings list, emit code using that value
                template.sharedBindings.Add(binding);

                if (!hasNonSharedBindings) {
                    hasNonSharedBindings = !binding.CanBeShared;
                }

                // at creation time I know the element & root & context which means I can run constant bindings and forget about them

                // difference is all sharing an array or each getting their own since bindings themselves are shared already where possible
                // if all bindings are shared -> use shared array from init data (or maybe better a span of a single array)

                // to support root level bindings we need 2 binding nodes or a switch
                // 2 nodes makes sense, which one runs first? probably the inner one so the outer can overwrite it
            }

            int endBindingIndex = template.sharedBindings.Count;

            if (endBindingIndex - startBindingIndex == 0) {
                return;
            }

            hasBindings = true;

            if (!hasNonSharedBindings) {
                ParameterExpression bindingNode = ctx.ElementExpr;

                // bindingNode = new LinqBindingNode(root, element, ctx.contextStack.Peek());
                ctx.AddStatement(Expression.Assign(bindingNode,
                    Expression.Call(null, s_BindingNodePool_Get,
                        ctx.templateScope,
                        ctx.rootParam,
                        ctx.ElementExpr,
                        Expression.Default(typeof(TemplateContext)),
                        Expression.Default(typeof(LinqBinding)),
                        Expression.Default(typeof(LightList<LinqBinding>.ListSpan)
                        )
                    )
                ));
                // bindingNode.bindings = sharedBindings.CreateSpan(bindingStart, sharedBindings.Count);
//                ctx.AddStatement(Expression.Assign(
//                    Expression.Field(bindingNode, s_BindingNode_Bindings),
//                    Expression.Call(ctx.sharedBindingsExpr, s_LinqBindingList_CreateListSpan, Expression.Constant(startBindingIndex), Expression.Constant(endBindingIndex))
//                ));
//
//                ctx.AddStatement(Expression.Assign(
//                    Expression.Field(ctx.GetTargetElementVariable(), s_ElementBindingNode), bindingNode)
//                );

                Expression lastBindingParent = ctx.bindingNodeStack.Peek();

                ctx.AddStatement(
                    Expression.Call(lastBindingParent, s_BindingNode_AddChild, bindingNode)
                );

                ctx.PushBindingNode(bindingNode);
            }
        }

        private void EmitStyles(StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex, out bool hasBindings) {
            int attrCount = attributes.size;
            AttributeDefinition2[] attributeDefinitions = attributes.array;

            hasBindings = false;
            int cnt = endIndex - startIdx;
            if (cnt == 0) return;

            for (int i = 0; i < attrCount; i++) {
                ref AttributeDefinition2 attr = ref attributeDefinitions[i];

                if (attr.type != AttributeType.Style) {
                    continue;
                }

                if ((attr.flags & AttributeFlags.Binding) == 0) {
                    cnt++;
                }
                else {
                    // LinqBinding binding = styleCompiler.Compile(ctx.rootType, ctx.elementType, ctx.contextTree, attr);


                    // to support root level bindings we need 2 binding nodes or a switch
                    // 2 nodes makes sense, which one runs first? probably the inner one so the outer can overwrite it
                }
            }

            ParameterExpression attributeList = Expression.Parameter(typeof(StructList<ElementAttribute>), "staticAttrList_" + (varId++));

            StyleBindingCompiler cmp = new StyleBindingCompiler();

            cmp.Compile(null, null, attributeDefinitions[0].key, attributeDefinitions[0].value);
        }

        private void EmitAttributes(StructList<AttributeDefinition2> attributes, in CompilationContext ctx, int startIdx, int endIndex, out bool hasBindings) {
            AttributeDefinition2[] attributeDefinitions = attributes.array;

            int cnt = endIndex - startIdx;
            hasBindings = false;

            if (cnt == 0) return;

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

            // todo -- handle non const attributes

            for (int i = startIdx; i < endIndex; i++) {
                ref AttributeDefinition2 attr = ref attributeDefinitions[i];

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

        private static StructList<AttributeDefinition2> MergeAttributes(AttributeDefinition2[] inner, StructList<AttributeDefinition2> outer) {
            StructList<AttributeDefinition2> mergedAttributes = StructList<AttributeDefinition2>.GetMinSize(inner.Length + outer.size);

            // match on type & name, might have to track source also in case of binding context

            // add all outer ones
            mergedAttributes.AddRange(outer);

            int outerCount = outer.size;
            AttributeDefinition2[] mergedArray = mergedAttributes.array;

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

            return mergedAttributes;
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
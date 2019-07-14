using System;
using System.Linq.Expressions;
using System.Reflection;
using UIForia.Elements;
using UIForia.Parsing.Expression;
using UIForia.Systems;
using UIForia.Util;

namespace UIForia.Compilers {

    public struct StoredTemplate {

        public string templateName;
        public int templateId;
        public UIElement closureRoot;

        public StoredTemplate(string templateName, int templateId, UIElement closureRoot) {
            this.templateName = templateName;
            this.templateId = templateId;
            this.closureRoot = closureRoot;
        }

    }

    public class CompilationContext {

        public ProcessedType rootType;
        public ProcessedType elementType;

        public LightStack<Expression> bindingNodeStack;
        public LightList<ParameterExpression> variables;
        public LightStack<LightList<Expression>> statementStacks;
        public LightStack<TemplateContextDefinition> contextProviderStack;

        public Expression rootParam;
        public Expression templateData;
        public Expression templateScope;
        public Expression applicationExpr;
        private Expression slotUsage;
        private ParameterExpression slotUsageListVar;

        private int currentDepth;
        private int maxDepth;
        private int bindingNodeCount;

        private readonly LightStack<ParameterExpression> hierarchyStack;

        public CompilationContext() {
            this.variables = new LightList<ParameterExpression>();
            this.bindingNodeStack = new LightStack<Expression>();
            this.statementStacks = new LightStack<LightList<Expression>>();
            this.hierarchyStack = new LightStack<ParameterExpression>();
        }

        public int AllocateBindingNode() {
            // find binding node for current depth. create it if it doesn't exist. might result in extra vars         
            ParameterExpression bindingNode = null;
            string targetName = "bindingNode_" + currentDepth;
            for (int i = 0; i < variables.Count; i++) {
                if (variables[i].Name == targetName) {
                    bindingNode = variables[i];
                    break;
                }
            }

            if (bindingNode == null) {
                bindingNode = Expression.Parameter(typeof(LinqBindingNode), targetName);
                variables.Add(bindingNode);
            }

            return statementStacks.PeekUnchecked().size - 1;
        }

        public void RemoveStatement(int id) {
            statementStacks.PeekUnchecked().RemoveAt(id);
        }

        public void Clear() {
            this.currentDepth = 0;
            this.maxDepth = 0;
            this.applicationExpr = null;
            this.rootParam = null;
            this.templateData = null;
            this.rootType = default;
            this.elementType = default;
            this.variables.Clear();
            this.bindingNodeStack.Clear();
            this.statementStacks.Clear();
            this.hierarchyStack.Clear();
        }

        public ParameterExpression ParentExpr => hierarchyStack.PeekAtUnchecked(hierarchyStack.Count - 2);
        public ParameterExpression ElementExpr => hierarchyStack.PeekUnchecked();
        public Expression BindingNodeExpr => bindingNodeStack.PeekUnchecked();
        public Expression ParentBindingNodeExpr => bindingNodeStack.PeekAtUnchecked(bindingNodeStack.Count - 2);

        public void Initialize(ParameterExpression parent, Expression bindingNode) {
            hierarchyStack.Push(parent);
            bindingNodeStack.Push(bindingNode);
            PushBlock();
        }

        private static readonly MethodInfo s_StructList_SlotUsage_GetMinSize = typeof(StructList<SlotUsage>).GetMethod(nameof(StructList<SlotUsage>.GetMinSize), new[] {typeof(int)});
        private static readonly MethodInfo s_StructList_SlotUsage_Release = typeof(StructList<SlotUsage>).GetMethod(nameof(StructList<SlotUsage>.Release), BindingFlags.Static);
        private static readonly MethodInfo s_StructList_SlotUsage_ReleaseSelf = typeof(StructList<SlotUsage>).GetMethod(nameof(StructList<SlotUsage>.Release), BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo s_StructList_SlotUsage_Size = typeof(StructList<SlotUsage>).GetField(nameof(StructList<SlotUsage>.size));

        private const string k_SlotUsageListName = "slotUsageList";

        public Expression GetSlotUsageArray(int slotCount) {
            if (slotUsageListVar == null) {
                slotUsageListVar = Expression.Parameter(typeof(StructList<SlotUsage>), k_SlotUsageListName);
                variables.Add(slotUsageListVar);
            }

            Expression getSlotList = Expression.Call(null, s_StructList_SlotUsage_GetMinSize, Expression.Constant(slotCount));

            AddStatement(
                Expression.Assign(slotUsageListVar, getSlotList)
            );

            AddStatement(
                Expression.Assign(
                    Expression.Field(slotUsageListVar, s_StructList_SlotUsage_Size), Expression.Constant(slotCount)
                )
            );

            return slotUsageListVar;
        }

        public void ReleaseSlotUsage() {
            AddStatement(
                Expression.Call(slotUsageListVar, s_StructList_SlotUsage_ReleaseSelf)
            );
        }

        public void PushScope() {
            currentDepth++;

            if (currentDepth > maxDepth) {
                maxDepth = currentDepth;
                ParameterExpression variable = Expression.Parameter(typeof(UIElement), "targetElement_" + currentDepth);
                variables.Add(variable);
                hierarchyStack.Push(variable);
            }
            else {
                string targetName = "targetElement_" + currentDepth;
                for (int i = 0; i < variables.size; i++) {
                    if (variables[i].Type == typeof(UIElement) && variables[i].Name == targetName) {
                        hierarchyStack.Push(variables[i]);
                        return;
                    }
                }

                throw new ArgumentOutOfRangeException();
            }
        }

        public void PushBinding() {
            currentDepth++;

            string targetName = "bindingNode" + currentDepth;
            for (int i = 0; i < variables.size; i++) {
                if (variables[i].Type == typeof(UIElement) && variables[i].Name == targetName) {
                    bindingNodeStack.Push(variables[i]);
                    return;
                }
            }

            ParameterExpression bindingNode = Expression.Parameter(typeof(LinqBindingNode), targetName);
            variables.Add(bindingNode);
            bindingNodeStack.Push(bindingNode);
        }

        public void PopBinding() {
            bindingNodeStack.Pop();
        }

        public void PopScope() {
            currentDepth--;
            hierarchyStack.Pop();
        }

        public ParameterExpression GetVariable(Type type, string name) {
            for (int i = 0; i < variables.Count; i++) {
                if (variables[i].Type == type && variables[i].Name == name) {
                    return variables[i];
                }
            }

            ParameterExpression variable = Expression.Parameter(type, name);
            variables.Add(variable);
            return variable;
        }

        public void PushBlock() {
            statementStacks.Push(LightList<Expression>.Get());
        }

        public BlockExpression PopBlock() {
            LightList<Expression> statements = statementStacks.Pop();
            Expression[] array = statements.ToArray();
            LightList<Expression>.Release(ref statements);
            return Expression.Block(typeof(void), array);
        }

        public BlockExpression Finalize(Type type) {
            LightList<Expression> statements = statementStacks.Pop();
            Expression[] array = statements.ToArray();
            LightList<Expression>.Release(ref statements);
            return Expression.Block(type, variables, array);
        }

        public void AddStatement(Expression expression) {
            this.statementStacks.PeekUnchecked().Add(expression);
        }

    }

}
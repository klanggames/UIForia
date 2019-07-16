using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UIForia.Exceptions;
using UIForia.Parsing.Expression;
using UIForia.Parsing.Expression.AstNodes;
using UIForia.Util;
using Debug = UnityEngine.Debug;

namespace UIForia.Compilers {

    public interface ILinqAliasResolver {

        Expression Resolve(string aliasName, LinqCompiler compiler);

    }

    public class LinqCompiler {

        // todo -- event / delegate subscription
        // todo -- delegate invocation
        // todo -- don't re-access properties that are not auto fields, might be expensive
        // todo    null checking needs to be scope aware, not global since multiple code paths can be checked. also need positive & negative ie != null && == null

        private static readonly ObjectPool<LinqCompiler> s_CompilerPool = new ObjectPool<LinqCompiler>(null, (c) => c.Reset());
        private static readonly ObjectPool<BlockDefinition2> s_BlockPool = new ObjectPool<BlockDefinition2>((b) => b.Spawn(), (b) => b.Release());

        private static readonly MethodInfo StringConcat2 = typeof(string).GetMethod(
            nameof(string.Concat),
            ReflectionUtil.SetTempTypeArray(typeof(string), typeof(string))
        );

        private LinqCompiler parent;
        private Parameter? implicitContext;
        private LightList<BlockDefinition2> blocksToRelease;
        private readonly StructList<Parameter> parameters;
        private readonly LightStack<BlockDefinition2> blockStack;
        private readonly LightList<string> namespaces;
        private readonly HashSet<Expression> wasNullChecked;
        private readonly Dictionary<string, int> variableNames;

        private Type returnType;
        private LabelTarget returnLabel;
        private ParameterExpression returnVar;
        private LambdaExpression lambdaExpression;
        private bool shouldNullCheck = true;
        private bool shouldBoundsCheck = true;
        private int blockIdGen;
        private int subCompilerId;
        private int id;

        private struct AliasResolver {

            public string aliasName;
            public ILinqAliasResolver resolver;

        }

        private Action<LinqCompiler, Expression> nullCheckHandler;
        private StructStack<AliasResolver> aliasResolvers;

        public LinqCompiler() {
            this.parameters = new StructList<Parameter>();
            this.blockStack = new LightStack<BlockDefinition2>();
            this.namespaces = new LightList<string>();
            this.variableNames = new Dictionary<string, int>();
            this.wasNullChecked = new HashSet<Expression>();
            PushBlock();
        }

        public void PushAliasResolver(string aliasName, ILinqAliasResolver resolver) {
            aliasResolvers = aliasResolvers ?? new StructStack<AliasResolver>();
            aliasResolvers.Push(new AliasResolver() {
                aliasName = aliasName[0] == '$' ? aliasName : '$' + aliasName,
                resolver = resolver
            });
        }

        public void PopAliasResolver() {
            if (aliasResolvers == null || aliasResolvers.size == 0) {
                return;
            }

            aliasResolvers.Pop();
        }

        private BlockDefinition2 currentBlock {
            [DebuggerStepThrough] get { return blockStack.PeekUnchecked(); }
        }

        internal string GetUniqueVariableName(string name) {
            if (parent != null) {
                return parent.GetUniqueVariableName(name);
            }

            if (variableNames.TryGetValue(name, out int val)) {
                string retn = name + "_" + val;
                variableNames[name] = ++val;
                return retn;
            }
            else {
                variableNames[name] = 0;
                return name;
            }
        }

        public void Reset() {
            parent = null;
            id = 0;
            subCompilerId = 0;
            lambdaExpression = null;
            returnType = null;
            implicitContext = null;

            wasNullChecked.Clear();
            parameters.Clear();
            blockStack.Clear();
            namespaces.Clear();
            variableNames.Clear();

            returnVar = null;
            returnLabel = null;
            blockIdGen = 0;


            if (blocksToRelease != null) {
                for (int i = 0; i < blocksToRelease.Count; i++) {
                    s_BlockPool.Release(blocksToRelease[i]);
                }

                LightList<BlockDefinition2>.Release(ref blocksToRelease);
            }

            PushBlock();
        }

        private void PushBlock() {
            BlockDefinition2 block = s_BlockPool.Get();
            block.parent = blockStack.Peek();
            block.compiler = this;
            block.blockId = blockIdGen++;
            blockStack.Push(block);
        }

        private BlockExpression PopBlock() {
            Debug.Assert(blockStack.Count > 1);
            BlockDefinition2 popped = blockStack.Pop();
            BlockExpression expr = popped.ToBlockExpression();
            blocksToRelease = blocksToRelease ?? LightList<BlockDefinition2>.Get();
            blocksToRelease.Add(popped);
            return expr;
        }

        public void SetSignature<T>() {
            parameters.Clear();
            returnType = typeof(T);
        }

        public void SetSignature(Type retnType = null) {
            parameters.Clear();
            returnType = retnType ?? typeof(void);
        }

        public void SetSignature<T>(in Parameter p0) {
            parameters.Clear();
            AddParameter(p0);
            returnType = typeof(T);
        }

        public void SetSignature(in Parameter p0, Type retnType = null) {
            parameters.Clear();
            AddParameter(p0);
            returnType = retnType ?? typeof(void);
        }

        public void SetSignature<T>(in Parameter p0, in Parameter p1) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            returnType = typeof(T);
        }

        public void SetSignature(in Parameter p0, in Parameter p1, Type retnType = null) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            returnType = retnType ?? typeof(void);
        }

        public void SetSignature<T>(in Parameter p0, in Parameter p1, in Parameter p2) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            returnType = typeof(T);
        }

        public void SetSignature(in Parameter p0, in Parameter p1, in Parameter p2, Type retnType = null) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            returnType = retnType ?? typeof(void);
        }

        public void SetSignature<T>(in Parameter p0, in Parameter p1, in Parameter p2, in Parameter p3) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            AddParameter(p3);
            returnType = typeof(T);
        }

        public void SetSignature(in Parameter p0, in Parameter p1, in Parameter p2, in Parameter p3, Type retnType = null) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            AddParameter(p3);
            returnType = retnType ?? typeof(void);
        }

        public void SetSignature<T>(in Parameter p0, in Parameter p1, in Parameter p2, in Parameter p3, in Parameter p4) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            AddParameter(p3);
            AddParameter(p4);
            returnType = typeof(T);
        }

        public void SetSignature(in Parameter p0, in Parameter p1, in Parameter p2, in Parameter p3, in Parameter p4, Type retnType = null) {
            parameters.Clear();
            AddParameter(p0);
            AddParameter(p1);
            AddParameter(p2);
            AddParameter(p3);
            AddParameter(p4);
            returnType = retnType ?? typeof(void);
        }

        public ParameterExpression GetParameter(string name) {
            for (int i = 0; i < parameters.Count; i++) {
                if (parameters[i].name == name) {
                    return parameters[i].expression;
                }
            }

            return parent?.GetParameter(name);
        }

        public void SetSignature<T>(IReadOnlyList<Parameter> parameters) {
            this.parameters.Clear();
            for (int i = 0; i < parameters.Count; i++) {
                AddParameter(parameters[i]);
            }

            returnType = typeof(T);
        }

        public void SetSignature(IReadOnlyList<Parameter> parameters, Type retnType = null) {
            this.parameters.Clear();
            for (int i = 0; i < parameters.Count; i++) {
                AddParameter(parameters[i]);
            }

            returnType = retnType ?? typeof(void);
        }

        public void SetNullCheckingEnabled(bool shouldCheck) {
            shouldNullCheck = shouldCheck;
        }

        public void SetOutOfBoundsCheckingEnabled(bool shouldCheck) {
            shouldBoundsCheck = shouldCheck;
        }

        // helpful for debugging to see exactly where a statement was added from
        private void AddStatement(Expression expression) {
            currentBlock.AddStatement(expression);
        }

        public Expression AccessorStatement(Type targetType, string input) {
            return Visit(targetType, ExpressionParser.Parse(input));
        }

        public ParameterExpression AddVariable(Parameter parameter, string value) {
            ParameterExpression variable = currentBlock.AddUserVariable(parameter);
            AddStatement(Expression.Assign(variable, Visit(parameter.type, ExpressionParser.Parse(value))));
            if ((parameter.flags & ParameterFlags.NeverNull) != 0) {
                wasNullChecked.Add(parameter.expression); // todo -- hack :(
            }

            return variable;
        }

        public ParameterExpression AddVariable(Parameter parameter, Expression value) {
            ParameterExpression variable = currentBlock.AddUserVariable(parameter);
            if ((parameter.flags & ParameterFlags.NeverNull) != 0) {
                wasNullChecked.Add(parameter.expression); // todo -- hack :(
            }

            AddStatement(Expression.Assign(variable, value));
            return variable;
        }

        public void SetImplicitContext(ParameterExpression parameterExpression, ParameterFlags flags = 0) {
            if (parameterExpression == null) {
                implicitContext = null;
                return;
            }
            
            implicitContext = new Parameter() {
                expression = parameterExpression,
                flags = flags,
                name = parameterExpression.Name,
                type = parameterExpression.Type
            };
        }

        public ParameterExpression AddVariable(Type type, string name) {
            return currentBlock.AddInternalVariable(type, name);
        }

        public void Log() {
            Debug.Log(Print());
        }

        public string Print() {
            return Mono.Linq.Expressions.CSharp.ToCSharpCode((Expression) BuildLambda());
        }

        public LambdaExpression BuildLambda() {
            if (lambdaExpression != null) {
                return lambdaExpression;
            }

            Debug.Assert(blockStack.Count == 1);

            LightList<Expression> statements = currentBlock.GetStatements();
            LightList<ParameterExpression> variables = currentBlock.GetVariables();

            if (statements == null || statements.Count == 0) {
                throw CompileException.NoStatementsRootBlock();
            }

            if (returnLabel == null) {
                int assignmentCount = 0;
                int lastRetnAssignment = -1;
                for (int i = 0; i < statements.Count; i++) {
                    if (statements[i].NodeType == ExpressionType.Assign) {
                        BinaryExpression assignment = (BinaryExpression) statements[i];
                        if (assignment.Left == returnVar) {
                            assignmentCount++;
                            lastRetnAssignment = i;
                        }
                    }
                }

                if (assignmentCount == 1) {
                    BinaryExpression assign = (BinaryExpression) statements[lastRetnAssignment];
                    statements.RemoveAt(lastRetnAssignment);
                    statements.Add(assign.Right);
                    variables.Remove(returnVar);
                }
            }
            else if (returnType != typeof(void)) {
                statements.Insert(0, Expression.Assign(returnVar, Expression.Default(returnVar.Type)));
                statements.Add(Expression.Label(returnLabel));
                statements.Add(returnVar);
            }
            else {
                statements.Add(Expression.Label(returnLabel));
                statements.Add(Expression.Default(typeof(void)));
            }

            BlockExpression blockExpression;

            if (variables != null && variables.Count > 0) {
                blockExpression = Expression.Block(returnType, variables, statements);
            }
            else {
                blockExpression = Expression.Block(returnType, statements);
            }

            lambdaExpression = Expression.Lambda(blockExpression, MakeParameterArray(parameters));
            return lambdaExpression;
        }

        public void Return(string input = null) {
            if (returnType == typeof(void) && !string.IsNullOrEmpty(input)) {
                throw new InvalidArgumentException("Return expects a null value because the signature of the currently compiled function expects a void return value");
            }

            Return(ExpressionParser.Parse(input));
        }

        public void Statement(string input) {
            AddStatement(Visit(ExpressionParser.Parse(input)));
        }

        private void EnsureReturnLabel() {
            if (returnLabel != null) {
                return;
            }

            if (id == 0) {
                returnLabel = Expression.Label("retn");
            }
            else {
                returnLabel = Expression.Label("retn_" + id);
            }
        }

        public void Return(ASTNode ast) {
            if (returnType == null) {
                throw CompileException.SignatureNotDefined();
            }

            if (returnType == typeof(void)) { // error if input?
                // ensure we have a return label
                // emit a goto for the label
                EnsureReturnLabel();

                AddStatement(Expression.Return(returnLabel));
                return;
            }

            if (returnVar == null) {
                if (id == 0) {
                    returnVar = blockStack.Stack[0].AddInternalVariable(returnType, "retn_val");
                }
                else {
                    returnVar = blockStack.Stack[0].AddInternalVariable(returnType, "retn_val_" + id);
                }
            }

            Expression returnVal = Visit(returnType, ast);
            AddStatement(Expression.Assign(returnVar, returnVal));
        }

        public void Return(string input, out Type retnType) {
            if (returnType == null) {
                throw CompileException.SignatureNotDefined();
            }

            if (returnType == typeof(void)) { // error if input?
                // ensure we have a return label
                // emit a goto for the label
                EnsureReturnLabel();

                AddStatement(Expression.Return(returnLabel));
                retnType = typeof(void);
                return;
            }

            if (returnVar == null) {
                if (id == 0) {
                    returnVar = blockStack.Stack[0].AddInternalVariable(returnType, "retn_val");
                }
                else {
                    returnVar = blockStack.Stack[0].AddInternalVariable(returnType, "retn_val_" + id);
                }
            }

            Expression returnVal = VisitAndGetType(returnType, ExpressionParser.Parse(input), out retnType);
            AddStatement(Expression.Assign(returnVar, returnVal));
        }

        public void Assign(LHSStatementChain left, RHSStatementChain right) {
            if (left.isSimpleAssignment) {
                currentBlock.AddStatement(
                    Expression.Assign(left.targetExpression, right.OutputExpression)
                );
            }
            else {
                LHSAssignment[] assignments = left.assignments.array;
                // this avoid one unneeded copy since undoctored this would write to a local that is unused
                assignments[left.assignments.size - 1].left = right.OutputExpression;
                for (int i = left.assignments.size - 1; i >= 0; i--) {
                    currentBlock.AddStatement(Expression.Assign(assignments[i].right, assignments[i].left));
                }
            }
        }

        public void Assign(string lhsInput, string rhsInput) {
            LHSStatementChain left = AssignableStatement(lhsInput);
            Expression right = Visit(left.targetExpression.Type, ExpressionParser.Parse(rhsInput));
            Assign(left, right);
//
//            if (left.assignments == null) {
//                currentBlock.AddStatement(Expression.Assign(left.targetExpression, expression));
//                return;
//            }
//
//            LHSAssignment[] assignments = left.assignments.array;
//            // this avoid one unneeded copy since undoctored this would write to a local that is unused
//            assignments[left.assignments.size - 1].left = expression;
//            for (int i = left.assignments.size - 1; i >= 0; i--) {
//                currentBlock.AddStatement(Expression.Assign(assignments[i].right, assignments[i].left));
//            }
        }

        public void Assign(LHSStatementChain left, Expression expression) {
            if (left.isSimpleAssignment || left.assignments == null) {
                currentBlock.AddStatement(
                    Expression.Assign(left.targetExpression, expression)
                );
            }
            else {
                LHSAssignment[] assignments = left.assignments.array;
                // this avoid one unneeded copy since undoctored this would write to a local that is unused
                assignments[left.assignments.size - 1].left = expression;
                for (int i = left.assignments.size - 1; i >= 0; i--) {
                    currentBlock.AddStatement(Expression.Assign(assignments[i].right, assignments[i].left));
                }
            }
        }

        public void AddNamespace(string namespaceName) {
            if (string.IsNullOrEmpty(namespaceName)) return;
            if (namespaces.Contains(namespaceName)) return;
            namespaces.Add(namespaceName);
        }

        public void Assign(Expression left, Expression right) {
            currentBlock.AddStatement(Expression.Assign(left, right));
        }


        public void ForEach(IEnumerable list, Action<ParameterExpression> block) { }

        public Expression Constant(object value) {
            return Expression.Constant(value);
        }

        public void IfEqual(string variableName, object value, Action bodyTrue, Action bodyFalse = null) {
            Expression variable = ResolveVariableName(variableName);
            Expression right = null;
            if (value is Expression valueExpression) {
                right = valueExpression;
            }
            else {
                right = Expression.Constant(value);
            }

            IfEqual(variable, right, bodyTrue, bodyFalse);
        }

        public void IfEqual(Expression left, Expression right, Action bodyTrue, Action bodyFalse = null) {
            Expression condition = Expression.Equal(left, right);


            PushBlock();

            bodyTrue();

            BlockExpression bodyBlock = PopBlock();

            if (bodyFalse == null) {
                AddStatement(Expression.IfThen(condition, bodyBlock));
            }
            else {
                PushBlock();

                bodyFalse();

                BlockExpression falseBodyBlock = PopBlock();

                AddStatement(Expression.IfThenElse(condition, bodyBlock, falseBodyBlock));
            }
        }

        public void IfEqual(Expression left, Expression right, Expression trueExpr) {
            Expression condition = Expression.Equal(left, right);
            currentBlock.AddStatement(Expression.IfThen(condition, trueExpr));
        }

        public void IfNotEqual(LHSStatementChain left, Expression right, Action body) {
            Debug.Assert(left != null);
            Debug.Assert(right != null);
            Debug.Assert(body != null);

            Expression condition = Expression.NotEqual(left.targetExpression, right);

            PushBlock();

            body();

            BlockExpression bodyBlock = PopBlock();

            currentBlock.AddStatement(Expression.IfThen(condition, bodyBlock));
        }

        public void IfNotEqual<T>(LHSStatementChain left, Expression right, Action<T> body, T ctx) {
            Debug.Assert(left != null);
            Debug.Assert(right != null);
            Debug.Assert(body != null);

            Expression condition = Expression.NotEqual(left.targetExpression, right);

            PushBlock();

            body(ctx);

            BlockExpression bodyBlock = PopBlock();

            currentBlock.AddStatement(Expression.IfThen(condition, bodyBlock));
        }

        // todo -- see if this works w/ pooling
        private static ParameterExpression[] MakeParameterArray(StructList<Parameter> parameters) {
            ParameterExpression[] parameterExpressions = new ParameterExpression[parameters.size];
            for (int i = 0; i < parameters.size; i++) {
                parameterExpressions[i] = parameters[i].expression;
            }

            return parameterExpressions;
        }

        public T Compile<T>() where T : Delegate {
            return (T) BuildLambda().Compile();
        }

        public void SetNullCheckHandler(Action<LinqCompiler, Expression> nullCHeckHandler) {
            this.nullCheckHandler = nullCHeckHandler;
        }

        public void SetOutOfBoundsCheckHandler() {
            throw new NotImplementedException();
        }

        private void AddParameter(Type type, string name, ParameterFlags flags = 0) {
            // todo validate no name conflicts && no keyword names
            Parameter parameter = new Parameter(type, name, flags);
//            if ((flags & ParameterFlags.Implicit) != 0) {
//                if (implicitContext != null) {
//                    throw new CompileException($"Trying to set parameter {name} as the implicit context but {implicitContext.Value.name} was already set. There can only be one implicit context parameter");
//                }
//
//                implicitContext = parameter;
//            }

            parameters.Add(parameter);
        }

        private void AddParameter(Parameter parameter) {
            // todo validate no name conflicts && no keyword names
//            if ((parameter.flags & ParameterFlags.Implicit) != 0) {
//                if (implicitContext != null) {
//                    throw new CompileException($"Trying to set parameter {parameter.name} as the implicit context but {implicitContext.Value.name} was already set. There can only be one implicit context parameter");
//                }
//
//                implicitContext = parameter;
//            }

            parameters.Add(parameter);
        }

        private Parameter? ResolveVariableName(string variableName) {
            Parameter? variable = currentBlock.ResolveVariable(variableName);
            if (variable != null) {
                return variable;
            }

            for (int i = 0; i < parameters.Count; i++) {
                if (parameters[i].name == variableName) {
                    return parameters[i];
                }
            }

            return parent?.ResolveVariableName(variableName);
        }

        private bool TryResolveVariableName(string variableName, out Parameter parameter) {
            parameter = default;
            Parameter? p = ResolveVariableName(variableName);
            if (p != null) {
                parameter = p.Value;
                return true;
            }

            return false;
        }

        public LHSStatementChain AssignableStatement(string input) {
            ASTNode astRoot = ExpressionParser.Parse(input);

            LHSStatementChain retn = new LHSStatementChain();

            if (astRoot.type == ASTNodeType.Identifier) {
                IdentifierNode idNode = (IdentifierNode) astRoot;

                if (idNode.IsAlias) {
                    throw new InvalidLeftHandStatementException("alias cannot be used in a LHS expression", input);
                }

                if (implicitContext != null) {
                    // todo -- still wrong
//                    Expression last = MemberAccess(implicitContext.Value.expression, idNode.name);
//                    Expression variable = currentBlock.AddInternalVariable(last.Type, idNode.name + "_assign");
//                    currentBlock.AddStatement(Expression.Assign(variable, last));
//                    retn.AddAssignment(variable, last);
                    retn.isSimpleAssignment = true;
                    retn.targetExpression = MemberAccess(implicitContext.Value.expression, idNode.name);
                    return retn;

//                    if (TryResolveInstanceOrStaticMemberAccess(implicitContext.Value.expression, idNode.name, out Expression expression)) {
//                        retn.isSimpleAssignment = true;
//                        retn.targetExpression = expression;
//                        return retn;
//                    }
                }

                ParameterExpression head = ResolveVariableName(idNode.name);

                if (head == null) {
                    throw CompileException.UnresolvedIdentifier(idNode.name);
                }

                retn.isSimpleAssignment = true;
                retn.targetExpression = head;
                return retn;
            }
            else if (astRoot.type == ASTNodeType.AccessExpression) {
                MemberAccessExpressionNode memberNode = (MemberAccessExpressionNode) astRoot;

                LightList<ProcessedPart> parts = ProcessASTParts(memberNode.parts);

                // only supports dot access for now and only variables, no null checking

                ParameterExpression variable = ResolveVariableName(memberNode.identifier);

                if (variable == null) {
                    throw CompileException.UnresolvedIdentifier(memberNode.identifier);
                }

                if (parts.Count == 1) {
                    ProcessedPart part = parts[0];
                    if (part.type != PartType.DotAccess) {
                        throw new NotImplementedException("Assign statements can only contain dot notation currently, no indexing or invoking");
                    }

                    retn.targetExpression = MemberAccess(variable, part.name);
                    return retn;
                }

                for (int i = 0; i < parts.Count; i++) {
                    ref ProcessedPart part = ref parts.Array[i];
                    switch (part.type) {
                        case PartType.DotAccess:
                            Expression last = MemberAccess(variable, part.name);
                            variable = currentBlock.AddInternalVariable(last.Type, part.name + "_assign");
                            currentBlock.AddStatement(Expression.Assign(variable, last));
                            retn.AddAssignment(variable, last);
                            continue;
                    }

                    throw new InvalidLeftHandStatementException(astRoot.type.ToString(), input);
                }

                retn.targetExpression = retn.assignments[retn.assignments.size - 1].left;
            }
            else {
                throw new InvalidLeftHandStatementException(astRoot.type.ToString(), input);
            }

            return retn;
        }

        private bool TryResolveInstanceOrStaticMemberAccess(Expression head, string fieldOrPropertyName, out Expression accessExpression) {
            if (ReflectionUtil.IsField(head.Type, fieldOrPropertyName, out FieldInfo fieldInfo)) {
                if (!fieldInfo.IsPublic) {
                    throw CompileException.AccessNonReadableField(head.Type, fieldInfo);
                }

                if (fieldInfo.IsStatic || fieldInfo.IsInitOnly) {
                    accessExpression = Expression.MakeMemberAccess(null, fieldInfo);
                    return true;
                }

                accessExpression = Expression.MakeMemberAccess(head, fieldInfo);
                return true;
            }

            if (ReflectionUtil.IsProperty(head.Type, fieldOrPropertyName, out PropertyInfo propertyInfo)) {
                if (!propertyInfo.CanRead || !propertyInfo.GetMethod.IsPublic) {
                    throw CompileException.AccessNonReadableProperty(head.Type, propertyInfo);
                }

                if (propertyInfo.GetMethod.IsStatic) {
                    accessExpression = Expression.MakeMemberAccess(null, propertyInfo);
                    return true;
                }

                accessExpression = Expression.MakeMemberAccess(head, propertyInfo);
                return true;
            }

            throw new InvalidArgumentException();
        }

        private Expression MakeFieldAccess(Expression head, FieldInfo fieldInfo) {
            if (!fieldInfo.IsPublic) {
                throw CompileException.AccessNonReadableField(head.Type, fieldInfo);
            }

            // current expression = member access
            // current expression.isWritten = false;
            return Expression.MakeMemberAccess(head, fieldInfo);
        }

        private Expression MakePropertyAccess(Expression head, PropertyInfo propertyInfo) {
            if (!propertyInfo.CanRead || !propertyInfo.GetMethod.IsPublic) {
                throw CompileException.AccessNonReadableProperty(head.Type, propertyInfo);
            }

            if (propertyInfo.GetMethod.IsStatic) {
                return Expression.MakeMemberAccess(null, propertyInfo);
            }

            return Expression.MakeMemberAccess(head, propertyInfo);
        }


        private Expression MakeMethodCall(Expression head, LightList<MethodInfo> methodInfos, LightList<ASTNode> arguments) {
            Expression[] args = new Expression[arguments.Count];

            for (int i = 0; i < arguments.Count; i++) {
                args[i] = Visit(arguments[i]);
            }

            head = NullCheck(head);

            MethodInfo info = ExpressionUtil.SelectEligibleMethod(methodInfos, args, out StructList<ExpressionUtil.ParameterConversion> conversions);

            if (info == null || !info.IsPublic) {
                throw CompileException.UnresolvedMethodOverload(head.Type, methodInfos[0].Name, args.Select(a => a.Type).ToArray());
            }

            if (conversions.size > args.Length) {
                Array.Resize(ref args, conversions.size);
            }

            for (int i = 0; i < conversions.size; i++) {
                args[i] = conversions[i].Convert();
            }

            return Expression.Call(head, info, args);
        }


        private Expression PerformIndex(Expression head, Expression index, Type type = null) {
            type = type ?? head.Type;
            if (type.IsArray) {
                return Expression.ArrayIndex(head, index);
            }
            else {
                Expression indexExpression = FindIndexExpression(type, index, out PropertyInfo indexProperty);
                return Expression.MakeIndex(head, indexProperty, new[] {indexExpression});
            }
        }

        private Expression IndexDictionary(Expression head, ASTNode indexNode, bool isNullableAccess, LightList<ProcessedPart> parts, ref int start) {
            Expression indexExpression = Visit(null, indexNode);
            Expression indexer = indexExpression;

            if (!(indexExpression is ParameterExpression) && !(indexExpression is ConstantExpression)) {
                indexer = currentBlock.AddInternalVariable(indexExpression.Type, "indexer");
                currentBlock.AddStatement(Expression.Assign(indexer, indexExpression));
            }

            MethodInfo info = head.Type.GetMethod("TryGetValue");

            Debug.Assert(info != null);

            Type t = head.Type.GetGenericArguments()[1];
            ParameterExpression outVar = currentBlock.AddInternalVariable(t, "outVar");

            EnsureReturnLabel();

            if (isNullableAccess) {
                start++;

                Expression nullableAccessVar = null;

                BinaryExpression condition = Expression.NotEqual(head, Expression.Constant(null));

                PushBlock();

                Expression continuation = VisitAccessExpressionParts(outVar, parts, ref start);
                nullableAccessVar = blockStack.PeekAtUnchecked(blockStack.Count - 2).AddInternalVariable(ReflectionUtil.GetNullableType(continuation.Type), "nullableAccess");

                blockStack.PeekAtUnchecked(blockStack.Count - 2).AddStatement(Expression.Assign(nullableAccessVar, Expression.Default(nullableAccessVar.Type)));

                MethodCallExpression call2 = Expression.Call(head, info, indexer, outVar);
                PushBlock();

                AddStatement(Expression.Assign(nullableAccessVar, Expression.Convert(continuation, nullableAccessVar.Type)));

                BlockExpression innerBlock = PopBlock();
                AddStatement(Expression.IfThenElse(Expression.Equal(call2, Expression.Constant(true)), innerBlock, Expression.Block(typeof(void), Expression.Goto(returnLabel))));

                Expression outerBlock = PopBlock();
                AddStatement(Expression.IfThen(condition, outerBlock));

                return nullableAccessVar;
            }

            PushBlock();
            // todo -- only do this if bounds checking enabled
            // todo -- allow user override behavior
            AddStatement(Expression.Goto(returnLabel));

            BlockExpression block = PopBlock();

            MethodCallExpression call = Expression.Call(head, info, indexer, outVar);
            AddStatement(Expression.IfThen(Expression.NotEqual(call, Expression.Constant(true)), block));

            return outVar;
        }

        private Expression IndexNonList(Expression head, LightList<ASTNode> arguments, bool isNullableAccess, LightList<ProcessedPart> parts, ref int start) {
            if (arguments.Count == 1) {
                Expression indexExpression = Visit(null, arguments[0]);

                Expression indexer = indexExpression;

                if (!(indexExpression is ParameterExpression) && !(indexExpression is ConstantExpression)) {
                    indexer = currentBlock.AddInternalVariable(indexExpression.Type, "indexer");
                    currentBlock.AddStatement(Expression.Assign(indexer, indexExpression));
                }

                if (isNullableAccess) {
                    start++;

                    Expression nullableAccessVar = null;

                    BinaryExpression condition = Expression.NotEqual(head, Expression.Constant(null));
                    Expression block = null;

                    Expression idx = PerformIndex(head, indexer);

                    // if more parts, continue visiting here since we need to final type to create our nullable variable
                    Expression continuation = VisitAccessExpressionParts(idx, parts, ref start);
                    nullableAccessVar = AddVariable(ReflectionUtil.GetNullableType(continuation.Type), "nullableAccess");

                    AddStatement(Expression.Assign(nullableAccessVar, Expression.Default(nullableAccessVar.Type)));

                    BinaryExpression assign = Expression.Assign(nullableAccessVar, Expression.Convert(continuation, nullableAccessVar.Type));

                    if (shouldBoundsCheck && (!ResolveParameter(head, out Parameter p) || (p.flags & ParameterFlags.NeverOutOfBounds) == 0)) {
                        PushBlock();

                        head = BoundsCheck(head, ref indexer);

                        AddStatement(assign);

                        block = PopBlock();
                    }
                    else {
                        block = Expression.Block(typeof(void), assign);
                    }

                    AddStatement(Expression.IfThen(condition, block));

                    return nullableAccessVar;
                }

                head = NullCheck(head);

                indexExpression = FindIndexExpression(head.Type, indexExpression, out PropertyInfo indexProperty);
                return Expression.MakeIndex(head, indexProperty, new[] {indexer});
            }
            else {
                throw new NotImplementedException("Expressions only support indexed access with one argument");
            }
        }

        private Expression IndexIList(Expression head, Expression indexExpression, bool isNullableAccess, LightList<ProcessedPart> parts, ref int start) {
            Expression indexer = indexExpression;

            Type headType = head.Type;

            if (isNullableAccess) {
                // Nullable<T> result = default(T?);
                // if head != null
                //     result = array[index]; (bounds checked)
                // return result


                start++;

                Expression nullableAccessVar = null;

                if (!wasNullChecked.Contains(head)) {
                    wasNullChecked.Add(head);
                }

                BinaryExpression condition = Expression.NotEqual(head, Expression.Constant(null));
                Expression block = null;

                // if more parts, continue visiting here since we need to final type to create our nullable variable

                if (RequiresBoundsCheck(head)) {
                    PushBlock();

                    head = BoundsCheck(head, ref indexer);

                    Expression idx = PerformIndex(head, indexer, headType);

                    Expression continuation = VisitAccessExpressionParts(idx, parts, ref start);

                    nullableAccessVar = blockStack.PeekRelativeUnchecked(2).AddInternalVariable(ReflectionUtil.GetNullableType(continuation.Type), "nullableAccess");

                    blockStack.PeekRelativeUnchecked(2).AddStatement(Expression.Assign(nullableAccessVar, Expression.Default(nullableAccessVar.Type)));

                    BinaryExpression assign = Expression.Assign(nullableAccessVar, Expression.Convert(continuation, nullableAccessVar.Type));

                    AddStatement(assign);

                    block = PopBlock();
                }
                else {
                    Expression idx = PerformIndex(head, indexer, headType);
                    Expression continuation = VisitAccessExpressionParts(idx, parts, ref start);

                    nullableAccessVar = AddVariable(ReflectionUtil.GetNullableType(continuation.Type), "nullableAccess");
                    AddStatement(Expression.Assign(nullableAccessVar, Expression.Default(nullableAccessVar.Type)));
                    block = Expression.Block(typeof(void), Expression.Assign(nullableAccessVar, Expression.Convert(continuation, nullableAccessVar.Type)));
                }

                AddStatement(Expression.IfThen(condition, block));

                return nullableAccessVar;
            }

            head = NullCheck(head);

            head = BoundsCheck(head, ref indexer);

            return PerformIndex(head, indexer, headType);
        }

        private Expression Invoke(Expression head, LightList<ASTNode> arguments) {
            head = NullCheck(head);

            Expression[] args = new Expression[arguments.Count];

            Type delegateType = head.Type;

            ParameterInfo[] parameterInfos = delegateType.GetMethod("Invoke").GetParameters();

            for (int i = 0; i < arguments.Count; i++) {
                args[i] = Visit(parameterInfos[i].ParameterType, arguments[i]);
            }

            return Expression.Invoke(head, args);
        }

        private Expression MemberAccess(Expression head, string fieldOrPropertyName) {
            MemberInfo memberInfo = ReflectionUtil.GetFieldOrProperty(head.Type, fieldOrPropertyName);

            if (memberInfo == null) {
                throw new CompileException($"Type {head.Type} does not declare an accessible instance field or property with the name {fieldOrPropertyName}");
            }

            // cascade a null check, if we are looking up a value and trying to read from something that is null,
            // then we jump to the end of value chain and use default(inputType) as a final value
            head = NullCheck(head);

            if (memberInfo is FieldInfo fieldInfo) {
                return MakeFieldAccess(head, fieldInfo);
            }
            else if (memberInfo is PropertyInfo propertyInfo) {
                return MakePropertyAccess(head, propertyInfo);
            }
            else {
                // should never hit this
                throw new InvalidArgumentException();
            }
        }

        private static Expression ParseEnum(Type type, string value) {
            try {
                return Expression.Constant(Enum.Parse(type, value));
            }
            catch (Exception) {
                throw CompileException.UnknownEnumValue(type, value);
            }
        }

        private static bool ResolveNamespaceChain(MemberAccessExpressionNode node, out int start, out string resolvedNamespace) {
            // since we don't know where the namespace stops and the type begins, when we cannot immediately resolve a variable or type from a member access,
            // we need to walk the chain and resolve as we go.

            LightList<string> names = LightList<string>.Get();

            names.Add(node.identifier);

            for (int i = 0; i < node.parts.Count; i++) {
                if ((node.parts[i] is DotAccessNode dotAccessNode)) {
                    names.Add(dotAccessNode.propertyName);
                }
                else {
                    break;
                }
            }

            string BuildString(int count) {
                string retn = "";

                for (int i = 0; i < count; i++) {
                    retn += names[i] + ".";
                }

                retn += names[count];

                return retn;
            }

            for (int i = names.Count - 2; i >= 0; i--) {
                string check = BuildString(i);
                if (TypeProcessor.IsNamespace(check)) {
                    LightList<string>.Release(ref names);
                    resolvedNamespace = check;
                    start = i;
                    return true;
                }
            }

            LightList<string>.Release(ref names);
            start = 0;
            resolvedNamespace = string.Empty;
            return false;
        }

        private bool ResolveTypeChain(Type startType, LightList<ProcessedPart> parts, ref int start, out Type tailType) {
            if (start >= parts.Count || startType.IsEnum) {
                tailType = null;
                return false;
            }

            if (parts[start].type != PartType.DotAccess) {
                tailType = null;
                return false;
            }

            Type[] nestedTypes = startType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);

            string targetName = parts[start].name;

            GenericTypePathNode genericNode = null;
            // if we are looking for a generic type we need to be sure not to pick up a non generic with the same name
            // we also need to be sure the generic argument count is equal
            if (start + 1 < parts.Count - 1 && parts[start + 1].type == PartType.Generic) {
                start++;
                targetName += "`" + parts[start].generic.genericPath.generics.Count;
                genericNode = parts[start].generic;
            }

            for (int i = 0; i < nestedTypes.Length; i++) {
                if (nestedTypes[i].Name == targetName) {
                    tailType = nestedTypes[i];

                    if (genericNode != null) {
                        tailType = TypeProcessor.ResolveNestedGenericType(startType, tailType, genericNode.genericPath, namespaces);
                    }

                    start++;
                    int recursedStart = start;
                    if (ResolveTypeChain(tailType, parts, ref recursedStart, out Type recursedTailType)) {
                        tailType = recursedTailType;
                        start = recursedStart;
                    }

                    return true;
                }
            }

            tailType = null;
            return false;
        }

        private Expression MakeStaticMethodCall(Type type, string propertyName, LightList<ASTNode> parameters) {
            Expression[] args = new Expression[parameters.Count];

            for (int i = 0; i < parameters.Count; i++) {
                args[i] = Visit(parameters[i]);
            }

            if (!ReflectionUtil.HasStaticMethod(type, propertyName, out LightList<MethodInfo> methodInfos)) {
                throw CompileException.UnresolvedStaticMethod(type, propertyName);
            }

            MethodInfo info = ExpressionUtil.SelectEligibleMethod(methodInfos, args, out StructList<ExpressionUtil.ParameterConversion> conversions);

            if (info == null) {
                throw CompileException.UnresolvedMethodOverload(type, propertyName, args.Select(a => a.Type).ToArray());
            }

            if (conversions.size > args.Length) {
                Array.Resize(ref args, conversions.size);
            }

            for (int i = 0; i < conversions.size; i++) {
                args[i] = conversions[i].Convert();
            }

            LightList<MethodInfo>.Release(ref methodInfos);

            return Expression.Call(null, info, args);
        }

        private static Expression MakeStaticConstOrEnumMemberAccess(Type type, string propertyName) {
            if (type.IsEnum) {
                return ParseEnum(type, propertyName);
            }

            if (ReflectionUtil.HasConstOrStaticMember(type, propertyName, out MemberInfo memberInfo)) {
                if (memberInfo is FieldInfo fieldInfo && !fieldInfo.IsPublic) {
                    throw CompileException.AccessNonReadableStaticOrConstField(type, propertyName);
                }
                else if (memberInfo is PropertyInfo propertyInfo) {
                    if (!propertyInfo.CanRead) {
                        throw CompileException.AccessNonReadableStaticProperty(type, propertyName);
                    }

                    if (!propertyInfo.GetMethod.IsPublic) {
                        throw CompileException.AccessNonPublicStaticProperty(type, propertyName);
                    }
                }

                return Expression.MakeMemberAccess(null, memberInfo);
            }

            throw CompileException.UnknownStaticOrConstMember(type, propertyName);
        }


        private Expression VisitStaticAccessExpression(Type type, LightList<ProcessedPart> parts, int start) {
            Expression head = null;

            if (ResolveTypeChain(type, parts, ref start, out Type subType)) {
                type = subType;
            }

            if (!type.IsPublic && !type.IsNestedPublic) {
                throw CompileException.NonPublicType(type);
            }

            ref ProcessedPart part = ref parts.Array[start];

            switch (part.type) {
                case PartType.DotAccess:
                    head = MakeStaticConstOrEnumMemberAccess(type, part.name);
                    break;

                case PartType.DotInvoke:
                    head = MakeStaticMethodCall(type, part.name, part.arguments);
                    break;

                case PartType.DotIndex:

                    head = MakeStaticConstOrEnumMemberAccess(type, part.name);

                    Type lastValueType = head.Type;

                    if (typeof(IList).IsAssignableFrom(lastValueType)) {
                        if (lastValueType.IsArray && lastValueType.GetArrayRank() != 1) {
                            throw new NotSupportedException("Expressions do not support multidimensional arrays yet");
                        }

                        head = IndexIList(head, Visit(typeof(int), part.arguments[0]), part.isNullableAccess, parts, ref start);
                    }
                    else if (lastValueType.IsGenericType && lastValueType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                        head = IndexDictionary(head, part.arguments[0], part.isNullableAccess, parts, ref start);
                    }
                    else {
                        head = IndexNonList(head, part.arguments, part.isNullableAccess, parts, ref start);
                    }

                    break;
                default:
                    throw new NotImplementedException();
            }

            start++;

            if (start == parts.Count) {
                return head;
            }

            return VisitAccessExpressionParts(head, parts, ref start);
        }

        private bool TryCreateVariableExpression(Expression expressionHead, string propertyRead, out Expression expression) {
            Type exprType = expressionHead.Type;
            if (ReflectionUtil.IsField(exprType, propertyRead)) {
                expression = MemberAccess(expressionHead, propertyRead);
                return true;
            }
            else if (ReflectionUtil.IsProperty(exprType, propertyRead)) {
                expression = MemberAccess(expressionHead, propertyRead);
                return true;
            }
            else if (ReflectionUtil.HasInstanceMethod(exprType, propertyRead, out LightList<MethodInfo> methodInfos)) {
                throw new NotImplementedException();
            }

            expression = null;
            return false;
        }

        private struct ProcessedPart {

            public string name;
            public PartType type;
            public LightList<ASTNode> arguments;
            public GenericTypePathNode generic;
            public bool isNullableAccess;

        }

        private enum PartType {

            DotAccess,
            DotInvoke,
            DotIndex,
            Invoke,
            Index,
            Generic

        }

        private static LightList<ProcessedPart> ProcessASTParts(LightList<ASTNode> parts) {
            LightList<ProcessedPart> retn = LightList<ProcessedPart>.Get();

            for (int i = 0; i < parts.Count; i++) {
                if (parts[i] is DotAccessNode dotAccessNode) {
                    if (i + 1 < parts.Count) {
                        if (parts[i + 1] is InvokeNode invokeNode) {
                            retn.Add(new ProcessedPart() {
                                type = PartType.DotInvoke,
                                name = dotAccessNode.propertyName,
                                arguments = invokeNode.parameters,
                                isNullableAccess = false
                            });
                            i++;
                            continue;
                        }
                        else if (parts[i + 1] is IndexNode indexNode) {
                            retn.Add(new ProcessedPart() {
                                type = PartType.DotIndex,
                                name = dotAccessNode.propertyName,
                                arguments = indexNode.arguments,
                                isNullableAccess = indexNode.isNullableAccess
                            });
                            i++;
                            continue;
                        }
                    }

                    retn.Add(new ProcessedPart() {
                        type = PartType.DotAccess,
                        name = dotAccessNode.propertyName,
                        arguments = null,
                        isNullableAccess = dotAccessNode.isNullableAccess
                    });

                    continue;
                }
                else if (parts[i] is IndexNode idx) {
                    retn.Add(new ProcessedPart() {
                        type = PartType.Index,
                        name = null,
                        arguments = idx.arguments,
                        isNullableAccess = idx.isNullableAccess
                    });
                    continue;
                }
                else if (parts[i] is InvokeNode invoke) {
                    retn.Add(new ProcessedPart() {
                        type = PartType.Invoke,
                        name = null,
                        arguments = invoke.parameters,
                        isNullableAccess = false
                    });
                    continue;
                }
                else if (parts[i] is GenericTypePathNode genericTypePathNode) {
                    retn.Add(new ProcessedPart() {
                        type = PartType.Generic,
                        name = null,
                        arguments = null,
                        generic = genericTypePathNode,
                        isNullableAccess = false
                    });
                    continue;
                }

                throw new InvalidArgumentException();
            }

            return retn;
        }

        private Expression ResolveAlias(string aliasName) {
            if (aliasResolvers == null) {
                throw CompileException.MissingAliasResolver(aliasName);
            }

            for (int i = aliasResolvers.size - 1; i >= 0; i--) {
                if (aliasResolvers.array[i].aliasName == aliasName) {
                    return aliasResolvers.array[i].resolver.Resolve(aliasName, this);
                }
            }

            throw CompileException.MissingAliasResolver(aliasName);
        }

        private Expression VisitAccessExpression(MemberAccessExpressionNode accessNode) {
            LightList<ProcessedPart> parts = ProcessASTParts(accessNode.parts);

            // assume not an alias for now, aliases will be resolved by user visit code

            int start = 0;
            string accessRootName = accessNode.identifier;
            string resolvedNamespace;

            if (accessRootName[0] == '$') {
                return VisitAccessExpressionParts(ResolveAlias(accessRootName), parts, ref start);
            }

            // if implicit is defined give it priority
            // then check variables
            // then check types
            if (implicitContext.HasValue) {
                if (TryCreateVariableExpression(implicitContext.Value.expression, accessNode.identifier, out Expression head)) {
                    return VisitAccessExpressionParts(head, parts, ref start);
                }
            }

            if (TryResolveVariableName(accessNode.identifier, out Parameter variable)) {
                // thing.function() -> head -> invoke node
                // thing.function().function()[i]()
                // dot access type -> invoke, index, fieldproperty
                return VisitAccessExpressionParts(variable, parts, ref start);
            }

            if (ResolveNamespaceChain(accessNode, out start, out resolvedNamespace)) {
                // if a namespace chain was resolved then we have to resolve a type next which means an enum, static, or const value
                if ((!(accessNode.parts[start] is DotAccessNode dotAccessNode))) {
                    // namespace[index] and namespace() are both invalid. If we hit that its a hard error
                    throw CompileException.InvalidNamespaceOperation(resolvedNamespace, accessNode.parts[start].GetType());
                }

                accessRootName = dotAccessNode.propertyName;
                start++;

                if (start >= accessNode.parts.Count) {
                    throw CompileException.InvalidAccessExpression();
                }

                Type type = TypeProcessor.ResolveType(accessRootName, resolvedNamespace);

                if (type == null) {
                    throw CompileException.UnresolvedType(new TypeLookup(accessRootName), namespaces);
                }

                if (!(accessNode.parts[start] is DotAccessNode)) {
                    // type[index] and type() are both invalid. If we hit that its a hard error
                    throw CompileException.InvalidIndexOrInvokeOperator(); //resolvedNamespace, accessNode.parts[start].GetType());
                }

                return VisitStaticAccessExpression(type, parts, start);
            }
            else {
                // check for generic access too
                Type type = TypeProcessor.ResolveType(accessRootName, namespaces);

                if (type == null) {
                    throw CompileException.UnresolvedIdentifier(accessRootName);
                }

                return VisitStaticAccessExpression(type, parts, start);
            }
        }

        private bool ResolveParameter(Expression parameterExpression, out Parameter parameter) {
            if (!(parameterExpression is ParameterExpression)) {
                parameter = default;
                return false;
            }

            for (int i = 0; i < parameters.size; i++) {
                if (parameters[i].expression == parameterExpression) {
                    parameter = parameters[i];
                    return true;
                }
            }

            if (parent != null) {
                return parent.ResolveParameter(parameterExpression, out parameter);
            }

            parameter = default;
            return false;
        }

        private Expression VisitAccessExpressionParts(Expression head, LightList<ProcessedPart> parts, ref int start) {
            Expression lastExpression = head;
            // need a variable when we hit a reference type
            // structs do not need intermediate variables, in fact due to the copy cost its best not to have them for structs at all
            // todo -- properties should always be read into fields, we assume they are more expensive and worth local caching even when structs

            for (int i = start; i < parts.Count; i++) {
                ref ProcessedPart part = ref parts.Array[i];
                switch (part.type) {
                    case PartType.DotAccess: {
                        lastExpression = MemberAccess(lastExpression, part.name);
                        break;
                    }

                    case PartType.DotInvoke:
                        LightList<MethodInfo> methods = ReflectionUtil.GetInstanceMethodsWithName(lastExpression.Type, part.name);
                        lastExpression = MakeMethodCall(lastExpression, methods, part.arguments);
                        LightList<MethodInfo>.Release(ref methods);
                        break;

                    case PartType.Invoke:
                        lastExpression = Invoke(lastExpression, part.arguments);
                        break;

                    case PartType.Index: {
                        Type lastValueType = lastExpression.Type;

                        if (typeof(IList).IsAssignableFrom(lastValueType)) {
                            if (lastValueType.IsArray && lastValueType.GetArrayRank() != 1) {
                                throw new NotSupportedException("Expressions do not support multidimensional arrays yet");
                            }

                            lastExpression = IndexIList(lastExpression, Visit(typeof(int), part.arguments[0]), part.isNullableAccess, parts, ref i);
                        }
                        else if (lastValueType.IsGenericType && lastValueType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                            lastExpression = IndexDictionary(lastExpression, part.arguments[0], part.isNullableAccess, parts, ref i);
                        }
                        else {
                            lastExpression = IndexNonList(lastExpression, part.arguments, part.isNullableAccess, parts, ref i);
                        }

                        break;
                    }

                    case PartType.DotIndex: {
                        // todo -- also no support for multiple index properties right now, parser needs to accept a comma list for that to work

                        lastExpression = MemberAccess(lastExpression, part.name);
                        Type lastValueType = lastExpression.Type;

                        if (typeof(IList).IsAssignableFrom(lastValueType)) {
                            if (lastValueType.IsArray && lastValueType.GetArrayRank() != 1) {
                                throw new NotSupportedException("Expressions do not support multidimensional arrays yet");
                            }

                            lastExpression = IndexIList(lastExpression, Visit(typeof(int), part.arguments[0]), part.isNullableAccess, parts, ref i);
                        }
                        else if (lastValueType.IsGenericType && lastValueType.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                            lastExpression = IndexDictionary(lastExpression, part.arguments[0], part.isNullableAccess, parts, ref i);
                        }
                        else {
                            lastExpression = IndexNonList(lastExpression, part.arguments, part.isNullableAccess, parts, ref i);
                        }

                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return lastExpression;
        }

        private bool RequiresBoundsCheck(Expression head) {
            return shouldBoundsCheck && (!ResolveParameter(head, out Parameter parameter) || (parameter.flags & ParameterFlags.NeverOutOfBounds) == 0);
        }

        private Expression BoundsCheck(Expression head, ref Expression indexExpression) {
            if (!RequiresBoundsCheck(head)) {
                return head;
            }

            Expression indexer = indexExpression;

            if (!(indexExpression is ParameterExpression) && !(indexExpression is ConstantExpression)) {
                indexer = AddVariable(indexExpression.Type, "indexer");
                AddStatement(Expression.Assign(indexer, indexExpression));
                indexExpression = indexer;
            }

            if (!(head is ParameterExpression) && !(head is ConstantExpression)) {
                ParameterExpression newHead = AddVariable(head.Type, "toBoundsCheck");
                AddStatement(Expression.Assign(newHead, head));
                if (wasNullChecked.Contains(head)) {
                    wasNullChecked.Add(newHead);
                }

                head = newHead;
            }

            EnsureReturnLabel();

            Expression lengthExpr = null;

            if (head.Type.IsArray) {
                lengthExpr = Expression.ArrayLength(head);
            }
            else {
                lengthExpr = MemberAccess(head, "Count");
            }

            if (indexer is ConstantExpression constantExpression && constantExpression.Value is int intVal) {
                if (intVal < 0) {
                    currentBlock.AddStatement(Expression.Goto(returnLabel));
                    return head;
                }

                AddStatement(Expression.IfThen(
                    Expression.GreaterThanOrEqual(indexer, lengthExpr),
                    Expression.Goto(returnLabel)
                ));
                return head;
            }

            ConditionalExpression expr = Expression.IfThen(
                Expression.OrElse(
                    Expression.LessThan(indexer, Expression.Constant(0)),
                    Expression.GreaterThanOrEqual(indexer, lengthExpr)
                ),
                Expression.Goto(returnLabel)
            );
            currentBlock.AddStatement(expr);
            return head;
        }

        private bool RequiresNullCheck(Expression head) {
            return shouldNullCheck &&
                   head.Type.IsClass &&
                   !wasNullChecked.Contains(head) &&
                   (!ResolveParameter(head, out Parameter parameter) || (parameter.flags & ParameterFlags.NeverNull) == 0);
        }

        private Expression NullCheck(Expression variable) {
            if (!RequiresNullCheck(variable)) {
                return variable;
            }

            EnsureReturnLabel();

            Expression nullCheck = variable;

            if (!(variable is ConstantExpression) && !(variable is ParameterExpression)) {
                nullCheck = AddVariable(variable.Type, "nullCheck");
                Assign(nullCheck, variable);
                wasNullChecked.Add(nullCheck);
            }

            if (!wasNullChecked.Contains(variable)) {
                wasNullChecked.Add(variable);
            }

            if (nullCheckHandler != null) {
                shouldNullCheck = false;
                PushBlock();
                nullCheckHandler.Invoke(this, variable);
                AddStatement(Expression.Goto(returnLabel));
                BlockExpression blockExpression = PopBlock();
                AddStatement(Expression.IfThen(Expression.Equal(nullCheck, Expression.Constant(null)), blockExpression));
                shouldNullCheck = true;
            }
            else {
                AddStatement(Expression.IfThen(Expression.Equal(nullCheck, Expression.Constant(null)), Expression.Goto(returnLabel)));
            }

            return nullCheck;
        }

        private static Expression FindIndexExpression(Type type, Expression indexExpression, out PropertyInfo indexProperty) {
            IList<ReflectionUtil.IndexerInfo> indexedProperties = ReflectionUtil.GetIndexedProperties(type, ListPool<ReflectionUtil.IndexerInfo>.Get());
            List<ReflectionUtil.IndexerInfo> l = (List<ReflectionUtil.IndexerInfo>) indexedProperties;

            Type targetType = indexExpression.Type;
            for (int i = 0; i < indexedProperties.Count; i++) {
                if (indexedProperties[i].parameterInfos.Length == 1) {
                    if (indexedProperties[i].parameterInfos[0].ParameterType == targetType) {
                        indexProperty = indexedProperties[i].propertyInfo;
                        ListPool<ReflectionUtil.IndexerInfo>.Release(ref l);
                        return indexExpression;
                    }
                }
            }

            for (int i = 0; i < indexedProperties.Count; i++) {
                // if any conversions exist this will work, if not we hit an exception
                try {
                    indexExpression = Expression.Convert(indexExpression, indexedProperties[i].parameterInfos[0].ParameterType);
                    indexProperty = indexedProperties[i].propertyInfo;
                    ListPool<ReflectionUtil.IndexerInfo>.Release(ref l);

                    return indexExpression;
                }
                catch (Exception) {
                    // ignored
                }
            }

            ListPool<ReflectionUtil.IndexerInfo>.Release(ref l);
            throw new CompileException($"Can't find indexed property that accepts an indexer of type {indexExpression.Type}");
        }

        private Expression Visit(ASTNode node) {
            return Visit(null, node);
        }

        private Expression VisitUnchecked(Type targetType, ASTNode node) {
            switch (node.type) {
                case ASTNodeType.NullLiteral:
                    return Expression.Constant(null);

                case ASTNodeType.BooleanLiteral:
                    return VisitBoolLiteral((LiteralNode) node);

                case ASTNodeType.NumericLiteral:
                    return VisitNumericLiteral(targetType, (LiteralNode) node);

                case ASTNodeType.DefaultLiteral:
                    if (targetType != null) {
                        return Expression.Default(targetType);
                    }

                    if (implicitContext != null) {
                        return Expression.Default(implicitContext.Value.type);
                    }

                    // todo -- when target type is unknown we require the default(T) syntax. Change the parser to check for a type node optionally after default tokens, maybe use ASTNodeType.DefaultExpression
                    throw new NotImplementedException();

                case ASTNodeType.StringLiteral:
                    return Expression.Constant(((LiteralNode) node).rawValue);

                case ASTNodeType.Operator:
                    return VisitOperator(targetType, (OperatorNode) node);

                case ASTNodeType.TypeOf:
                    return VisitTypeNode((TypeNode) node);

                case ASTNodeType.Identifier:
                    return VisitIdentifierNode((IdentifierNode) node);

                case ASTNodeType.AccessExpression:
                    return VisitAccessExpression((MemberAccessExpressionNode) node);

                case ASTNodeType.UnaryNot:
                    return VisitUnaryNot((UnaryExpressionNode) node);

                case ASTNodeType.UnaryMinus:
                    return VisitUnaryNot((UnaryExpressionNode) node);

                case ASTNodeType.UnaryBitwiseNot:
                    return VisitBitwiseNot((UnaryExpressionNode) node);

                case ASTNodeType.DirectCast:
                    return VisitDirectCast((UnaryExpressionNode) node);

                case ASTNodeType.ListInitializer:
                    // [] if not used as a return value then use pooling for the array 
                    // [1, 2, 3].Contains(myValue)
                    // repeat list="[1, 2, 3]"
                    // style=[style1, style2, property ? style3]
                    // value=new Vector
                    throw new NotImplementedException();

                case ASTNodeType.New:
                    return VisitNew((NewExpressionNode) node);

                case ASTNodeType.Paren:
                    ParenNode parenNode = (ParenNode) node;
                    return Visit(parenNode.expression);

                case ASTNodeType.LambdaExpression:
                    return VisitLambda(targetType, (LambdaExpressionNode) node);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Expression Visit(Type targetType, ASTNode node) {
            Expression retn = VisitUnchecked(targetType, node);
            if (targetType != null && retn.Type != targetType) {
                try {
                    retn = Expression.Convert(retn, targetType);
                }
                catch (InvalidOperationException) {
                    throw CompileException.InvalidTargetType(targetType, retn.Type);
                }
            }

            return retn;
        }

        private Expression VisitAndGetType(Type targetType, ASTNode node, out Type statementType) {
            Expression retn = VisitUnchecked(targetType, node);
            statementType = retn.Type;
            if (targetType != null && retn.Type != targetType) {
                try {
                    retn = Expression.Convert(retn, targetType);
                }
                catch (InvalidOperationException) {
                    throw CompileException.InvalidTargetType(targetType, retn.Type);
                }
            }

            return retn;
        }

        private Expression VisitOperatorStep(OperatorType operatorType, Expression left, Expression right) {
            if (TypeUtil.IsArithmetic(left.Type) && TypeUtil.IsArithmetic(right.Type)) {
                if (left.Type != right.Type) {
                    if (ReflectionUtil.AreNumericTypesCompatible(left.Type, right.Type)) {
                        bool isLeftIntegral = ReflectionUtil.IsIntegralType(left.Type);
                        bool isRightIntegral = ReflectionUtil.IsIntegralType(right.Type);
                        bool isLeftFloatingPoint = !isLeftIntegral;
                        bool isRightFloatingPoint = !isRightIntegral;

                        if (isLeftIntegral && isRightFloatingPoint) {
                            left = Expression.Convert(left, right.Type);
                        }
                        else if (isLeftFloatingPoint && isRightIntegral) {
                            right = Expression.Convert(right, left.Type);
                        }
                        else if (isLeftIntegral) {
                            if (right.Type == typeof(int) && right.NodeType == ExpressionType.Constant) {
                                ConstantExpression constantExpression = (ConstantExpression) right;
                                int constValue = (int) constantExpression.Value;

                                if (left.Type == typeof(byte) && constValue <= byte.MaxValue && constValue >= byte.MaxValue) {
                                    right = ExpressionFactory.Convert(right, typeof(byte), null);
                                }

                                if (left.Type == typeof(sbyte) && constValue <= sbyte.MaxValue && constValue >= sbyte.MaxValue) {
                                    right = ExpressionFactory.Convert(right, typeof(sbyte), null);
                                }

                                if (left.Type == typeof(short) && constValue <= short.MaxValue && constValue >= short.MaxValue) {
                                    right = ExpressionFactory.Convert(right, typeof(short), null);
                                }

                                if (left.Type == typeof(ushort) && constValue <= ushort.MaxValue && constValue >= ushort.MaxValue) {
                                    right = ExpressionFactory.Convert(right, typeof(ushort), null);
                                }

                                if (left.Type == typeof(uint)) {
                                    right = ExpressionFactory.Convert(right, typeof(uint), null);
                                }

                                if (left.Type == typeof(ulong)) {
                                    right = ExpressionFactory.Convert(right, typeof(ulong), null);
                                }
                            }

                            else {
                                throw new NotImplementedException("Implicit conversion between integral types is not yet supported. Please use casting to fix this");
                            }
                        }
                        else {
                            // there are no implicit conversions between floating point types.
                            throw CompileException.NoImplicitConversion(left.Type, right.Type);
                        }
                    }
                }
            }

            // todo -- more constant folding
            switch (operatorType) {
                case OperatorType.Coalesce:
                    return Expression.Coalesce(left, right);

                case OperatorType.Plus:
                    bool leftIsString = left.Type == typeof(string);
                    bool rightIsString = right.Type == typeof(string);

                    if (leftIsString && rightIsString) {
                        if (left is ConstantExpression leftConst && right is ConstantExpression rightConst) {
                            string leftStr = (string) leftConst.Value;
                            string rightStr = (string) rightConst.Value;
                            return Expression.Constant(leftStr + rightStr);
                        }

                        return Expression.Call(StringConcat2, left, right);
                    }
                    else if (leftIsString) {
                        if (left is ConstantExpression leftConst) {
                            if (right is ConstantExpression rightConst) {
                                return Expression.Constant((string) leftConst.Value + rightConst.Value);
                            }
                        }

                        if (right is ConstantExpression constRight) {
                            return Expression.Call(StringConcat2, left, Expression.Constant(constRight.Value.ToString()));
                        }

                        Expression x = Expression.Call(right, right.Type.GetMethod("ToString", Type.EmptyTypes));
                        return Expression.Call(null, StringConcat2, left, x);
                    }
                    else if (rightIsString) {
                        if (right is ConstantExpression rightConst) {
                            if (left is ConstantExpression leftConst) {
                                return Expression.Constant(leftConst.Value + (string) rightConst.Value);
                            }
                        }

                        if (left is ConstantExpression leftConst2) {
                            return Expression.Call(StringConcat2, Expression.Constant(leftConst2.Value.ToString()), right);
                        }

                        Expression x = Expression.Call(left, left.Type.GetMethod("ToString", Type.EmptyTypes));
                        return Expression.Call(null, StringConcat2, x, right);
                    }
                    else {
                        return Expression.Add(left, right);
                    }

                case OperatorType.Minus:
                    return Expression.Subtract(left, right);

                case OperatorType.Mod:
                    return Expression.Modulo(left, right);

                case OperatorType.Times:
                    return Expression.Multiply(left, right);

                case OperatorType.Divide:
                    return Expression.Divide(left, right);

                case OperatorType.Equals: {
                    if (left.Type.IsClass && (right is ConstantExpression nullNode) && nullNode.Value == null) {
                        wasNullChecked.Add(left);
                    }
                    else if (right.Type.IsClass && (left is ConstantExpression nullNode2) && nullNode2.Value == null) {
                        wasNullChecked.Add(right);
                    }

                    return Expression.Equal(left, right);
                }

                case OperatorType.NotEquals: {
                    if (left.Type.IsClass && (right is ConstantExpression nullNode) && nullNode.Value == null) {
                        wasNullChecked.Add(left);
                    }
                    else if (right.Type.IsClass && (left is ConstantExpression nullNode2) && nullNode2.Value == null) {
                        wasNullChecked.Add(right);
                    }

                    return Expression.NotEqual(left, right);
                }

                case OperatorType.GreaterThan:
                    return Expression.GreaterThan(left, right);

                case OperatorType.GreaterThanEqualTo:
                    return Expression.GreaterThanOrEqual(left, right);

                case OperatorType.LessThan:
                    return Expression.LessThan(left, right);

                case OperatorType.LessThanEqualTo:
                    return Expression.LessThanOrEqual(left, right);

                case OperatorType.And:
                    return Expression.AndAlso(left, right);

                case OperatorType.Or:
                    return Expression.OrElse(left, right);

                case OperatorType.ShiftRight:
                    return Expression.RightShift(left, right);

                case OperatorType.ShiftLeft:
                    return Expression.LeftShift(left, right);

                case OperatorType.BinaryAnd:
                    return Expression.And(left, right);

                case OperatorType.BinaryOr:
                    return Expression.Or(left, right);

                case OperatorType.BinaryXor:
                    return Expression.ExclusiveOr(left, right);

                default:
                    throw new CompileException($"Tried to visit the operator node {operatorType} but it wasn't handled by LinqCompiler.VisitOperator");
            }
        }

        private Expression ForceBooleanCast(Expression expression) {
            Type expressionType = expression.Type;

            if (expressionType == typeof(bool)) {
                return expression;
            }

            try {
                expression = Expression.Convert(expression, typeof(bool));
            }
            catch (InvalidOperationException ex) {
                if (ex.Message.Contains("No coercion operator is defined between types")) {
                    if (expressionType.IsClass || expressionType.IsNullableType()) {
                        wasNullChecked.Add(expression);
                        expression = Expression.NotEqual(expression, Expression.Constant(null));
                    }
                    else if (TypeUtil.IsArithmetic(expressionType)) {
                        expression = Expression.NotEqual(expression, Expression.Constant(0));
                    }
                }
            }

            return expression;
        }

        private Expression VisitOperator(Type targetType, OperatorNode operatorNode) {
            Expression left;
            Expression right;

            if (operatorNode.operatorType == OperatorType.TernaryCondition) {
                if (!(operatorNode.right is OperatorNode select)) {
                    throw new CompileException("Bad ternary, expected the right hand side to be a TernarySelection but it was {select}");
                }

                if (select.operatorType != OperatorType.TernarySelection) {
                    throw new CompileException($"Bad ternary, expected the right hand side to be a TernarySelection but it was {select.operatorType}");
                }

                Debug.Assert(targetType != null);

                // if target type is null & left type & right type are not compatible, error

                // todo -- support null target type & try to find matching type between left and right.
                // todo    probably involves re-visiting with sub-compilers since we don't want to emit the output, just find types
                Expression ternaryCondition = ForceBooleanCast(VisitUnchecked(typeof(bool), operatorNode.left));

                Expression ternaryVariable = currentBlock.AddInternalVariable(targetType, "ternaryOutput");

                PushBlock();

                left = Visit(targetType, select.left);
                AddStatement(Expression.Assign(ternaryVariable, left));

                BlockExpression passBlock = PopBlock();

                PushBlock();

                right = Visit(targetType, select.right);
                AddStatement(Expression.Assign(ternaryVariable, right));

                BlockExpression failBlock = PopBlock();


                AddStatement(Expression.IfThenElse(ternaryCondition, passBlock, failBlock));

                return ternaryVariable;
            }

            left = Visit(operatorNode.left);
            if (operatorNode.operatorType == OperatorType.Is) {
                TypeNode typeNode = (TypeNode) operatorNode.right;
                Type t = TypeProcessor.ResolveType(typeNode.typeLookup, namespaces);
                return Expression.TypeIs(left, t);
            }
            else if (operatorNode.operatorType == OperatorType.As) {
                TypeNode typeNode = (TypeNode) operatorNode.right;
                Type t = TypeProcessor.ResolveType(typeNode.typeLookup, namespaces);
                return Expression.TypeAs(left, t);
            }

            right = Visit(operatorNode.right);

            try {
                return VisitOperatorStep(operatorNode.operatorType, left, right);
            }
            catch (InvalidOperationException invalidOp) {
                // todo -- need to do my own casting for math types and string concats
                if (invalidOp.Message.Contains("is not defined for the types")) {
                    throw CompileException.MissingBinaryOperator(operatorNode.operatorType, left.Type, right.Type);
                }
                else throw;
            }
        }

        private int GetNextCompilerId() {
            if (parent != null) {
                return parent.GetNextCompilerId();
            }

            return ++subCompilerId;
        }

        private Expression VisitLambda(Type targetType, LambdaExpressionNode lambda) {
            // assume a target type for now, I think its an error not to have one anyway

            LinqCompiler nested = s_CompilerPool.Get();

            nested.parent = this;
            nested.id = GetNextCompilerId();

            if (targetType == null) {
                throw new NotImplementedException("LambdaExpressions are only valid when they have a target type set.");
            }

            Type[] arguments = targetType.GetGenericArguments();

            if (ReflectionUtil.IsAction(targetType)) {
                if (lambda.signature.size != arguments.Length) { }

                nested.returnType = typeof(void);
            }
            else {
                nested.returnType = arguments[arguments.Length - 1];
                if (lambda.signature.size != arguments.Length - 1) { }
            }

            if (lambda.signature.size > 0) {
                for (int i = 0; i < lambda.signature.size; i++) {
                    if (lambda.signature[i].type != null) {
                        Type argType = TypeProcessor.ResolveType(lambda.signature[i].type.Value, namespaces);
                        if (argType != arguments[i]) {
                            throw CompileException.InvalidLambdaArgument();
                        }

                        nested.AddParameter(argType, lambda.signature[i].identifier);
                    }
                    else {
                        nested.AddParameter(arguments[i], lambda.signature[i].identifier);
                    }
                }
            }

            nested.Return(lambda.body);

            LambdaExpression retn = nested.BuildLambda();
            s_CompilerPool.Release(nested);
            return retn;
        }

        private Expression VisitNew(NewExpressionNode newNode) {
            TypeLookup typeLookup = newNode.typeLookup;

            Type type = TypeProcessor.ResolveType(typeLookup, namespaces);
            if (type == null) {
                throw CompileException.UnresolvedType(typeLookup);
            }

            if (newNode.parameters == null || newNode.parameters.Count == 0) {
                return Expression.New(type);
            }

            Expression[] arguments = new Expression[newNode.parameters.Count];
            for (int i = 0; i < newNode.parameters.Count; i++) {
                Expression argument = Visit(newNode.parameters[i]);
                arguments[i] = argument;
            }

            StructList<ExpressionUtil.ParameterConversion> conversions;

            ConstructorInfo constructor = ExpressionUtil.SelectEligibleConstructor(type, arguments, out conversions);
            if (constructor == null) {
                throw CompileException.UnresolvedConstructor(type, arguments.Select((e) => e.Type).ToArray());
            }

            if (conversions.size > arguments.Length) {
                Array.Resize(ref arguments, conversions.size);
            }

            for (int i = 0; i < conversions.size; i++) {
                arguments[i] = conversions[i].Convert();
            }

            StructList<ExpressionUtil.ParameterConversion>.Release(ref conversions);
            return Expression.New(constructor, arguments);
        }

        private Expression VisitDirectCast(UnaryExpressionNode node) {
            Type t = TypeProcessor.ResolveType(node.typeLookup, namespaces);

            Expression toConvert = Visit(node.expression);
            return Expression.Convert(toConvert, t);
        }

        private Expression VisitUnaryNot(UnaryExpressionNode node) {
            Expression body = Visit(node.expression);
            return Expression.Not(body);
        }

        private Expression VisitUnaryMinus(UnaryExpressionNode node) {
            Expression body = Visit(node.expression);
            return Expression.Negate(body);
        }

        private Expression VisitBitwiseNot(UnaryExpressionNode node) {
            Expression body = Visit(node.expression);
            return Expression.OnesComplement(body);
        }

        private Expression VisitTypeNode(TypeNode typeNode) {
            TypeLookup typePath = typeNode.typeLookup;
            try {
                Type t = TypeProcessor.ResolveType(typeNode.typeLookup, namespaces);
                return Expression.Constant(t);
            }
            catch (TypeResolutionException) { }

            throw CompileException.UnresolvedType(typePath);
        }

        private Expression VisitIdentifierNode(IdentifierNode identifierNode) {
            if (identifierNode.name[0] == '$') {
                return ResolveAlias(identifierNode.name);
            }

            if (implicitContext != null) {
                if (TryResolveInstanceOrStaticMemberAccess(implicitContext.Value.expression, identifierNode.name, out Expression expression)) {
                    return expression;
                }
            }

            // temp

            Expression parameterExpression = ResolveVariableName(identifierNode.name);

            if (parameterExpression != null) {
                //  Expression variable = currentBlock.AddVariable(parameterExpression.Type, identifierNode.name);
                //  currentBlock.AddStatement(Expression.Assign(variable, parameterExpression));
                return parameterExpression; //variable;
            }
//
//            else if (defaultIdentifier != null) {
//                Expression expr = MemberAccess(ResolveVariableName(defaultIdentifier), identifierNode.name);
//                Expression variable = currentBlock.AddVariable(expr.Type, identifierNode.name);
//                currentBlock.AddStatement(Expression.Assign(variable, expr));
//                return variable;
//            }

            throw CompileException.UnresolvedIdentifier(identifierNode.name);
        }

        private static Expression VisitBoolLiteral(LiteralNode literalNode) {
            if (bool.TryParse(literalNode.rawValue, out bool value)) {
                return Expression.Constant(value);
            }

            throw new CompileException($"Unable to parse bool from {literalNode.rawValue}");
        }

        private static Expression VisitNumericLiteral(Type targetType, LiteralNode literalNode) {
            if (targetType == null) {
                string value = literalNode.rawValue.Trim();
                char lastChar = char.ToLower(value[value.Length - 1]);
                if (value.Length > 1) {
                    if (lastChar == 'f') {
                        if (float.TryParse(value.Remove(value.Length - 1), out float fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a float but failed");
                    }

                    if (lastChar == 'd') {
                        if (double.TryParse(value.Remove(value.Length - 1), out double fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a double but failed");
                    }

                    if (lastChar == 'm') {
                        if (decimal.TryParse(value.Remove(value.Length - 1), out decimal fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a decimal but failed");
                    }

                    if (lastChar == 'u') {
                        if (uint.TryParse(value.Remove(value.Length - 1), out uint fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a uint but failed");
                    }

                    if (lastChar == 'l') {
                        if (value.Length >= 2) {
                            char prevToLast = char.ToLower(value[value.Length - 2]);
                            if (prevToLast == 'u') {
                                if (ulong.TryParse(value.Remove(value.Length - 1), out ulong ulongVal)) {
                                    return Expression.Constant(ulongVal);
                                }

                                throw new CompileException($"Tried to parse value {literalNode.rawValue} as a ulong but failed");
                            }
                        }

                        if (long.TryParse(value.Remove(value.Length - 1), out long fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a long but failed");
                    }

                    // no character specifier, parse as double if there is a decimal or int if there is not

                    if (value.Contains(".")) {
                        if (double.TryParse(value, out double fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a double but failed");
                    }
                    else {
                        if (int.TryParse(value, out int fVal)) {
                            return Expression.Constant(fVal);
                        }

                        throw new CompileException($"Tried to parse value {literalNode.rawValue} as a int but failed");
                    }
                }

                if (int.TryParse(value, out int intVal)) {
                    return Expression.Constant(intVal);
                }

                throw new CompileException($"Tried to parse value {literalNode.rawValue} as a int but failed");
            }

            if (targetType == typeof(float)) {
                if (float.TryParse(literalNode.rawValue.Replace("f", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException("Tried to parse value {literalNode.rawValue} as a float but failed");
                }
            }

            if (targetType == typeof(int)) {
                if (int.TryParse(literalNode.rawValue, out int f)) {
                    return Expression.Constant(f);
                }
                else if (float.TryParse(literalNode.rawValue.Replace("f", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float fVal)) {
                    return Expression.Constant((int) fVal);
                }

                throw new CompileException($"Unable to parse {literalNode.rawValue} as an int value");
            }

            if (targetType == typeof(double)) {
                if (double.TryParse(literalNode.rawValue.Replace("d", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a double but failed");
                }
            }

            if (targetType == typeof(short)) {
                if (short.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out short f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a short but failed");
                }
            }

            if (targetType == typeof(ushort)) {
                if (ushort.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out ushort f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a ushort but failed");
                }
            }

            if (targetType == typeof(byte)) {
                if (byte.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out byte f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a byte but failed");
                }
            }

            if (targetType == typeof(sbyte)) {
                if (sbyte.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out sbyte f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a sbyte but failed");
                }
            }

            if (targetType == typeof(long)) {
                if (long.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out long f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a long but failed");
                }
            }

            if (targetType == typeof(uint)) {
                if (uint.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out uint f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a uint but failed");
                }
            }

            if (targetType == typeof(ulong)) {
                if (ulong.TryParse(literalNode.rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out ulong f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a ulong but failed");
                }
            }

            if (targetType == typeof(char)) {
                if (char.TryParse(literalNode.rawValue, out char f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a char but failed");
                }
            }

            if (targetType == typeof(decimal)) {
                if (decimal.TryParse(literalNode.rawValue, out decimal f)) {
                    return Expression.Constant(f);
                }
                else {
                    throw new CompileException($"Tried to parse value {literalNode.rawValue} as a decimal but failed");
                }
            }

            throw new CompileException($"Unable to parse numeric value from {literalNode.rawValue} target type was {targetType}");
        }

        public bool HasVariable(string variableName, out ParameterExpression variable) {
            variable = currentBlock.ResolveVariable(variableName);
            return variable != null;
        }

        public ParameterExpression AssignVariable(string variableName, string expression) {
            ASTNode ast = ExpressionParser.Parse(expression);
            Expression value = VisitAndGetType(null, ast, out Type expressionType);
            ParameterExpression variable = Expression.Parameter(expressionType, variableName);
            currentBlock.AddUserVariable(new Parameter() {
                type = expressionType,
                name = variableName,
                expression = variable
            });
            AddStatement(Expression.Assign(variable, value));
            return variable;
        }

    }

    public struct Parameter<T> {

        public readonly string name;
        public readonly ParameterFlags flags;

        public Parameter(string name, ParameterFlags flags = 0) {
            this.name = name;
            this.flags = flags;
        }

        public static implicit operator Parameter(Parameter<T> parameter) {
            return new Parameter(typeof(T), parameter.name, parameter.flags);
        }

    }

    public struct Parameter {

        public ParameterFlags flags;
        public ParameterExpression expression;
        public string name;
        public Type type;

        public Parameter(Type type, string name, ParameterFlags flags = 0) {
            this.type = type;
            this.name = name;
            this.flags = flags;
            this.expression = Expression.Parameter(type, name);
        }

        public static implicit operator ParameterExpression(Parameter parameter) {
            return parameter.expression;
        }

    }

    [Flags]
    public enum ParameterFlags {

        NeverNull = 1 << 1,
        NeverOutOfBounds = 1 << 2

    }

}
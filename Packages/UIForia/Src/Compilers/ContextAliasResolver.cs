using System;
using System.Linq.Expressions;
using System.Reflection;
using UIForia.Systems;
using UIForia.Util;

namespace UIForia.Compilers {

    public class ContextAliasResolver : ILinqAliasResolver {

        public Type type;
        public int id;
        public string contextVarName;

        private static readonly MethodInfo s_ResolveInfo = typeof(ContextAliasResolver).GetMethod(nameof(ResolveContext), BindingFlags.Static | BindingFlags.Public);

        public ContextAliasResolver(Type type, string ctxName, int id) {
            this.type = type;
            this.id = id;
            this.contextVarName = "_ctx_" + ctxName + "_" + id;
        }

        public Expression Resolve(string aliasName, LinqCompiler compiler) {
            ParameterExpression ctxVar = null;

            // make sure we looked up the context, look up if not there
            if (!compiler.HasVariable(contextVarName, out ctxVar)) {
                Expression contextCall = Expression.Call(null, s_ResolveInfo.MakeGenericMethod(type), compiler.GetParameter(TemplateCompiler.k_ContextStackVarName), Expression.Constant(id));
                ctxVar = compiler.AddVariable(new Parameter(type, contextVarName, ParameterFlags.NeverNull), contextCall);
            }

            return ctxVar;
        }

        public static T ResolveContext<T>(StructStack<TemplateContextWrapper> stack, int id) where T : TemplateContext {
            TemplateContextWrapper[] array = stack.array;
            for (int i = stack.size - 1; i >= 0; i--) {
                if (array[i].id == id) {
                    return (T) array[i].context;
                }
            }

            throw new Exception("Unresolved context");
        }

    }

    public class ContextVariableResolver : ILinqAliasResolver {

        public Type type;
        public int id;
        public string contextVarName;
        public string aliasVarName;
        public string expression;
        
        private static readonly MethodInfo s_ResolveInfo = typeof(ContextAliasResolver).GetMethod(nameof(ContextAliasResolver.ResolveContext), BindingFlags.Static | BindingFlags.Public);

        public ContextVariableResolver(Type type, string ctxName, int id, in TemplateContextVariable contextVariable) {
            this.type = type;
            this.id = id;
            this.contextVarName = "_ctx_" + ctxName + "_" + id;
            this.aliasVarName = "_alias_" + contextVariable.name + "_" + id;
            this.expression = contextVariable.expression;
        }

        public Expression Resolve(string aliasName, LinqCompiler compiler) {
            
            ParameterExpression ctxVar = null;
            ParameterExpression aliasVariable = null;
            
            // make sure we looked up the context, look up if not there
            if (!compiler.HasVariable(contextVarName, out ctxVar)) {
                Expression contextCall = Expression.Call(null, s_ResolveInfo.MakeGenericMethod(type), compiler.GetParameter(TemplateCompiler.k_ContextStackVarName), Expression.Constant(id));
                ctxVar = compiler.AddVariable(new Parameter(type, contextVarName, ParameterFlags.NeverNull), contextCall);
            }

            // if we haven't got a variable for this alias already, create one and assign it.
            if (!compiler.HasVariable(aliasVarName, out aliasVariable)) {
                compiler.SetImplicitContext(ctxVar, ParameterFlags.NeverNull | ParameterFlags.NeverOutOfBounds);
                // get return type of input expression
                aliasVariable = compiler.AssignVariable(aliasVarName, expression);
                compiler.SetImplicitContext(null);
            }

            return aliasVariable;

        }

    }

}
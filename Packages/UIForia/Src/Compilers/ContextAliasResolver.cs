using System;
using System.Linq.Expressions;
using System.Reflection;
using UIForia.Systems;
using UIForia.Util;

namespace UIForia.Compilers {

    public class ContextAliasResolver : ILinqAliasResolver {

        public Type type;
        public int id;

        private static readonly MethodInfo s_ResolveInfo = typeof(ContextAliasResolver).GetMethod(nameof(ResolveContext), BindingFlags.Static | BindingFlags.Public);
            
        public ContextAliasResolver(Type type, int id) {
            this.type = type;
            this.id = id;
        }

        public Expression Resolve(string aliasName, LinqCompiler compiler) {
            Expression contextCall = Expression.Call(null, s_ResolveInfo.MakeGenericMethod(type), compiler.GetParameter("__contextStack"), Expression.Constant(id));
            Expression ctxVar = compiler.AddVariable(new Parameter(type, "ctx", ParameterFlags.NeverNull), contextCall);
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

}
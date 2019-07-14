using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UIForia.Attributes;
using UIForia.Bindings;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Extensions;
using UIForia.Parsing.Expression;
using UIForia.Util;

namespace UIForia.Compilers {

    public class LinqPropertyCompiler {

        private readonly LinqCompiler compiler;
        
        [ThreadStatic] private static Dictionary<Type, LightList<PropertyChangeHandlerMethod>> s_TypeMap;

        private struct BlockCtx {

            public readonly LinqCompiler compiler;
            public readonly LightList<MethodInfo> changeHandlers;
            public readonly LHSStatementChain left;
            public readonly Expression right;
            public readonly Type rootType;
            public readonly Type elementType;

            public BlockCtx(LinqCompiler compiler, LightList<MethodInfo> changeHandlers, LHSStatementChain left, Expression right, Type rootType, Type elementType) {
                this.compiler = compiler;
                this.changeHandlers = changeHandlers;
                this.left = left;
                this.right = right;
                this.rootType = rootType;
                this.elementType = elementType;
            }

        }

        public LinqPropertyCompiler(LinqCompiler compiler) {
            this.compiler = compiler;
        }
        
        public LambdaExpression BuildLambda(CompilationContext ctx, in AttributeDefinition2 attributeDefinition) {
            LightList<MethodInfo> changedHandlers = GetPropertyChangedHandlers(ctx.elementType.rawType, attributeDefinition.key);

            compiler.SetSignature(
                // todo -- supported dotted or indexed access to properties, need to play with the implicit flag, set to element for left, root for right
                new Parameter(typeof(UIElement), "__root", ParameterFlags.NeverNull),
                new Parameter(typeof(UIElement), "__element", ParameterFlags.NeverNull),
                new Parameter(typeof(StructStack<TemplateContextWrapper>), "__contextStack", ParameterFlags.NeverNull)
            );

            // todo -- never null check _root or _element
            ParameterExpression rootExpr = compiler.AddVariable(new Parameter(ctx.rootType.rawType, "_root", ParameterFlags.NeverNull), Expression.Convert(compiler.GetParameter("root"), ctx.rootType.rawType));
            ParameterExpression elementExpr = compiler.AddVariable(new Parameter(ctx.elementType.rawType, "_element", ParameterFlags.NeverNull), Expression.Convert(compiler.GetParameter("element"), ctx.elementType.rawType));
            
            // if is content binding
                // assign root = element
            
            
            compiler.SetNullCheckHandler((c, expr) => {
                // todo -- log out errors to uiforia console,
                // if element handles binding failure, invoke that handler. maybe log out the whole binding & file path & type of the null thing (var name is probably useless)
            });

            compiler.SetImplicitContext(elementExpr);
            LHSStatementChain left = compiler.AssignableStatement(attributeDefinition.key);
            
            compiler.SetImplicitContext(rootExpr);
            Expression accessor = compiler.AccessorStatement(left.targetExpression.Type, attributeDefinition.value);
            Expression right = null;

            // todo -- we really want the assignment chain to only be unrolled when we perform the assignment. We have extra struct copies right now :(

            if (accessor is ConstantExpression) {
                right = accessor;
            }
            else {
                // may not need a variable if also omitting the if check
                right = compiler.AddVariable(left.targetExpression.Type, "right");
                compiler.Assign(right, accessor);
            }

            if (left.isSimpleAssignment && changedHandlers.Count == 0) {
                compiler.Assign(left, right);
            }
            else {
                // todo -- can eliminate the if here if the assignment is to a simple field and no handlers are used
                compiler.IfNotEqual(left, right, (blockContext) => {
                    blockContext.compiler.Assign(blockContext.left, blockContext.right);

                    for (int i = 0; i < blockContext.changeHandlers.Count; i++) {
                        // if parameter count == 1
                        // if parameter count == 2
                        // assert parameter types match exactly
//                    compiler.Invoke(rootParameter, changedHandlers[i], compiler.GetVariable("previousValue"));
                    }

                    if (blockContext.elementType.Implements(typeof(IPropertyChangedHandler))) {
                        //compiler.Invoke("element", "OnPropertyChanged", compiler.GetVariable("currentValue"));
                    }
                }, new BlockCtx(compiler, changedHandlers, left, right, ctx.rootType.rawType, ctx.elementType.rawType));
            }

            LightList<MethodInfo>.Release(ref changedHandlers);
            
            LambdaExpression retn = compiler.BuildLambda();
            // todo -- set other data?
            // todo -- scan for 'const-ness'
            // todo -- optimize generated code (constant folding, etc)

            compiler.Reset();
            
            return retn;
        }

        private struct PropertyChangeHandlerMethod {

            public MethodInfo methodInfo;
            public OnPropertyChanged propertyChangedAttr;

        }

        private static LightList<MethodInfo> GetPropertyChangedHandlers(Type targetType, string fieldOrPropertyName) {
            LightList<MethodInfo> retn = LightList<MethodInfo>.GetMinSize(4);

            s_TypeMap = s_TypeMap ?? new Dictionary<Type, LightList<PropertyChangeHandlerMethod>>();
            
            if (s_TypeMap.TryGetValue(targetType, out LightList<PropertyChangeHandlerMethod> methodInfos)) {
                if (methodInfos == null) return retn;

                for (int i = 0; i < methodInfos.Count; i++) {
                    if (methodInfos[i].propertyChangedAttr.propertyName == fieldOrPropertyName) {
                        retn.Add(methodInfos[i].methodInfo);
                    }
                }
            }
            else {
                methodInfos = LightList<PropertyChangeHandlerMethod>.Get();

                MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++) {
                    MethodInfo info = methods[i];

                    if (!(Attribute.GetCustomAttribute(info, typeof(OnPropertyChanged), true) is OnPropertyChanged attribute)) {
                        continue;
                    }

                    if (!info.IsPublic) {
                        throw new CompileException($"Method {info} on type {targetType} is marked with an OnPropertyChanged attribute but it is not public");
                    }

                    methodInfos.Add(new PropertyChangeHandlerMethod() {
                        methodInfo = info,
                        propertyChangedAttr = attribute
                    });
                }

                if (methodInfos.Count == 0) {
                    LightList<PropertyChangeHandlerMethod>.Release(ref methodInfos);
                }

                // will be null if count was 0
                s_TypeMap[targetType] = methodInfos;

                if (methodInfos != null) {
                    for (int i = 0; i < methodInfos.Count; i++) {
                        if (methodInfos[i].propertyChangedAttr.propertyName == fieldOrPropertyName) {
                            retn.Add(methodInfos[i].methodInfo);
                        }
                    }
                }
            }

            return retn;
        }

    }

}
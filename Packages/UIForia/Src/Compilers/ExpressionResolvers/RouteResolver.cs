using System;
using UIForia.Elements;
using UIForia.Expressions;

namespace UIForia.Compilers.ExpressionResolvers {
    
    public class RouteResolver : ExpressionAliasResolver {

        public RouteResolver(string aliasName) : base(aliasName) { }

        private static readonly UrlReaderExpression s_UrlReaderExpression = new UrlReaderExpression();

        public override Expression CompileAsDotExpression(CompilerContext context, string propertyName) {
            if (propertyName == "url") {
                return s_UrlReaderExpression;
            }

            return null;
        }

        public class UrlReaderExpression : Expression<string> {

            public override Type YieldedType => typeof(string);

            public override string Evaluate(ExpressionContext context) {
                UIElement element = (UIElement) context.currentObject;
                return element.Application.RoutingSystem.FindRouterInHierarchy(element).CurrentUrl;
            }

            public override bool IsConstant() {
                return false;
            }

        }

    }

}
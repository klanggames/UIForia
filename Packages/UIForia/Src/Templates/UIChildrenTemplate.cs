using System;
using System.Collections.Generic;
using UIForia.Elements;
using UIForia.Expressions;
using UIForia.Parsing.Expression;

namespace UIForia.Templates {

    public class UIChildrenTemplate : UITemplate {

        public UIChildrenTemplate(Application app, List<UITemplate> childTemplates = null, List<AttributeDefinition> attributes = null) 
            : base(app, childTemplates, attributes) { }

        protected override Type elementType => typeof(UIChildrenElement);

        public override void Compile(ParsedTemplate template) {
            CompileStyleBindings(template);
            ResolveBaseStyles(template);
            BuildBindings();
        }

        public override UIElement CreateScoped(TemplateScope inputScope) {
            
            UIChildrenElement element = new UIChildrenElement();
            inputScope.rootElement.TranscludedChildren = element;
            element.OriginTemplate = this;
            element.templateContext = new ExpressionContext(inputScope.rootElement, element);
            return element;
            
        }


    }

}
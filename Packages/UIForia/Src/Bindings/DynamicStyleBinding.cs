using System.Collections.Generic;
using UIForia.Compilers.Style;
using UIForia.Elements;
using UIForia.Expressions;
using UIForia.Rendering;
using UIForia.Templates;
using UIForia.Util;

namespace UIForia.Bindings {

    public class DynamicStyleBinding : Binding {

        private readonly ParsedTemplate template;
        public readonly ArrayLiteralExpression<string> bindingList;

        public DynamicStyleBinding(ParsedTemplate template, ArrayLiteralExpression<string> bindingList) : base("style") {
            this.template = template;
            this.bindingList = bindingList.Clone();
        }

        public override void Execute(UIElement element, ExpressionContext context) {
            IList<string> bindingStyles = bindingList.Evaluate(context);

            if (!element.style.EqualsToSharedStyles(bindingStyles)) {
                LightList<UIStyleGroupContainer> groups = LightListPool<UIStyleGroupContainer>.Get();
                string tagName = element.GetDisplayName();
                UIStyleGroupContainer groupContainer = template.ResolveElementStyle(tagName);
                if (groupContainer != null) {
                    groups.Add(groupContainer);
                }

                for (int i = 0; i < bindingStyles.Count; i++) {
                    string styleName = bindingStyles[i];
                    if (!string.IsNullOrEmpty(styleName)) {
                        if (template.TryResolveStyleGroup(styleName, out UIStyleGroupContainer group)) {
                            group.styleType = StyleType.Shared;
                            groups.Add(group);
                        }
                    }
                }

                element.style.SetStyleGroups(groups);
                LightListPool<UIStyleGroupContainer>.Release(ref groups);
            }
        }

        public override bool IsConstant() {
            return bindingList.IsConstant();
        }

    }

}

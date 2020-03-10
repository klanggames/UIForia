using System;
using System.Collections.Generic;
using UIForia.Attributes;
using UIForia.Routing;
using UIForia.Util;

namespace UIForia.Elements {

    [ContainerElement]
    [TemplateTagName("RouterLink")]
    public class UIRouterLinkElement : UIElement {

        public string path;

        public UIRouterLinkElement() { }

        [OnMouseClick]
        public void GoToPath() {
            List<ElementAttribute> attrs = GetAttributes();
            for (int i = 0; i < attrs.Count; i++) {
                int index = attrs[i].name.IndexOf("parameters.", StringComparison.Ordinal);
                if (index == 0) {
                    //  Expression<string> exp = new ExpressionCompiler(null).Compile<string>(attrs[i].value);
                    throw new NotImplementedException();
                }
            }

            Router gameRouter = application.RoutingSystem.FindRouter("game");
            gameRouter.GoTo(path);

            ListPool<ElementAttribute>.Release(ref attrs);
        }

    }

    [ContainerElement]
    [TemplateTagName("RouterLinkBack")]
    public class UIRouterLinkBackElement : UIElement {

        public override void OnUpdate() { }

        [OnMouseClick]
        public void GoBack() {
            Router gameRouter = application.RoutingSystem.FindRouter("game");
            gameRouter.GoBack();
        }

    }

}
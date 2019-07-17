using System;
using UIForia.Elements;
using UIForia.Rendering;
using UIForia.Util;

namespace UIForia.Systems {

    public interface IStyleSystem : ISystem {

        event Action<UIElement, StructList<StyleProperty>> onStylePropertyChanged;

        void SetStyleProperty(UIElement element, StyleProperty propertyValue);

        void UpdateSelectors();

        void UpdateBindings();

        void UpdateAnimations();

        void Flush();

    }

}
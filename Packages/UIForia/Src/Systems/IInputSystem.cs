using System;
using System.Collections.Generic;
using UIForia.Elements;
using UIForia.UIInput;

namespace UIForia.Systems {

    public interface IInputSystem : ISystem, IInputProvider {

        event Action<IFocusable> onFocusChanged;
        MouseInputEvent CurrentMouseEvent { get; }
        DragEvent CurrentDragEvent { get; }
        KeyboardInputEvent CurrentKeyboardEvent { get; }
        IReadOnlyList<UIElement> ElementsThisFrame { get; }
        void OnLateUpdate();
        
#if UNITY_EDITOR
        List<UIElement> DebugElementsThisFrame { get; }
        bool DebugMouseUpThisFrame { get; }
#endif
    }

}
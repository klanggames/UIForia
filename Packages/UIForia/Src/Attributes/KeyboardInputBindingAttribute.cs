using System;
using UIForia.UIInput;
using UnityEngine;

namespace UIForia.Attributes {
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class KeyboardInputBindingAttribute : Attribute {

        public readonly char character;
        public readonly KeyCode key;
        public readonly KeyboardModifiers modifiers;
        public readonly InputEventType eventType;
        public readonly bool requiresFocus;

        protected KeyboardInputBindingAttribute(KeyCode key, char character, KeyboardModifiers modifiers, InputEventType eventType, bool requiresFocus) {
            this.key = key;
            this.character = character;
            this.modifiers = modifiers;
            this.eventType = eventType;
            this.requiresFocus = requiresFocus;
        }

    }
}
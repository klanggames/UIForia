﻿using UIForia.Expressions;

namespace UIForia.UIInput {

    public struct DragCreatorGroup {

        public readonly ExpressionContext context;
        public readonly DragEventCreator[] creators;

        public DragCreatorGroup(ExpressionContext context, DragEventCreator[] creators) {
            this.context = context;
            this.creators = creators;
        }

        public DragEvent TryCreateEvent(object target, MouseInputEvent mouseEvent) {
            for (int i = 0; i < creators.Length; i++) {
                DragEvent evt = creators[i].Invoke(target, context, mouseEvent);
                if (evt != null) {
                    return evt;
                }
            }

            return null;
        }
    }
}

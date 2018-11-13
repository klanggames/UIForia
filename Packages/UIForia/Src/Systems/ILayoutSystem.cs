﻿using System;
using System.Collections.Generic;
using UIForia.Elements;
using UIForia.Rendering;
using UnityEngine;

namespace UIForia.Systems {

    public interface ILayoutSystem : ISystem {

        event Action<VirtualScrollbar> onCreateVirtualScrollbar;
        event Action<VirtualScrollbar> onDestroyVirtualScrollbar;

        List<UIElement> QueryPoint(Vector2 point, List<UIElement> retn);

        OffsetRect GetPaddingRect(UIElement element);
        OffsetRect GetMarginRect(UIElement element);
        OffsetRect GetBorderRect(UIElement element);

    }

}
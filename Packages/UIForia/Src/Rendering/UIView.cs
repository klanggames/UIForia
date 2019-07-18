using System;
using System.Collections.Generic;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Layout;
using UIForia.Systems;
using UIForia.Templates;
using UIForia.Util;
using UnityEngine;
using UnityEngine.Rendering;
using Application = UIForia.Application;

public class UIViewRootElement : UIElement {

    public UIViewRootElement() {
        flags |= UIElementFlags.BuiltIn;
        flags |= UIElementFlags.ImplicitElement;
        flags |= UIElementFlags.Created;
        flags |= UIElementFlags.Ready;
    }

}

// the idea behind a view is that it is a flat plane that can be oriented in 3d space and show content
public class UIView {

    public event Action<UIElement> onElementCreated;
    public event Action<UIElement> onElementReady;
    public event Action<UIElement> onElementRegistered;
    public event Action<UIElement> onElementDestroyed;
    public event Action<UIElement> onElementHierarchyEnabled;
    public event Action<UIElement> onElementHierarchyDisabled;

    private readonly Type m_ElementType;
    private readonly string m_Template;

    public int Depth { get; set; }

    public Rect Viewport { get; set; }
    // this might want to be changed but so many test expect this that I dont' want to right now

    public UIElement RootElement => rootElement.GetChild(0);
    public float ScaleFactor { get; set; } = 1f;

    internal Matrix4x4 matrix;
    internal Size size;

    internal Vector3 position;
    internal Vector3 scale;
    internal Quaternion rotation;

    public readonly int id;
    public readonly Application Application;
    public readonly string name;
    public RenderTexture renderTexture;

    internal LightList<UIElement> elements;
    internal LightList<UIElement> visibleElements;
    internal UIViewRootElement rootElement;
    private int elementCount;

    public bool clipOverflow;

    public bool focusOnMouseDown;
    public bool sizeChanged;

    internal UIView(int id, string name, Application app, Rect viewportRect, int depth, Type elementType, string template = null) {
        this.id = id;
        this.name = name;
        this.Application = app;
        this.Viewport = viewportRect;
        this.Depth = depth;
        this.m_Template = template;
        this.m_ElementType = elementType;
        this.rotation = Quaternion.identity;
        this.scale = Vector3.one;
        this.position = viewportRect.position;
        this.size = new Size(Screen.width, Screen.height);
        this.elements = new LightList<UIElement>(32);
        this.visibleElements = new LightList<UIElement>(32);
        this.rootElement = new UIViewRootElement();
        this.rootElement.flags |= UIElementFlags.Enabled;
        this.rootElement.flags |= UIElementFlags.AncestorEnabled;
        this.rootElement.View = this;
        this.sizeChanged = true;
    }

    internal UIView(int id, string name, Application app, Rect viewportRect, int depth) {
        this.id = id;
        this.name = name;
        this.Application = app;
        this.Viewport = viewportRect;
        this.Depth = depth;
        this.rotation = Quaternion.identity;
        this.scale = Vector3.one;
        this.position = viewportRect.position;
        this.size = new Size(Screen.width, Screen.height);
        this.elements = new LightList<UIElement>(32);
        this.visibleElements = new LightList<UIElement>(32);
        this.rootElement = new UIViewRootElement();
        this.rootElement.flags |= UIElementFlags.Enabled;
        this.rootElement.flags |= UIElementFlags.AncestorEnabled;
        this.rootElement.View = this;
        this.sizeChanged = true;
    }

    public UIElement AddChild(UIElement element) {
        throw new NotImplementedException();
    }

    internal void Initialize() {
        elementCount = 1;
        sizeChanged = true;
        rootElement.children.Clear();

        if (m_ElementType == null) {
            return;
        }

        CompiledTemplate compiledTemplate = Application.templateCompiler.GetCompiledTemplate(m_ElementType);
        LinqBindingNode bindingNode = new LinqBindingNode();
        UIElement element = compiledTemplate.Create(null, new TemplateScope2(Application, bindingNode, null));
        rootElement.AddChild(element);
    }

    public int GetElementCount() {
        return elementCount;
    }

    public void SetZIndex() { }

    public void SetCamera(Camera camera, CameraEvent renderHook) { }

    public void SetRenderTexture(RenderTexture texture) { }

    public void Destroy() {
        this.Application.RemoveView(this);
    }

    internal void ElementRegistered(UIElement element) {
        elementCount++;
        onElementRegistered?.Invoke(element);
    }

    internal void ElementCreated(UIElement element) {
        onElementCreated?.Invoke(element);
    }

    internal void ElementDestroyed(UIElement element) {
        elementCount--;
        onElementDestroyed?.Invoke(element);
    }

    internal void ElementReady(UIElement element) {
        onElementReady?.Invoke(element);
    }

    internal void ElementHierarchyEnabled(UIElement element) {
        onElementHierarchyEnabled?.Invoke(element);
    }

    internal void ElementHierarchyDisabled(UIElement element) {
        onElementHierarchyDisabled?.Invoke(element);
    }

    public void SetPosition(Vector2 position) {
        if (position != Viewport.position) {
            sizeChanged = true;
        }

        Viewport = new Rect(position.x, position.y, Viewport.width, Viewport.height);
    }

    public void SetSize(int width, int height) {
        if (width != Viewport.width || height != Viewport.height) {
            sizeChanged = true;
        }

        Viewport = new Rect(Viewport.x, Viewport.y, width, height);
    }

    public UIElement CreateElement<T>() {
        ParsedTemplate parsedTemplate = Application.templateParser.GetParsedTemplate(typeof(T));
        if (parsedTemplate == null) {
            return null;
        }

        try {
            // todo -- shouldn't auto - add to child list
            UIElement element = parsedTemplate.Create();
            rootElement.AddChild(element);
            return element;
        }
        catch {
            throw;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>true in case the depth has been changed in order to get focus</returns>
    public bool RequestFocus() {
        var views = Application.GetViews();
        if (focusOnMouseDown && Depth < views.Length - 1) {
            for (var index = 0; index < views.Length; index++) {
                UIView view = views[index];
                if (view.Depth > Depth) {
                    view.Depth--;
                }
            }

            Depth = views.Length - 1;
            Application.SortViews();
            return true;
        }

        return false;
    }

}
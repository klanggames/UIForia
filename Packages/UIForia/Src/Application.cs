using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UIForia.Animation;
using UIForia.AttributeProcessors;
using UIForia.Bindings;
using UIForia.Compilers;
using UIForia.Compilers.Style;
using UIForia.Elements;
using UIForia.Extensions;
using UIForia.Parsing.Expression;
using UIForia.Rendering;
using UIForia.Routing;
using UIForia.Systems;
using UIForia.Systems.Input;
using UIForia.Util;
using UnityEngine;

namespace UIForia {

    public abstract class Application {

#if UNITY_EDITOR
        public static List<Application> Applications = new List<Application>();
#endif

        public readonly string id;
        private static int ElementIdGenerator;
        public static int NextElementId => ElementIdGenerator++;
        private string templateRootPath;

        protected readonly BindingSystem m_BindingSystem;
        protected readonly IStyleSystem m_StyleSystem;
        protected ILayoutSystem m_LayoutSystem;
        protected IRenderSystem m_RenderSystem;
        protected IInputSystem m_InputSystem;
        protected RoutingSystem m_RoutingSystem;
        protected AnimationSystem m_AnimationSystem;
        protected LinqBindingSystem linqBindingSystem;

        public readonly StyleSheetImporter styleImporter;
        private readonly IntMap<UIElement> elementMap;
        protected readonly List<ISystem> m_Systems;

        public event Action<UIElement> onElementRegistered;
        public event Action<UIElement> onElementCreated;
        public event Action<UIElement> onElementDestroyed;
        public event Action<UIElement> onElementEnabled;
        public event Action<UIElement> onElementDisabled;

        public event Action onWillRefresh;
        public event Action onRefresh;
        public event Action onUpdate;
        public event Action onReady;
        public event Action onDestroy;
        public event Action onNextRefresh;
        public event Action<UIView> onViewAdded;
        public event Action<UIView[]> onViewsSorted;
        public event Action<UIView> onViewRemoved;

        internal TemplateData templateData;
        internal TemplateCompiler templateCompiler;

        protected internal readonly List<UIView> m_Views;

        public static readonly List<IAttributeProcessor> s_AttributeProcessors;

        internal static readonly Dictionary<string, ISVGXElementPainter> s_CustomPainters;
        internal static readonly Dictionary<string, Scrollbar> s_Scrollbars;

        public readonly TemplateParser templateParser;

        private static readonly LightList<Application> s_ApplicationList;

        private readonly UITaskSystem m_BeforeUpdateTaskSystem;
        private readonly UITaskSystem m_AfterUpdateTaskSystem;

        public static readonly UIForiaSettings Settings;
        private ElementPool elementPool;
        private LightList<UIElement> selectorUpdates;

        static Application() {
            ArrayPool<UIElement>.SetMaxPoolSize(64);
            s_AttributeProcessors = new List<IAttributeProcessor>();
            s_ApplicationList = new LightList<Application>();
            s_CustomPainters = new Dictionary<string, ISVGXElementPainter>();
            s_Scrollbars = new Dictionary<string, Scrollbar>();
            Settings = Resources.Load<UIForiaSettings>("UIForiaSettings");
            if (Settings == null) {
                throw new Exception("UIForiaSettings are missing. Use the UIForia/Create UIForia Settings to create it");
            }
        }

        // todo -- replace the static version with this one
        public UIForiaSettings settings => Settings;

        // todo -- override that accepts an index into an array instead of a type, to save a dictionary lookup
        // todo -- don't create a list for every type, maybe a single pool list w/ sorting & a jump search or similar
        public UIElement CreateElementFromPool(Type type, UIElement parent, int childCount) {
            UIElement retn = elementPool.Get(type);
            retn.children = LightList<UIElement>.GetMinSize(childCount);
            retn.children.size = childCount;
            if (parent != null) {
                // don't know sibling index here unless it is passed in to us
                retn.parent = parent;
                if (parent.isEnabled) {
                    retn.flags |= UIElementFlags.Enabled;
                }

                retn.depth = parent.depth + 1;
                retn.View = parent.View;
            }

            return retn;
        }

        protected Application(string id, string templateRootPath = null) {
            this.id = id;
            this.templateRootPath = templateRootPath;

            // todo -- exceptions in constructors aren't good practice
            if (s_ApplicationList.Find(id, (app, _id) => app.id == _id) != null) {
                throw new Exception($"Applications must have a unique id. Id {id} was already taken.");
            }

            s_ApplicationList.Add(this);

            this.templateData = new TemplateData(); // todo -- load this from elsewhere in the pre-generated case
            this.templateCompiler = new TemplateCompiler(this);

            this.elementPool = new ElementPool();
            this.m_Systems = new List<ISystem>();
            this.m_Views = new List<UIView>();
            this.selectorUpdates = new LightList<UIElement>();
            
            m_StyleSystem = new StyleSystem();
            m_BindingSystem = new BindingSystem();
            m_LayoutSystem = new LayoutSystem(this, m_StyleSystem);
            m_InputSystem = new GameInputSystem(m_LayoutSystem);
//            m_RenderSystem = new VertigoRenderSystem(Camera.current, m_LayoutSystem, m_StyleSystem); 
            m_RenderSystem = new SVGXRenderSystem(this, null, m_LayoutSystem);
            m_RoutingSystem = new RoutingSystem();
            m_AnimationSystem = new AnimationSystem();
            linqBindingSystem = new LinqBindingSystem();

            styleImporter = new StyleSheetImporter(this);
            templateParser = new TemplateParser(this);

            elementMap = new IntMap<UIElement>();

            m_Systems.Add(m_StyleSystem);
            m_Systems.Add(m_BindingSystem);
            m_Systems.Add(m_RoutingSystem);
            m_Systems.Add(m_InputSystem);
            m_Systems.Add(m_AnimationSystem);
            m_Systems.Add(m_LayoutSystem);
            m_Systems.Add(m_RenderSystem);

            m_BeforeUpdateTaskSystem = new UITaskSystem();
            m_AfterUpdateTaskSystem = new UITaskSystem();

#if UNITY_EDITOR
            Applications.Add(this);
#endif
        }

        internal static void ProcessClassAttributes(Type type, Attribute[] attrs) {
            for (var i = 0; i < attrs.Length; i++) {
                Attribute attr = attrs[i];
                if (attr is CustomPainterAttribute paintAttr) {
                    if (type.GetConstructor(Type.EmptyTypes) == null || type.GetInterface(nameof(ISVGXElementPainter)) == null) {
                        throw new Exception($"Classes marked with [{nameof(CustomPainterAttribute)}] must provide a parameterless constructor" +
                                            $" and the class must implement {nameof(ISVGXElementPainter)}. Ensure that {type.FullName} conforms to these rules");
                    }

                    if (s_CustomPainters.ContainsKey(paintAttr.name)) {
                        throw new Exception($"Failed to register a custom painter with the name {paintAttr.name} from type {type.FullName} because it was already registered.");
                    }

                    s_CustomPainters.Add(paintAttr.name, (ISVGXElementPainter) Activator.CreateInstance(type));
                }
                else if (attr is CustomScrollbarAttribute scrollbarAttr) {
                    if (type.GetConstructor(Type.EmptyTypes) == null || !(typeof(Scrollbar)).IsAssignableFrom(type)) {
                        throw new Exception($"Classes marked with [{nameof(CustomScrollbarAttribute)}] must provide a parameterless constructor" +
                                            $" and the class must extend {nameof(Scrollbar)}. Ensure that {type.FullName} conforms to these rules");
                    }

                    if (s_Scrollbars.ContainsKey(scrollbarAttr.name)) {
                        throw new Exception($"Failed to register a custom scrollbar with the name {scrollbarAttr.name} from type {type.FullName} because it was already registered.");
                    }

                    s_Scrollbars.Add(scrollbarAttr.name, (Scrollbar) Activator.CreateInstance(type));
                }
            }
        }

        public string TemplateRootPath {
            get {
                if (templateRootPath == null) {
                    return string.Empty; // UnityEngine.Application.dataPath;
                }

                return templateRootPath;
            }
            set { templateRootPath = value; }
        }

        public IStyleSystem StyleSystem => m_StyleSystem;
        public BindingSystem BindingSystem => m_BindingSystem;
        public IRenderSystem RenderSystem => m_RenderSystem;
        public ILayoutSystem LayoutSystem => m_LayoutSystem;
        public IInputSystem InputSystem => m_InputSystem;
        public RoutingSystem RoutingSystem => m_RoutingSystem;

        public Camera Camera { get; private set; }
        public LinqBindingSystem LinqBindingSystem => linqBindingSystem;

        // Doesn't expect to create the root
        internal void HydrateTemplate(int templateId, UIElement root, TemplateScope2 scope) {
            templateData.templateFns[templateId](root, scope);
        }

        public void SetCamera(Camera camera) {
            Camera = camera;
            RenderSystem.SetCamera(camera);
        }

        private int nextViewId = 0;

        public UIView CreateView(string name, Rect rect, Type type, string template = null) {
            UIView view = GetView(name);

            if (view == null) {
                view = new UIView(nextViewId++, name, this, rect, m_Views.Count, type, template);
                m_Views.Add(view);

                for (int i = 0; i < m_Systems.Count; i++) {
                    m_Systems[i].OnViewAdded(view);
                }

                view.Initialize();

                onViewAdded?.Invoke(view);
            }
            else {
                if (view.RootElement.GetType() != type) {
                    throw new Exception($"A view named {name} with another root type ({view.RootElement.GetType()}) already exists.");
                }

                view.Viewport = rect;
            }

            return view;
        }

        public UIView CreateView(string name, Rect rect) {
            UIView view = new UIView(nextViewId++, name, this, rect, m_Views.Count);

            m_Views.Add(view);

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnViewAdded(view);
            }

            view.Initialize();

            onViewAdded?.Invoke(view);
            return view;
        }

        public UIView RemoveView(UIView view) {
            if (!m_Views.Remove(view)) return null;

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnViewRemoved(view);
            }

            onViewRemoved?.Invoke(view);
            DestroyElement(view.rootElement);
            return view;
        }

        public UIElement CreateElement(Type type) {
            if (type == null) {
                return null;
            }

            return templateParser.GetParsedTemplate(type)?.Create();
        }

        public T CreateElement<T>() where T : UIElement {
            return templateParser.GetParsedTemplate(typeof(T))?.Create() as T;
        }

        public void Refresh() {
            onWillRefresh?.Invoke();

            foreach (ISystem system in m_Systems) {
                system.OnReset();
            }

            onReady = null;
            onUpdate = null;

            elementMap.Clear();
            templateParser.Reset();
            styleImporter.Reset();
            ResourceManager.Reset();

            m_AfterUpdateTaskSystem.OnReset();
            m_BeforeUpdateTaskSystem.OnReset();

            // copy the list here because there might be view-sorting going on during view.initialize() 
            LightList<UIView> views = LightList<UIView>.Get();
            views.AddRange(m_Views);

            // todo -- store root view, rehydrate. kill the rest
            for (int i = 0; i < views.Count; i++) {
                for (int j = 0; j < m_Systems.Count; j++) {
                    m_Systems[j].OnViewAdded(views[i]);
                }

                views[i].Initialize();
            }

            LightList<UIView>.Release(ref views);

            onRefresh?.Invoke();
            onNextRefresh?.Invoke();
            onNextRefresh = null;
            onReady?.Invoke();
        }

        public void Destroy() {
#if UNITY_EDITOR
            Applications.Remove(this);
#endif
            onDestroy?.Invoke();

            foreach (ISystem system in m_Systems) {
                system.OnDestroy();
            }

            foreach (UIView view in m_Views) {
                view.Destroy();
            }

            onRefresh = null;
            onNextRefresh = null;
            onReady = null;
            onUpdate = null;
            onDestroy = null;
            onNextRefresh = null;
            onElementCreated = null;
            onElementEnabled = null;
            onElementDisabled = null;
            onElementDestroyed = null;
            onElementRegistered = null;
        }

        private static void InvokeAttributeProcessors(UIElement element) {
            List<ElementAttribute> attributes = element.GetAttributes();

            // todo -- the origin template can figure out which processors to invoke at compile time, saves potentially a lot of cycles

            for (int i = 0; i < s_AttributeProcessors.Count; i++) {
                s_AttributeProcessors[i].Process(element, element.OriginTemplate, attributes);
            }

            if (element.children == null) return;

            for (int i = 0; i < element.children.Count; i++) {
                InvokeAttributeProcessors(element.children[i]);
            }
        }

        public static void DestroyElement(UIElement element) {
            element.View.Application.DoDestroyElement(element);
        }

        internal void DoDestroyElement(UIElement element) {
            if ((element.flags & UIElementFlags.Destroyed) != 0) {
                return;
            }

            LightStack<UIElement> stack = new LightStack<UIElement>();
            LightList<UIElement> toInternalDestroy = LightList<UIElement>.Get();

            stack.Push(element);

            while (stack.Count > 0) {
                UIElement current = stack.PopUnchecked();

                UIElement[] children = current.children.Array;
                int childCount = current.children.Count;
                for (int i = childCount - 1; i >= 0; i--) {
                    stack.Push(children[i]);
                }

                if (!current.isDestroyed) {
                    current.flags |= UIElementFlags.Destroyed;
                    current.OnDestroy();
                    toInternalDestroy.Add(current);
                }
            }


            if (element.parent != null) {
                element.parent.children.Remove(element);
                for (int i = 0; i < element.parent.children.Count; i++) {
                    element.parent.children[i].siblingIndex = i;
                }
            }


            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnElementDestroyed(element);
            }

            if (toInternalDestroy.Count > 0) {
                UIView view = toInternalDestroy[0].View;
                for (int i = 0; i < toInternalDestroy.Count; i++) {
                    view.ElementDestroyed(toInternalDestroy[i]);
                    toInternalDestroy[i].InternalDestroy();
                    elementMap.Remove(toInternalDestroy[i].id);
                }
            }

            LightList<UIElement>.Release(ref toInternalDestroy);

            onElementDestroyed?.Invoke(element);

            // todo -- if element is poolable, pool it here
            LightStack<UIElement>.Release(ref stack);
        }

        internal void DestroyChildren(UIElement element) {
            if (element.isDestroyed) {
                return;
            }

            if (element.children == null || element.children.Count == 0) {
                return;
            }

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            LightList<UIElement> toInternalDestroy = LightList<UIElement>.Get();

            int childCount = element.children.Count;
            UIElement[] children = element.children.Array;

            for (int i = 0; i < childCount; i++) {
                stack.Push(children[i]);
            }

            while (stack.Count > 0) {
                UIElement current = stack.PopUnchecked();

                if (!current.isDestroyed) {
                    current.flags &= ~(UIElementFlags.Enabled);
                    current.flags |= UIElementFlags.Destroyed;
                    current.OnDestroy();
                    toInternalDestroy.Add(current);
                }

                childCount = current.children.Count;
                children = current.children.Array;

                for (int i = childCount - 1; i >= 0; i--) {
                    stack.Push(children[i]);
                }
            }

            for (int i = 0; i < element.children.Count; i++) {
                for (int j = 0; j < m_Systems.Count; j++) {
                    m_Systems[j].OnElementDestroyed(element.children[i]);
                }
            }

            if (toInternalDestroy.Count > 0) {
                UIView view = toInternalDestroy[0].View;
                for (int i = 0; i < toInternalDestroy.Count; i++) {
                    view.ElementDestroyed(toInternalDestroy[i]);
                    toInternalDestroy[i].InternalDestroy();
                    elementMap.Remove(toInternalDestroy[i].id);
                }
            }

            LightList<UIElement>.Release(ref toInternalDestroy);
            element.children.Clear();
        }

        public void Update() {
            m_InputSystem.OnUpdate();
            linqBindingSystem.OnUpdate();
            
            m_StyleSystem.UpdateSelectors();

            UIElement[] elements = selectorUpdates.array;
            for (int i = 0; i < selectorUpdates.size; i++) {
                UIElement element = elements[i];
                if (element.isEnabled) {
                    element.selectorNode.Update(element);
                }

                element.flags &= ~(UIElementFlags.SelectorNeedsUpdate);
                
            }
            
            selectorUpdates.QuickClear();
            
            m_StyleSystem.UpdateBindings();
            m_StyleSystem.UpdateAnimations();
            m_StyleSystem.Flush();

            // these should be one thing, single pass?
            m_LayoutSystem.OnUpdate();
            m_RenderSystem.OnUpdate();

            onUpdate?.Invoke();

            m_Views[0].SetSize(Screen.width, Screen.height);
        }

        /// <summary>
        /// Note: you don't need to remove tasks from the system. Any canceled or otherwise completed task gets removed
        /// from the system automatically.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public UITask RegisterBeforeUpdateTask(UITask task) {
            return m_BeforeUpdateTaskSystem.AddTask(task);
        }

        /// <summary>
        /// Note: you don't need to remove tasks from the system. Any canceled or otherwise completed task gets removed
        /// from the system automatically.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public UITask RegisterAfterUpdateTask(UITask task) {
            return m_AfterUpdateTaskSystem.AddTask(task);
        }

        public static void EnableElement(UIElement element) {
            element.View.Application.DoEnableElement(element);
        }

        public static void DisableElement(UIElement element) {
            element.View.Application.DoDisableElement(element);
        }

        private static void RunEnableBinding(UIElement element) {
            Binding[] enabledBindings = element.OriginTemplate?.triggeredBindings;

            if (enabledBindings != null) {
                for (int i = 0; i < enabledBindings.Length; i++) {
                    if (enabledBindings[i].bindingType == BindingType.OnEnable) {
                        enabledBindings[i].Execute(element, element.templateContext);
                    }
                }
            }
        }

        public void DoEnableElement(UIElement element) {
            if (element.isDestroyed) {
                return;
            }

            element.flags |= UIElementFlags.Enabled;
            // if element is not enabled (ie has a disabled ancestor), no-op 
            if (!element.isEnabled) return;

            int targetPhase = -1;

            UIElement ptr = element.parent;
            if (!ptr.isReady) {
                while (ptr != null) {
                    if (ptr.enablePhase != 0) { // todo -- remove this element field and make it an element flag
                        targetPhase = ptr.enablePhase;
                        break;
                    }

                    ptr = ptr.parent;
                }
            }

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            UIElement[] children;
            int childCount;

            stack.Push(element);

            element.enablePhase = 1;
            while (stack.Count > 0) {
                UIElement child = stack.PopUnchecked();

                if (child.isDestroyed) {
                    continue;
                }

                if (child.parent.isEnabled) {
                    child.flags |= UIElementFlags.AncestorEnabled;
                }

                if (child.isEnabled && !child.isCreated) {
                    child.flags |= UIElementFlags.Created;
                    child.OnCreate();
                    child.View.ElementCreated(child);
                }

                if (child.isDisabled) {
                    continue;
                }

                children = child.children.Array;
                childCount = child.children.Count;
                for (int i = childCount - 1; i >= 0; i--) {
                    stack.Push(children[i]);
                }
            }

            // if element is now enabled we need to walk it's children
            // and set enabled ancestor flags until we find a self-disabled child
            stack.Push(element);
            element.enablePhase = 2;

            if (!element.isEnabled || (targetPhase != -1 && targetPhase <= 2)) {
                element.enablePhase = 0;
                LightStack<UIElement>.Release(ref stack);
                return;
            }

            element.flags |= UIElementFlags.AncestorEnabled;

            foreach (ISystem system in m_Systems) {
                system.OnElementEnabled(element);
            }

            while (stack.Count > 0) {
                UIElement child = stack.PopUnchecked();
                child.flags |= UIElementFlags.AncestorEnabled;

                if (child.isSelfDisabled || child.isDestroyed) {
                    continue;
                }

                child.flags |= UIElementFlags.HasBeenEnabled;

                child.OnEnable();

                if (child.isEnabled) {
                    RunEnableBinding(child);
                    // need this isEnabled check in case a binding disabled the element
                    if (!child.isEnabled) {
                        continue;
                    }

                    children = child.children.Array;
                    childCount = child.children.Count;
                    for (int i = childCount - 1; i >= 0; i--) {
                        stack.Push(children[i]);
                    }
                }
            }

            element.enablePhase = 3;

            if (targetPhase != -1 && targetPhase <= 3) {
                element.enablePhase = 0;
                LightStack<UIElement>.Release(ref stack);
                return;
            }

            if (element.isEnabled) {
                element.View.ElementHierarchyEnabled(element);
                onElementEnabled?.Invoke(element);

                stack.Push(element);

                while (stack.Count > 0) {
                    UIElement child = stack.PopUnchecked();

                    if (child.isDestroyed) {
                        continue;
                    }

                    if (child.isEnabled && !child.isReady) {
                        child.flags |= UIElementFlags.Ready;
                        child.OnReady();
                        child.View.ElementReady(element);
                    }

                    if (child.isDisabled) {
                        continue;
                    }

                    children = child.children.Array;
                    childCount = child.children.Count;
                    for (int i = childCount - 1; i >= 0; i--) {
                        stack.Push(children[i]);
                    }
                }
            }

            element.enablePhase = 0;
            LightStack<UIElement>.Release(ref stack);
        }

        // todo bad things happen if we add children during disabling or enabling (probably)

        public void DoDisableElement(UIElement element) {
            // no-op for already disabled elements
            if (!element.isCreated || element.isDisabled) {
                element.flags &= ~(UIElementFlags.Enabled);
                return;
            }

            element.flags &= ~(UIElementFlags.Enabled);

            // if element was already disabled via ancestor, no-op
            if (element.hasDisabledAncestor) {
                return;
            }

            element.OnDisable();

            if (element.isEnabled) {
                return;
            }

            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            UIElement[] children = element.children.Array;
            int childCount = element.children.Count;

            for (int i = childCount - 1; i >= 0; i--) {
                stack.Push(children[i]);
            }

            while (stack.Count > 0) {
                UIElement child = stack.PopUnchecked();
                child.flags &= ~(UIElementFlags.AncestorEnabled);

                if (!child.isCreated) {
                    continue;
                }

                if ((child.flags & UIElementFlags.HasBeenEnabled) != 0) {
                    child.OnDisable();
                }

                if (child.isDisabled) {
                    children = child.children.Array;
                    childCount = child.children.Count;
                    for (int i = childCount - 1; i >= 0; i--) {
                        stack.Push(children[i]);
                    }
                }
            }

            LightStack<UIElement>.Release(ref stack);

            if (element.isDisabled) {
                foreach (ISystem system in m_Systems) {
                    system.OnElementDisabled(element);
                }

                element.View.ElementHierarchyDisabled(element);
                onElementDisabled?.Invoke(element);
            }
        }

        public UIElement GetElement(int elementId) {
            return elementMap.GetOrDefault(elementId);
        }

     

        public static void RefreshAll() {
            for (int i = 0; i < s_ApplicationList.Count; i++) {
                s_ApplicationList[i].Refresh();
            }
        }

        public UIView GetView(int i) {
            if (i < 0 || i >= m_Views.Count) return null;
            return m_Views[i];
        }

        public UIView GetView(string name) {
            for (int i = 0; i < m_Views.Count; i++) {
                UIView v = m_Views[i];
                if (v.name == name) {
                    return v;
                }
            }

            return null;
        }

        public static Application Find(string appId) {
            return s_ApplicationList.Find(appId, (app, _id) => app.id == _id);
        }

        public static bool HasCustomPainter(string name) {
            return s_CustomPainters.ContainsKey(name);
        }

        public static ISVGXElementPainter GetCustomPainter(string name) {
            return s_CustomPainters.GetOrDefault(name);
        }

        public static Scrollbar GetCustomScrollbar(string name) {
            if (string.IsNullOrEmpty(name)) {
                return s_Scrollbars["UIForia.Default"];
            }

            return s_Scrollbars.GetOrDefault(name);
        }

        public void Animate(UIElement element, AnimationData animation) {
            m_AnimationSystem.Animate(element, animation);
        }

        public UIView[] GetViews() {
            return m_Views.ToArray();
        }

        internal void InsertChild(UIElement parent, UIElement child, uint index) {
            if (child.parent != null) {
                throw new NotImplementedException("Reparenting is not supported");
            }

            bool hasView = child.View != null;

            // we don't know the hierarchy at this point.
            // could be made up of a mix of elements in various states

            child.parent = parent;
            parent.children.Insert((int) index, child);

            if (hasView) {
                throw new NotImplementedException("Changing views is not supported");
            }

            bool parentEnabled = parent.isEnabled;

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            UIView view = parent.View;
            stack.Push(child);

            view.BeginAddingElements();

            while (stack.Count > 0) {
                UIElement current = stack.Pop();

                current.depth = current.parent.depth + 1;

                // todo -- we don't support changing views or any sort of re-parenting

                current.View = view;

                if (current.parent.isEnabled) {
                    current.flags |= UIElementFlags.AncestorEnabled;
                }
                else {
                    current.flags &= ~UIElementFlags.AncestorEnabled;
                }

                elementMap[current.id] = current;

                if (!current.isRegistered) {
                    current.style.Initialize();
                    current.flags |= UIElementFlags.Registered;
                    for (int i = 0; i < m_Systems.Count; i++) {
                        m_Systems[i].OnElementCreated(current);
                    }

                    view.ElementRegistered(current);
                    onElementRegistered?.Invoke(current);
                }

                UIElement[] children = current.children.Array;
                int childCount = current.children.Count;
                // reverse this?
                for (int i = 0; i < childCount; i++) {
                    children[i].siblingIndex = i;
                    stack.Push(children[i]);
                }
            }

            for (int i = 0; i < parent.children.Count; i++) {
                parent.children[i].siblingIndex = i;
            }

            view.EndAddingElements();

            LightStack<UIElement>.Release(ref stack);

            if (parentEnabled && child.isEnabled) {
                child.flags &= ~UIElementFlags.Enabled;
                DoEnableElement(child);
            }
        }

        public void SortViews() {
            // let's bubble sort the views since only once view is out of place
            for (int i = (m_Views.Count - 1); i > 0; i--) {
                for (int j = 1; j <= i; j++) {
                    if (m_Views[j - 1].Depth > m_Views[j].Depth) {
                        UIView tempView = m_Views[j - 1];
                        m_Views[j - 1] = m_Views[j];
                        m_Views[j] = tempView;
                    }
                }
            }

            onViewsSorted?.Invoke(m_Views.ToArray());
        }


        internal UIElement InsertChildFromTemplate(UIElement parent, in StoredTemplate storedTemplate, uint index) {
            // CompiledTemplate template = templateData.templateFns[storedTemplate.templateId];
            // template.CreateStored(parent, storedTemplate.closureRoot, slots?, context? bindingNode?)
//            UIElement child = template.Create(parent, new TemplateScope2(this, null, StructList<SlotUsage>.Get()));
//            return child;
            throw new NotImplementedException();
        }

        internal LightList<SlotUsageTemplate> slotUsageTemplates = new LightList<SlotUsageTemplate>(128);

        // todo we will want to not compile this here, explore jitting this
        internal int AddSlotUsageTemplate(Expression<SlotUsageTemplate> lambda) {
            throw new NotImplementedException(); // todo move this to template data
            slotUsageTemplates.Add(lambda.Compile());
            return slotUsageTemplates.Count - 1;
        }

        internal bool TryCreateSlot(StructList<SlotUsage> slots, string targetSlot, LinqBindingNode bindingNode, UIElement parent, out UIElement element) {
            SlotUsage[] array = slots.array;
            for (int i = 0; i < slots.size; i++) {
                if (array[i].slotName == targetSlot) {
                    element = slotUsageTemplates[array[i].templateId].Invoke(this, bindingNode, parent, array[i].lexicalScope);
                    element.View = parent.View;
                    return true;
                }
            }

            element = null;
            return false;
        }

        internal UIElement CreateSlot(StructList<SlotUsage> slots, string targetSlot, LinqBindingNode bindingNode, UIElement parent, UIElement root, CompiledTemplate defaultTemplateData, int defaultTemplateId) {
            UIElement element;

            if (slots == null) {
                element = slotUsageTemplates[defaultTemplateId].Invoke(this, bindingNode, parent, new LexicalScope(root, defaultTemplateData));
                element.View = parent.View;
                element.parent = parent;
                return element;
            }

            SlotUsage[] array = slots.array;
            for (int i = 0; i < slots.size; i++) {
                if (array[i].slotName == targetSlot) {
                    element = slotUsageTemplates[array[i].templateId].Invoke(this, bindingNode, parent, array[i].lexicalScope);
                    element.parent = parent;
                    element.View = parent.View;
                    return element;
                }
            }

            element = slotUsageTemplates[defaultTemplateId].Invoke(this, bindingNode, parent, new LexicalScope(root, defaultTemplateData));
            element.View = parent.View;
            element.parent = parent;
            return element;
        }

        
        public void OnAttributeAdded(UIElement element, string name, string value) {
            
            if ((element.flags & UIElementFlags.SelectorNeedsUpdate) == 0) {
                selectorUpdates.Add(element);
            }

            element.flags |= UIElementFlags.Selector_AttributeAdded;
        
        }
        
        public void OnAttributeRemoved(UIElement element, string name, string value) {
            
            if ((element.flags & UIElementFlags.SelectorNeedsUpdate) == 0) {
                selectorUpdates.Add(element);
            }

            element.flags |= UIElementFlags.Selector_AttributeRemoved;
        
        }
        
        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string previousValue) {
            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnAttributeSet(element, attributeName, currentValue, previousValue);
            }
            
            if ((element.flags & UIElementFlags.SelectorNeedsUpdate) == 0) {
                selectorUpdates.Add(element);
            }

            element.flags |= UIElementFlags.Selector_AttributeChanged;

        }

    }

}
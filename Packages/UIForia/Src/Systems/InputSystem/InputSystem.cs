using System;
using System.Collections.Generic;
using UIForia.Rendering;
using UIForia.Elements;
using UIForia.Expressions;
using UIForia.Systems.Input;
using UIForia.Templates;
using UIForia.UIInput;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Systems {

    public abstract class InputSystem : IInputSystem {

        private struct PressedKey {

            public readonly KeyCode keyCode;
            public readonly char character;

            public PressedKey(KeyCode keyCode, char character) {
                this.keyCode = keyCode;
                this.character = character;
            }

        }

        public event Action<IFocusable> onFocusChanged;

        private const float k_DragThreshold = 5f;

        private readonly ILayoutSystem m_LayoutSystem;

        private List<UIElement> m_ElementsThisFrame;
        
#if UNITY_EDITOR
        public List<UIElement> DebugElementsThisFrame => m_ElementsLastFrame;
        public bool DebugMouseUpThisFrame => m_MouseState.isLeftMouseUpThisFrame;
#endif

        private List<UIElement> m_AllElementsThisFrame;
        private List<UIElement> m_ElementsLastFrame;
        private List<UIElement> m_AllElementsLastFrame;

        // temporary hack for the building system, this should be formalized and use ElementRef instead
        public IReadOnlyList<UIElement> ElementsThisFrame => m_ElementsLastFrame;    

        private CursorStyle currentCursor;

        protected UIElement m_FocusedElement;
        protected DragEvent m_CurrentDragEvent;
        protected MouseInputEvent m_CurrentMouseEvent;
        protected KeyboardInputEvent m_CurrentKeyboardEvent;

        private KeyboardModifiers modifiersThisFrame;

        protected MouseState m_MouseState;

        private readonly List<UIElement> m_ExitedElements;
        private readonly List<UIElement> m_ActiveElements;
        private readonly List<UIElement> m_EnteredElements;
        private readonly List<UIElement> m_MouseDownElements;

        private readonly Dictionary<KeyCode, KeyState> m_KeyStates;
        private readonly Dictionary<int, MouseHandlerGroup> m_MouseHandlerMap;
        private readonly Dictionary<int, DragHandlerGroup> m_DragHandlerMap;
        private readonly Dictionary<int, DragCreatorGroup> m_DragCreatorMap;
        private readonly SkipTree<KeyboardEventTreeNode> m_KeyboardEventTree;

        private readonly LightList<KeyCode> m_DownThisFrame;
        private readonly LightList<KeyCode> m_UpThisFrame;
        private readonly LightList<PressedKey> m_PressedKeys;

        private readonly EventPropagator m_EventPropagator;
        private readonly List<ValueTuple<MouseEventHandler, UIElement, ExpressionContext>> m_MouseEventCaptureList;
        private readonly List<ValueTuple<DragEventHandler, UIElement, ExpressionContext>> m_DragEventCaptureList;
        protected static readonly ZIndexComparerAscending s_DepthComparer = new ZIndexComparerAscending();
        private static readonly Event s_Event = new Event();

        public KeyboardModifiers KeyboardModifiers => modifiersThisFrame;

        private LightList<KeyboardEventHandlerInvocation> lateHandlers = new LightList<KeyboardEventHandlerInvocation>();
        private LightList<UIEvent> lateTriggers = new LightList<UIEvent>();

        protected InputSystem(ILayoutSystem layoutSystem) {
            this.m_LayoutSystem = layoutSystem;

            this.m_MouseDownElements = new List<UIElement>();
            this.m_ElementsThisFrame = new List<UIElement>();
            this.m_ElementsLastFrame = new List<UIElement>();
            this.m_EnteredElements = new List<UIElement>();
            this.m_ExitedElements = new List<UIElement>();
            this.m_ActiveElements = new List<UIElement>();
            this.m_AllElementsThisFrame = new List<UIElement>();
            this.m_AllElementsLastFrame = new List<UIElement>();

            this.m_MouseHandlerMap = new Dictionary<int, MouseHandlerGroup>();
            this.m_DragCreatorMap = new Dictionary<int, DragCreatorGroup>();
            this.m_DragHandlerMap = new Dictionary<int, DragHandlerGroup>();

            this.m_PressedKeys = new LightList<PressedKey>(16);
            this.m_UpThisFrame = new LightList<KeyCode>();
            this.m_DownThisFrame = new LightList<KeyCode>();
            this.m_KeyStates = new Dictionary<KeyCode, KeyState>();
            this.m_KeyboardEventTree = new SkipTree<KeyboardEventTreeNode>();

            this.m_EventPropagator = new EventPropagator();
            this.m_MouseEventCaptureList = new List<ValueTuple<MouseEventHandler, UIElement, ExpressionContext>>();
            this.m_DragEventCaptureList = new List<ValueTuple<DragEventHandler, UIElement, ExpressionContext>>();
            this.m_FocusedElement = null;
        }

        public DragEvent CurrentDragEvent => m_CurrentDragEvent;
        public MouseInputEvent CurrentMouseEvent => m_CurrentMouseEvent;
        public KeyboardInputEvent CurrentKeyboardEvent => m_CurrentKeyboardEvent;

        public bool IsMouseLeftDown => m_MouseState.isLeftMouseDown;
        public bool IsMouseLeftDownThisFrame => m_MouseState.isLeftMouseDownThisFrame;
        public bool IsMouseLeftUpThisFrame => m_MouseState.isLeftMouseUpThisFrame;

        public bool IsMouseRightDown => m_MouseState.isRightMouseDown;
        public bool IsMouseRightDownThisFrame => m_MouseState.isRightMouseDownThisFrame;
        public bool IsMouseRightUpThisFrame => m_MouseState.isRightMouseUpThisFrame;

        public bool IsMouseMiddleDown => m_MouseState.isMiddleMouseDown;
        public bool IsMouseMiddleDownThisFrame => m_MouseState.isMiddleMouseDownThisFrame;
        public bool IsMouseMiddleUpThisFrame => m_MouseState.isMiddleMouseUpThisFrame;

        public Vector2 ScrollDelta => m_MouseState.scrollDelta;

        public Vector2 MousePosition => m_MouseState.mousePosition;
        public Vector2 MouseDownPosition => m_MouseState.leftMouseButtonState.downPosition;

        public bool IsDragging { get; protected set; }

        protected abstract MouseState GetMouseState();

        public IFocusable GetFocusedElement() {
            return (IFocusable) m_FocusedElement;
        }

        public bool RequestFocus(IFocusable target) {
            if (!(target is UIElement)) {
                return false;
            }

            // todo -- if focus handlers added via template invoke them
            if (m_FocusedElement != null) {
                if (m_FocusedElement == (UIElement) target) {
                    return true;
                }

                IFocusable focusable = (IFocusable) m_FocusedElement;
                m_FocusedElement.style.ExitState(StyleState.Focused);
                focusable.Blur();
                onFocusChanged?.Invoke(target);
            }

            m_FocusedElement = (UIElement) target;
            target.Focus();
            m_FocusedElement.style.EnterState(StyleState.Focused);
            onFocusChanged?.Invoke(target);
            return true;
        }

        public void ReleaseFocus(IFocusable target) {
            if (m_FocusedElement == (UIElement) target) {
                IFocusable focusable = (IFocusable) m_FocusedElement;
                m_FocusedElement.style.ExitState(StyleState.Focused);
                focusable.Blur();
                // todo -- if focus handlers added via template invoke them
                m_FocusedElement = null;
                onFocusChanged?.Invoke(target);
            }
        }

        public virtual void OnUpdate() {
            m_MouseState = GetMouseState();

            ProcessKeyboardEvents();
            ProcessMouseInput();

            if (!IsDragging) {
                ProcessMouseEvents();
            }

            ProcessDragEvents();

            List<UIElement> temp = m_ElementsLastFrame;
            m_ElementsLastFrame = m_ElementsThisFrame;
            m_ElementsThisFrame = temp;

            for (int i = 0; i < m_ElementsLastFrame.Count; i++) {
                if (m_ElementsLastFrame[i].isDisabled || m_ElementsLastFrame[i].isDestroyed) {
                    m_ElementsLastFrame.RemoveAt(i--);
                }
            }

            m_ElementsThisFrame.Clear();
            m_EnteredElements.Clear();
            m_ExitedElements.Clear();

            if (IsMouseLeftUpThisFrame) {
                m_MouseDownElements.Clear();
            }
        }

        public virtual void DelayEvent(UIElement origin, UIEvent evt) {
            evt.origin = origin;
            lateTriggers.Add(evt);
        }

        public virtual void OnLateUpdate() {
            int lateHandlersCount = lateHandlers.Count;
            KeyboardEventHandlerInvocation[] invocations = lateHandlers.Array;
            for (int index = 0; index < lateHandlersCount; index++) {
                KeyboardEventHandlerInvocation invocation = invocations[index];
                invocation.handler.Invoke(invocation.target, invocation.context, invocation.evt);
            }

            lateHandlers.Clear();

            for (int index = 0; index < lateTriggers.Count; index++) {
                UIEvent uiEvent = lateTriggers[index];
                uiEvent.origin.TriggerEvent(uiEvent);
            }

            lateTriggers.Clear();
        }

        private void ProcessMouseInput() {
            LightList<UIElement> queryResults = (LightList<UIElement>) m_LayoutSystem.QueryPoint(m_MouseState.mousePosition, LightList<UIElement>.Get());

            if (!IsDragging) {
                LightList<UIElement> ancestorElements = LightList<UIElement>.Get();

                // the first element is always correct
                if (queryResults.Count > 0) {
                    ancestorElements.Add(queryResults[0]);
                }
                /*
                 * Every following element must be a parent of the first.
                 * This makes no sense for drag events but a lot for every other.
                 */
                for (int index = 1; index < queryResults.Count; index++) {
                    UIElement element = queryResults[index];
                    if (IsParentOf(element, queryResults[0])) {
                        ancestorElements.Add(element);
                    }
                }

                LightList<UIElement>.Release(ref queryResults);
                queryResults = ancestorElements;
            }
            
            for (int i = 0; i < queryResults.Count; i++) {
                UIElement element = queryResults[i];

                // todo -- handle masking here
                m_ElementsThisFrame.Add(element);

                if (!m_ElementsLastFrame.Contains(element)) {
                    m_EnteredElements.Add(element); 
                    element.style?.EnterState(StyleState.Hover);
                }

                if (IsMouseLeftDownThisFrame) {
                    element.style?.EnterState(StyleState.Active);
                    m_ActiveElements.Add(element);
                }
            }

            for (int i = 0; i < m_ElementsLastFrame.Count; i++) {
                if (!m_ElementsThisFrame.Contains(m_ElementsLastFrame[i])) {
                    m_ExitedElements.Add(m_ElementsLastFrame[i]);
                    m_ElementsLastFrame[i].style?.ExitState(StyleState.Hover);
                }
            }
            
            if (IsMouseLeftUpThisFrame) {
                for (int i = 0; i < m_ActiveElements.Count; i++) {
                    m_ActiveElements[i].style?.ExitState(StyleState.Active);
                }
                m_ActiveElements.Clear();
            }

            m_EnteredElements.Sort(s_DepthComparer);
            m_ElementsThisFrame.Sort(s_DepthComparer);

            if (!IsDragging) {
                CursorStyle newCursor = null;
                if (m_ElementsThisFrame.Count > 0) {
                    for (int i = 0; i < m_ElementsThisFrame.Count; i++) {
                        UIElement element = m_ElementsThisFrame[i];

                        if (element.style.IsDefined(StylePropertyId.Cursor)) {
                            newCursor = element.style.Cursor;
                            if (!newCursor.Equals(currentCursor)) {
                                Cursor.SetCursor(newCursor.texture, newCursor.hotSpot, CursorMode.Auto);
                            }

                            break;
                        }
                    }
                }

                if (currentCursor != null && newCursor == null) {
                    Cursor.SetCursor(null, new Vector2(0, 0), CursorMode.Auto);
                }

                currentCursor = newCursor;

                if (m_MouseState.AnyMouseDownThisFrame) {
                    m_MouseDownElements.AddRange(m_ElementsThisFrame);
                }
            }

            LightList<UIElement>.Release(ref queryResults);
        }
        
        private static bool IsParentOf(UIElement element, UIElement child) {
            UIElement ptr = child.parent;
            while (ptr != null) {
                if (ptr == element) {
                    return true;
                }
                ptr = ptr.parent;
            }

            return false;
        }
        
        private void ProcessDragEvents() {
            if (IsDragging) {
                if (m_MouseState.ReleasedDrag) {
                    EndDrag(InputEventType.DragDrop);
                    m_MouseDownElements.Clear();
                }
                else {
                    UpdateDrag();
                }
            }
            else if (m_MouseState.AnyMouseDown) {
                if (Vector2.Distance(m_MouseState.MouseDownPosition, m_MouseState.mousePosition) >= k_DragThreshold) {
                    BeginDrag();
                }
            }
        }

        private void UpdateDrag(bool firstFrame = false) {
            if (m_CurrentDragEvent == null) {
                return;
            }

            m_CurrentDragEvent.MousePosition = MousePosition;
            m_CurrentDragEvent.Modifiers = modifiersThisFrame;

            if (firstFrame) {
                RunDragEvent(m_ElementsThisFrame, InputEventType.DragEnter);
                m_CurrentDragEvent.Update();
            }
            else {
                RunDragEvent(m_ExitedElements, InputEventType.DragExit);
                RunDragEvent(m_EnteredElements, InputEventType.DragEnter);
                m_CurrentDragEvent.Update();
                RunDragEvent(m_ElementsThisFrame, m_MouseState.DidMove ? InputEventType.DragMove : InputEventType.DragHover);
            }

            if (m_CurrentDragEvent.IsCanceled) {
                EndDrag(InputEventType.DragCancel);
            }

            if (m_CurrentDragEvent.IsDropped) {
                EndDrag(InputEventType.DragDrop);
            }
        }

        private void BeginDrag() {
            m_MouseState.leftMouseButtonState.isDrag = m_MouseState.isLeftMouseDown;
            m_MouseState.rightMouseButtonState.isDrag = m_MouseState.isRightMouseDown;
            m_MouseState.middleMouseButtonState.isDrag = m_MouseState.isMiddleMouseDown;

            IsDragging = true;
            m_EventPropagator.Reset(m_MouseState);
            MouseInputEvent mouseEvent = new MouseInputEvent(m_EventPropagator, InputEventType.DragCreate, modifiersThisFrame);
            m_CurrentMouseEvent = mouseEvent;

            if (m_MouseDownElements.Count == 0) return;

            m_EventPropagator.origin = m_MouseDownElements[0];

            for (int i = 0; i < m_MouseDownElements.Count; i++) {
                UIElement element = m_MouseDownElements[i];
                if (element.isDestroyed || element.isDisabled) {
                    continue;
                }

                if (element.layoutResult.HasScrollbarVertical || element.layoutResult.HasScrollbarHorizontal) {
                    Scrollbar scrollbar = Application.GetCustomScrollbar(element.style.Scrollbar);
                    m_CurrentDragEvent = scrollbar.CreateDragEvent(element, mouseEvent);
                }

                if (m_CurrentDragEvent == null) {
                    DragCreatorGroup dragCreatorGroup;
                    if (!m_DragCreatorMap.TryGetValue(element.id, out dragCreatorGroup)) {
                        continue;
                    }

                    // todo -- figure out if these should respect propagation

                    m_CurrentDragEvent = dragCreatorGroup.TryCreateEvent(element, mouseEvent);
                    if (m_CurrentDragEvent == null) {
                        continue;
                    }
                }

                m_CurrentDragEvent.StartTime = Time.realtimeSinceStartup;
                m_CurrentDragEvent.DragStartPosition = MousePosition;

                UpdateDrag(true);
                return;
            }
        }

        private void EndDrag(InputEventType evtType) {
            IsDragging = false;

            if (m_CurrentDragEvent == null) {
                return;
            }

            m_CurrentDragEvent.MousePosition = MousePosition;
            m_CurrentDragEvent.Modifiers = modifiersThisFrame;

            bool isOriginElementThisFrame = false;
            for (int i = 0; i < m_ElementsThisFrame.Count; i++) {
                if (m_ElementsThisFrame[i].id == m_CurrentDragEvent.origin.id) {
                    isOriginElementThisFrame = true;
                    break;
                }
            }

            if (!isOriginElementThisFrame) {
                m_ElementsThisFrame.Add(m_CurrentDragEvent.origin);
            }

            if (evtType == InputEventType.DragCancel) {
                RunDragEvent(m_ElementsThisFrame, InputEventType.DragCancel);
            }
            else if (evtType == InputEventType.DragDrop) {
                RunDragEvent(m_ElementsThisFrame, InputEventType.DragDrop);
                m_CurrentDragEvent.Drop(true);
            }

            m_CurrentDragEvent.OnComplete();
            m_CurrentDragEvent = null;
        }

        private void RunDragEvent(List<UIElement> elements, InputEventType eventType) {
            if (m_CurrentDragEvent.IsCanceled && eventType != InputEventType.DragCancel) {
                return;
            }

            m_CurrentDragEvent.CurrentEventType = eventType;
            m_CurrentDragEvent.source = m_EventPropagator;

            m_EventPropagator.Reset(m_MouseState);

            for (int i = 0; i < elements.Count; i++) {
                UIElement element = elements[i];
                if (element.isDestroyed || element.isDisabled) {
                    continue;
                }

                DragHandlerGroup dragHandlerGroup;

                if (!m_DragHandlerMap.TryGetValue(element.id, out dragHandlerGroup)) {
                    continue;
                }

                if ((dragHandlerGroup.handledEvents & eventType) == 0) {
                    continue;
                }

                DragEventHandler[] handlers = dragHandlerGroup.handlers;

                for (int j = 0; j < handlers.Length; j++) {
                    DragEventHandler handler = handlers[j];
                    if (handler.eventType != eventType) {
                        continue;
                    }

                    if (handler.eventPhase != EventPhase.Bubble) {
                        m_DragEventCaptureList.Add(ValueTuple.Create(handler, element, dragHandlerGroup.context));
                        continue;
                    }

                    CurrentDragEvent.target = element;
                    handler.Invoke(element, dragHandlerGroup.context, m_CurrentDragEvent);
                    CurrentDragEvent.target = null;

                    if (m_CurrentDragEvent.IsCanceled || m_EventPropagator.shouldStopPropagation) {
                        break;
                    }
                }

                if (m_EventPropagator.shouldStopPropagation) {
                    break;
                }

                if (m_CurrentDragEvent.IsCanceled || m_EventPropagator.shouldStopPropagation) {
                    m_DragEventCaptureList.Clear();
                    return;
                }
            }

            for (int i = 0; i < m_DragEventCaptureList.Count; i++) {
                DragEventHandler handler = m_DragEventCaptureList[i].Item1;
                UIElement element = m_DragEventCaptureList[i].Item2;
                ExpressionContext context = m_DragEventCaptureList[i].Item3;

                handler.Invoke(element, context, m_CurrentDragEvent);

                if (m_EventPropagator.shouldStopPropagation) {
                    m_DragEventCaptureList.Clear();
                    return;
                }
            }

            m_DragEventCaptureList.Clear();
        }

        public void OnReset() {
            // don't clear key states
            m_FocusedElement = null;
            m_ElementsLastFrame.Clear();
            m_ElementsThisFrame.Clear();
            m_MouseDownElements.Clear();
            m_KeyboardEventTree.Clear();
            m_MouseHandlerMap.Clear();
            m_DragCreatorMap.Clear();
            m_DragHandlerMap.Clear();
            m_CurrentDragEvent = null;
            IsDragging = false;
        }

        public void OnDestroy() { }

        public void OnViewAdded(UIView view) { }

        public void OnViewRemoved(UIView view) { }

        public void OnElementEnabled(UIElement element) { }

        public void OnElementDisabled(UIElement element) {
            BlurOnDisableOrDestroy();
        }

        public void OnElementDestroyed(UIElement element) {
            BlurOnDisableOrDestroy();

            m_ElementsLastFrame.Remove(element);
            m_ElementsThisFrame.Remove(element);
            m_MouseDownElements.Remove(element);
            m_KeyboardEventTree.RemoveHierarchy(element);
            // todo -- clear child handlers
            m_MouseHandlerMap.Remove(element.id);
            m_DragCreatorMap.Remove(element.id);
            m_DragHandlerMap.Remove(element.id);
        }

        private void BlurOnDisableOrDestroy() {
            if (m_FocusedElement != null && (m_FocusedElement.isDisabled || m_FocusedElement.isDestroyed)) {
                ReleaseFocus((IFocusable) m_FocusedElement);
            }
        }

        public void OnElementCreated(UIElement element) {
            UITemplate template = element.OriginTemplate;

            if (template == null) return;

            MouseEventHandler[] mouseHandlers = template.mouseEventHandlers;
            DragEventCreator[] dragEventCreators = template.dragEventCreators;
            DragEventHandler[] dragEventHandlers = template.dragEventHandlers;
            KeyboardEventHandler[] keyboardHandlers = template.keyboardEventHandlers;

            if (mouseHandlers != null && mouseHandlers.Length > 0) {
                InputEventType handledEvents = 0;

                for (int i = 0; i < mouseHandlers.Length; i++) {
                    handledEvents |= mouseHandlers[i].eventType;
                }

                m_MouseHandlerMap[element.id] = new MouseHandlerGroup(element.templateContext, mouseHandlers, handledEvents);
            }

            if (dragEventHandlers != null && dragEventHandlers.Length > 0) {
                InputEventType handledEvents = 0;

                for (int i = 0; i < dragEventHandlers.Length; i++) {
                    handledEvents |= dragEventHandlers[i].eventType;
                }

                m_DragHandlerMap[element.id] = new DragHandlerGroup(element.templateContext, dragEventHandlers, handledEvents);
            }

            if (keyboardHandlers != null && keyboardHandlers.Length > 0) {
                m_KeyboardEventTree.AddItem(new KeyboardEventTreeNode(element, keyboardHandlers));
            }

            if (dragEventCreators != null && dragEventCreators.Length > 0) {
                m_DragCreatorMap[element.id] = new DragCreatorGroup(element.templateContext, dragEventCreators);
            }

//            if (element.children == null) {
//                return;
//            }

//            for (int i = 0; i < element.children.Count; i++) {
//                OnElementCreated(element.children[i]);
//            }
        }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string attributeValue) { }

        public bool IsKeyDown(KeyCode keyCode) {
            return (GetKeyState(keyCode) & KeyState.Down) != 0;
        }

        public bool IsKeyDownThisFrame(KeyCode keyCode) {
            return (GetKeyState(keyCode) & KeyState.DownThisFrame) != 0;
        }

        public bool IsKeyUp(KeyCode keyCode) {
            KeyState state = GetKeyState(keyCode);
            return (state == KeyState.Up || (state & KeyState.UpThisFrame) != 0);
        }

        public bool IsKeyUpThisFrame(KeyCode keyCode) {
            return (GetKeyState(keyCode) & KeyState.UpThisFrame) != 0;
        }

        public KeyState GetKeyState(KeyCode keyCode) {
            KeyState state;
            if (m_KeyStates.TryGetValue(keyCode, out state)) {
                return state;
            }

            return KeyState.Up;
        }

        protected void ProcessKeyboardEvent(KeyCode keyCode, InputEventType eventType, char character, KeyboardModifiers modifiers) {
            KeyboardInputEvent keyEvent = new KeyboardInputEvent(eventType, keyCode, character, modifiers, m_FocusedElement != null);
            m_CurrentKeyboardEvent = keyEvent;

            if (eventType == InputEventType.KeyDown) {
                if (UnityEngine.Input.GetKeyDown(keyCode)) {
                    m_PressedKeys.Add(new PressedKey(keyCode, character));
                }
            }
            else if (eventType == InputEventType.KeyUp) {
                m_PressedKeys.Remove(character, (pressed, c) => pressed.character == c);
                m_PressedKeys.Remove(keyCode, (pressed, c) => pressed.keyCode == c);
            }

            if (m_FocusedElement == null) {
                m_KeyboardEventTree.ConditionalTraversePreOrder(keyEvent, (item, evt) => {
                    if (evt.stopPropagation) return false;

                    UIElement element = (UIElement) item.Element;
                    if (element.isDestroyed || element.isDisabled) {
                        return false;
                    }

                    IReadOnlyList<KeyboardEventHandler> handlers = item.handlers;
                    for (int i = 0; i < handlers.Count; i++) {
                        if (evt.stopPropagationImmediately) break;
                        InvokeHandler(handlers[i], item.Element, default, evt);
                    }

                    if (evt.stopPropagationImmediately) {
                        evt.StopPropagation();
                    }

                    return !evt.stopPropagation;
                });
            }
            else {
                KeyboardEventTreeNode focusedNode = m_KeyboardEventTree.GetItem(m_FocusedElement);
                if (focusedNode == null) {
                    // actually this is totally fine since any element can implement IFocusable without accepting keyboard input
                    return;
                } 
                IReadOnlyList<KeyboardEventHandler> handlers = focusedNode.handlers;
                ExpressionContext context = ((UIElement) focusedNode.Element).templateContext;
                for (int i = 0; i < handlers.Count; i++) {
                    if (keyEvent.stopPropagationImmediately) break;
                    InvokeHandler(handlers[i], focusedNode.Element, context, keyEvent);
                }
            }
        }

        private void InvokeHandler(KeyboardEventHandler handler, IHierarchical target, ExpressionContext context, KeyboardInputEvent keyEvent) {
            if (handler.keyEventPhase == KeyEventPhase.Late) {
                lateHandlers.Add(new KeyboardEventHandlerInvocation {
                    handler = handler,
                    target = target,
                    context = context,
                    evt = keyEvent
                });
            }
            else {
                handler.Invoke(target, context, keyEvent);
            }
        }

        private void ProcessKeyboardEvents() {
            for (int i = 0; i < m_UpThisFrame.Count; i++) {
                m_KeyStates[m_UpThisFrame[i]] = KeyState.UpNotThisFrame;
            }

            for (int i = 0; i < m_DownThisFrame.Count; i++) {
                m_KeyStates[m_DownThisFrame[i]] = KeyState.DownNotThisFrame;
            }

            m_DownThisFrame.Clear();
            m_UpThisFrame.Clear();

            HandleShiftKey(KeyCode.LeftShift);
            HandleShiftKey(KeyCode.RightShift);

            if (IsKeyDown(KeyCode.LeftShift) || IsKeyDown(KeyCode.RightShift)) {
                modifiersThisFrame |= KeyboardModifiers.Shift;
            }
            else {
                modifiersThisFrame &= ~KeyboardModifiers.Shift;
            }

            while (Event.PopEvent(s_Event)) {
                KeyCode keyCode = s_Event.keyCode;
                char character = s_Event.character;

                // need to check this on osx, according to stackoverflow OSX and Windows might handle
                // sending key events differently

                if (s_Event.rawType == EventType.ExecuteCommand || s_Event.rawType == EventType.ValidateCommand) {
                    switch (s_Event.commandName) {
                        case "SelectAll":
                            ProcessKeyEvent(EventType.KeyDown, KeyCode.A, 'a');
                            continue;
                        case "Copy":
                            ProcessKeyEvent(EventType.KeyDown, KeyCode.C, 'c');
                            continue;
                        case "Cut":
                            ProcessKeyEvent(EventType.KeyDown, KeyCode.X, 'x');
                            continue;
                        case "Paste":
                            ProcessKeyEvent(EventType.KeyDown, KeyCode.V, 'v');
                            continue;
                        case "SoftDelete":
                            Debug.Log("Delete");
                            continue;
                        case "Duplicate":
                            Debug.Log("Duplicate");
                            continue;
                        case "Find":
                            continue;
//                            "Copy", "Cut", "Paste", "Delete", "SoftDelete", "Duplicate", "FrameSelected", "FrameSelectedWithLock", "SelectAll", "Find"
                    }
                }

                if (keyCode == KeyCode.None && character != '\0') {
                    if (s_Event.rawType == EventType.KeyDown) {
                        ProcessKeyboardEvent(keyCode, InputEventType.KeyDown, character, modifiersThisFrame);
                        continue;
                    }

                    if (s_Event.rawType == EventType.KeyUp) {
                        ProcessKeyboardEvent(keyCode, InputEventType.KeyUp, character, modifiersThisFrame);
                        continue;
                    }
                }

                ProcessKeyEvent(s_Event.rawType, s_Event.keyCode, s_Event.character);
            }

            for (int i = 0; i < m_PressedKeys.Count; i++) {
                if (!m_DownThisFrame.Contains(m_PressedKeys[i].keyCode, (k, p) => k != p)) {
                    if (UnityEngine.Input.GetKey(m_PressedKeys[i].keyCode)) {
                        ProcessKeyboardEvent(m_PressedKeys[i].keyCode, InputEventType.KeyHeldDown, m_PressedKeys[i].character, modifiersThisFrame);
                    }
                    else {
                        m_PressedKeys.Remove(m_PressedKeys[i].keyCode, ((pressed, code) => pressed.keyCode == code));
                    }
                }
            }
        }

        private void ProcessKeyEvent(EventType evtType, KeyCode keyCode, char character) {
            switch (evtType) {
                case EventType.KeyDown:
                    if (m_KeyStates.ContainsKey(keyCode)) {
                        KeyState state = m_KeyStates[keyCode];
                        if ((state & KeyState.Down) == 0) {
                            m_DownThisFrame.Add(keyCode);
                            m_KeyStates[keyCode] = KeyState.DownThisFrame;
                            ProcessKeyboardEvent(keyCode, InputEventType.KeyDown, character, modifiersThisFrame);
                        }
                    }
                    else {
                        m_DownThisFrame.Add(keyCode);
                        m_KeyStates[keyCode] = KeyState.DownThisFrame;
                        ProcessKeyboardEvent(keyCode, InputEventType.KeyDown, character, modifiersThisFrame);
                    }

                    HandleModifierDown(keyCode);
                    break;

                case EventType.KeyUp:
                    m_UpThisFrame.Add(keyCode);

                    m_PressedKeys.Remove(character, (pressed, c) => pressed.character == c);

                    m_KeyStates[keyCode] = KeyState.UpThisFrame;
                    ProcessKeyboardEvent(keyCode, InputEventType.KeyUp, character, modifiersThisFrame);
                    HandleModifierUp(keyCode);
                    break;
            }
        }

        private void HandleShiftKey(KeyCode code) {
            bool wasDown = IsKeyDown(code);
            bool isDown = UnityEngine.Input.GetKey(code);
            if ((wasDown && !isDown) || UnityEngine.Input.GetKeyUp(code)) {
                m_KeyStates[code] = KeyState.UpThisFrame;
                m_UpThisFrame.Add(code);
                ProcessKeyboardEvent(code, InputEventType.KeyUp, '\0', modifiersThisFrame);
            }
            else if (UnityEngine.Input.GetKeyDown(code)) {
                m_KeyStates[code] = KeyState.DownThisFrame;
                m_DownThisFrame.Add(code);
                ProcessKeyboardEvent(code, InputEventType.KeyDown, '\0', modifiersThisFrame);
            }
            else if (isDown) {
                m_KeyStates[code] = KeyState.Down;
            }
            else {
                m_KeyStates[code] = KeyState.Up;
            }
        }

        private void HandleModifierDown(KeyCode keyCode) {
            switch (keyCode) {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    modifiersThisFrame |= KeyboardModifiers.Alt;
                    break;
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    modifiersThisFrame |= KeyboardModifiers.Control;
                    break;
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    modifiersThisFrame |= KeyboardModifiers.Command;
                    break;
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                    modifiersThisFrame |= KeyboardModifiers.Windows;
                    break;
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    modifiersThisFrame |= KeyboardModifiers.Shift;
                    break;
                case KeyCode.Numlock:
                    modifiersThisFrame |= KeyboardModifiers.NumLock;
                    break;
                case KeyCode.CapsLock:
                    modifiersThisFrame |= KeyboardModifiers.CapsLock;
                    break;
            }
        }

        private void HandleModifierUp(KeyCode keyCode) {
            switch (keyCode) {
                case KeyCode.LeftAlt:
                    if (!UnityEngine.Input.GetKey(KeyCode.RightAlt)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Alt;
                    }

                    break;
                case KeyCode.RightAlt:
                    if (!UnityEngine.Input.GetKey(KeyCode.LeftAlt)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Alt;
                    }

                    break;
                case KeyCode.LeftControl:
                    if (!UnityEngine.Input.GetKey(KeyCode.RightControl)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Control;
                    }

                    break;
                case KeyCode.RightControl:
                    if (!UnityEngine.Input.GetKey(KeyCode.LeftControl)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Control;
                    }

                    break;
                case KeyCode.LeftCommand:
                    if (!UnityEngine.Input.GetKey(KeyCode.RightCommand)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Command;
                    }

                    break;
                case KeyCode.RightCommand:
                    if (!UnityEngine.Input.GetKey(KeyCode.LeftCommand)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Command;
                    }

                    break;
                case KeyCode.LeftWindows:
                    if (!UnityEngine.Input.GetKey(KeyCode.RightWindows)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Windows;
                    }

                    break;
                case KeyCode.RightWindows:
                    if (!UnityEngine.Input.GetKey(KeyCode.LeftWindows)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Windows;
                    }

                    break;
                case KeyCode.LeftShift:
                    if (!UnityEngine.Input.GetKey(KeyCode.RightShift)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Shift;
                    }

                    break;
                case KeyCode.RightShift:
                    if (!UnityEngine.Input.GetKey(KeyCode.LeftShift)) {
                        modifiersThisFrame &= ~KeyboardModifiers.Shift;
                    }

                    break;
                case KeyCode.Numlock:
                    modifiersThisFrame &= ~KeyboardModifiers.NumLock;
                    break;
                case KeyCode.CapsLock:
                    modifiersThisFrame &= ~KeyboardModifiers.CapsLock;
                    break;
            }
        }

        private void RunMouseEvents(List<UIElement> elements, InputEventType eventType) {
            if (elements.Count == 0) return;

            m_EventPropagator.Reset(m_MouseState);
            m_EventPropagator.origin = elements[0];
            MouseInputEvent mouseEvent = new MouseInputEvent(m_EventPropagator, eventType, modifiersThisFrame);
            m_CurrentMouseEvent = mouseEvent;

            for (int i = 0; i < elements.Count; i++) {
                UIElement element = elements[i];
                if (element.isDestroyed || element.isDisabled) {
                    continue;
                }

                MouseHandlerGroup mouseHandlerGroup;

                if (!m_MouseHandlerMap.TryGetValue(element.id, out mouseHandlerGroup)) {
                    continue;
                }

                if ((mouseHandlerGroup.handledEvents & eventType) == 0) {
                    continue;
                }

                MouseEventHandler[] handlers = mouseHandlerGroup.handlers;

                for (int j = 0; j < handlers.Length; j++) {
                    MouseEventHandler handler = handlers[j];
                    if (handler.eventType != eventType) {
                        continue;
                    }

                    if (handler.eventPhase != EventPhase.Bubble) {
                        m_MouseEventCaptureList.Add(ValueTuple.Create(handler, element, mouseHandlerGroup.context));
                        continue;
                    }

                    handler.Invoke(element, mouseHandlerGroup.context, mouseEvent);

                    if (m_EventPropagator.shouldStopPropagation) {
                        break;
                    }
                }

                if (m_EventPropagator.shouldStopPropagation) {
                    m_MouseEventCaptureList.Clear();
                    return;
                }
            }

            for (int i = 0; i < m_MouseEventCaptureList.Count; i++) {
                MouseEventHandler handler = m_MouseEventCaptureList[i].Item1;
                UIElement element = m_MouseEventCaptureList[i].Item2;
                ExpressionContext context = m_MouseEventCaptureList[i].Item3;

                handler.Invoke(element, context, mouseEvent);

                if (m_EventPropagator.shouldStopPropagation) {
                    m_MouseEventCaptureList.Clear();
                    return;
                }
            }

            m_MouseEventCaptureList.Clear();
        }

        private void ProcessMouseEvents() {
            RunMouseEvents(m_ExitedElements, InputEventType.MouseExit);
            RunMouseEvents(m_EnteredElements, InputEventType.MouseEnter);

            if (m_MouseState.scrollDelta != Vector2.zero) {
                RunMouseEvents(m_ElementsThisFrame, InputEventType.MouseScroll);
            }

            if (m_MouseState.isLeftMouseDownThisFrame || m_MouseState.isRightMouseDownThisFrame || m_MouseState.isMiddleMouseDownThisFrame) {
                
                HandleBlur();

                if (m_ElementsThisFrame.Count > 0 && m_ElementsThisFrame[0].View.RequestFocus()) {
                    // todo let's see if we have to process the mouse event again
                }
                RunMouseEvents(m_ElementsThisFrame, InputEventType.MouseDown);
            }
            else if (m_MouseState.isLeftMouseUpThisFrame || m_MouseState.isRightMouseUpThisFrame || m_MouseState.isMiddleMouseUpThisFrame) {
                RunMouseEvents(m_ElementsThisFrame, InputEventType.MouseUp);
                if (m_MouseState.clickCount > 0) {
                    RunMouseEvents(m_ElementsThisFrame, InputEventType.MouseClick);
                } else if (!m_MouseState.isLeftMouseDown && !m_MouseState.isMiddleMouseDown) {
                    RunMouseEvents(m_ElementsThisFrame, InputEventType.MouseContext);
                }
            }

            RunMouseEvents(m_ElementsThisFrame, m_MouseState.DidMove ? InputEventType.MouseMove : InputEventType.MouseHover);
        }

        private void HandleBlur() {
            if (m_FocusedElement == null) {
                return;
            }

            if (m_ElementsThisFrame.Count == 0) {
                ReleaseFocus((IFocusable)m_FocusedElement);
                return;
            }

            UIElement ptr = m_ElementsThisFrame[0];
            while (ptr != null) {
                if (ptr == m_FocusedElement) {
                    return;
                }
                ptr = ptr.parent;
            }
            
            ReleaseFocus((IFocusable)m_FocusedElement);
        }

    }

    public struct KeyboardEventHandlerInvocation {

        public KeyboardEventHandler handler { get; set; }
        public IHierarchical target { get; set; }
        public ExpressionContext context { get; set; }
        public KeyboardInputEvent evt { get; set; }

    }

}
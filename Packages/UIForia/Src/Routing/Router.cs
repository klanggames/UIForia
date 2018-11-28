using UIForia.Util;

namespace UIForia.Routing {

    public struct Route {

        public string path;

        public Route(string path) {
            this.path = path;
        }

    }

    public interface IRouteHandler {

        void OnRouteChanged(Route route);

    }

    public interface IRouteGuard {

        bool CanTransition(Route current, Route next);

    }

    public class Router {

        private Route current;
        private int historyIndex;

        private readonly LightList<Route> m_HistoryStack;
        private readonly LightList<IRouteGuard> m_Guards;
        private readonly LightList<IRouteHandler> m_Handlers;

        public Router() {
            current = new Route("");
            m_HistoryStack = new LightList<Route>();
            m_Guards = new LightList<IRouteGuard>();
            m_Handlers = new LightList<IRouteHandler>();
            m_HistoryStack.Add(current);
        }

        public string CurrentUrl => current.path;
        public bool CanGoBack => historyIndex > 1;
        public bool CanGoForwards => m_HistoryStack.Count > 1 && historyIndex != m_HistoryStack.Count - 1;

        public void AddRouteHandler(IRouteHandler handler) {
            m_Handlers.Add(handler);    
        }
        
        public void AddRouteGuard(IRouteGuard guard) {
            m_Guards.Add(guard);
        }

        public void GoForwards() {
            
        }

        public void GoBack() {
            if (historyIndex == 0) {
                return;
            }

            historyIndex--;
            Route route = m_HistoryStack[historyIndex];
            for (int i = 0; i < m_Handlers.Length; i++) {
                m_Handlers[i].OnRouteChanged(route);
            }

            current = route;
        }

        public bool GoTo(string path) {
            return GoTo(new Route(path));
        }      
        
        public bool GoTo(Route route) {
            if (IsTransitionBlocked(route)) {
                return false;
            }

            for (int i = 0; i < m_Handlers.Length; i++) {
                m_Handlers[i].OnRouteChanged(route);
            }

            current = route;
            historyIndex++;
            m_HistoryStack.Add(route);

            return false;
        }

        private bool IsTransitionBlocked(Route route) {
            for (int i = 0; i < m_Guards.Length; i++) {
                if (!m_Guards[i].CanTransition(current, route)) {
                    return true;
                }
            }

            return false;
        }

    }

}
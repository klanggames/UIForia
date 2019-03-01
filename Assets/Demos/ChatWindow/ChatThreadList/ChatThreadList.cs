using System.Collections.Generic;
using UIForia.Attributes;
using UIForia.Elements;

namespace Demo {

    [Template("Demos/ChatWindow/ChatThreadList/ChatThreadList.xml")]
    public class ChatThreadList : UIElement {

        public List<ChatThread> chatThreads;

    }

}
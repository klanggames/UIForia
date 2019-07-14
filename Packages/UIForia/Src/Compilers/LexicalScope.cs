using UIForia.Elements;

namespace UIForia.Compilers {

    public readonly struct LexicalScope {

        public readonly UIElement root;
        public readonly CompiledTemplate data;

        public LexicalScope(UIElement root, CompiledTemplate data) {
            this.root = root;
            this.data = data;
        }

    }

}
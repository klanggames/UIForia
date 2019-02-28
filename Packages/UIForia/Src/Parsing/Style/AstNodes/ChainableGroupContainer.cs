namespace UIForia.Parsing.Style.AstNodes {

    internal static partial class StyleASTNodeFactory {
    }

    public abstract class ChainableGroupContainer : StyleGroupContainer {
        public string value;
        public bool invert;
        public ChainableGroupContainer next;

        public string GetGroupName() {
            if (next != null) return $"{(invert ? "!" : "")}{identifier}+{next.GetGroupName()}";
            return identifier;
        }
    }
}
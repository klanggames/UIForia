
namespace UIForia.Compilers {

    public readonly struct TemplateContextVariable {

        public readonly string name;
        public readonly string expression;

        public TemplateContextVariable(string name, string expression) {
            this.name = name;
            this.expression = expression;
        }

    }

}
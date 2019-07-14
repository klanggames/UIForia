using UIForia.Systems;

namespace UIForia.Compilers {

    public readonly struct TemplateContextWrapper {

        public readonly int id;
        public readonly TemplateContext context;

        public TemplateContextWrapper(TemplateContext templateContext, int id) {
            this.context = templateContext;
            this.id = id;
        }

    }

}
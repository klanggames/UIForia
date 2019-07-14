namespace UIForia.Compilers {

    public readonly struct SlotUsage {

        public readonly string slotName;
        public readonly int templateId;
        public readonly LexicalScope lexicalScope;

        public SlotUsage(string slotName, int templateId, LexicalScope lexicalScope) {
            this.slotName = slotName;
            this.templateId = templateId;
            this.lexicalScope = lexicalScope;
        }

    }

}
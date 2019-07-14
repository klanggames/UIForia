using System;
using System.Linq.Expressions;
using UIForia.Util;

namespace UIForia.Compilers {

    public class TemplateContextDefinition {

        public string name;
        public int expressionId;
        public Type type;
        public int id;

        public StructList<TemplateContextVariable> variables;
        public static int IdGenerator = 0;

    }

}
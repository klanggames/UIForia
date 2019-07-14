using System;
using System.Diagnostics;

namespace UIForia.Parsing.Expression {

    // order is important because this is a sort key
    [Flags]
    public enum AttributeType : ushort {

        Context = 1 << 0,
        ContextVariable = 1 << 1,
        Alias = 1 << 2,
        Property = 1 << 3,
        Style = 1 << 4,
        Attribute = 1 << 5,
        Event = 1 << 6,

    }

    [Flags]
    public enum AttributeFlags : ushort {

        Binding = 1 << 0,
        Const = 1 << 1,
        EnableOnly = 1 << 2,
        RootContext = 1 << 3

    }
    
    [DebuggerDisplay("{key}={value}")]
    public class AttributeDefinition {

        internal bool isCompiled;
        public readonly string key;
        public readonly string value;
        public readonly bool isRealAttribute;
        public int line;
        public int column;

        public AttributeDefinition(string key, string value, int line = -1, int column = -1) {
            this.key = key.Trim();
            this.value = value.Trim();
            this.isRealAttribute = key.StartsWith("x-");
            this.line = line;
            this.column = column;
        }

    }

    [DebuggerDisplay("type={type} {key}={value}")]
    public struct AttributeDefinition2 {

        public readonly string key;
        public readonly string value;
        public int line;
        public int column;
        public AttributeType type;
        public AttributeFlags flags;
        
        public AttributeDefinition2(AttributeType type, AttributeFlags flags,  string key, string value, int line = -1, int column = -1) {
            this.type = type;
            this.flags = flags;
            this.key = key;
            this.value = value;
            this.line = line;
            this.column = column;
        }

    }

}
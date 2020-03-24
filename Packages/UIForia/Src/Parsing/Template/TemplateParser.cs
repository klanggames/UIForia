using System;
using System.Collections.Generic;

namespace UIForia.Parsing {

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TemplateParserAttribute : Attribute {

        public readonly string extension;

        public TemplateParserAttribute(string extension) {
            if (extension[0] == '.') {
                extension = extension.Substring(1);
            }
            this.extension = extension;
        }

    }
    
    internal struct ErrorContext {

        public int lineNumber;
        public int colNumber;

    }
    
    public abstract class TemplateParser {

        internal List<Diagnostic> diagnostics;

        public string FilePath { get; private set; }
        
        private ErrorContext errorContext;

        [ThreadStatic] private static TemplateParser[] s_TemplateParsers;

        public string extension { get; internal set; }

        protected TemplateParser() {
            diagnostics = new List<Diagnostic>();
        }
        
        internal void Setup(string filePath) {
            this.FilePath = filePath;
            OnSetup();
        }

        internal void Reset() {
            OnReset();
        }
        
        protected bool ReportParseError(string message) {
            // module.ReportParseError(filePath, message, errorContext.lineNumber, errorContext.colNumber);
            return false;
        }
        
        protected void SetErrorContext(int line, int column) {
            errorContext.lineNumber = line;
            errorContext.colNumber = column;
        }
        
        public abstract bool TryParse(string contents, TemplateShell templateShell);

        public virtual void OnSetup() { }

        public virtual void OnReset() { }

        public void AddDiagnostic() { }

        internal static TemplateParser GetParserForFileType(string extension) {
            s_TemplateParsers = s_TemplateParsers ?? TypeProcessor.CreateTemplateParsers();
            for (int i = 0; i < s_TemplateParsers.Length; i++) {
                if (s_TemplateParsers[i].extension == extension) {
                    return s_TemplateParsers[i];
                }
            }

            return null;
        }

    }

}
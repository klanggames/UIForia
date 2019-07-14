using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Linq.Expressions;
using NUnit.Framework;
using UIForia.Elements;
using UnityEngine;

namespace Tests.Compilers.TemplateCompiler {

    public class TestTemplateUtils {

        public static void AssertElementHierarchy(ElementAssertion assertion, UIElement element, UIElement parent = null) {
            Assert.AreEqual(assertion.type, element.GetType());
            AssertAttributesEqual(assertion.attributes, element.attributes?.ToArray());
            if (element is UITextElement textElement) {
                Assert.AreEqual(assertion.textContent, textElement.text);
            }

            if (assertion.children != null && element.children.size != assertion.children.Length) {
                Assert.IsTrue(false);
            }

            if (assertion.children == null && element.children.size != 0) {
                Assert.IsTrue(false);
            }

            Assert.AreEqual(parent, element.parent);
            if (assertion.children != null && element.children != null) {
                Assert.AreEqual(assertion.children.Length, element.children.size);
                for (int i = 0; i < assertion.children.Length; i++) {
                    AssertElementHierarchy(assertion.children[i], element.children[i], element);
                }
            }
        }

        public static void AssertAttributesEqual(ElementAttribute[] asserts, IList<ElementAttribute> elementAttributes) {
            if (asserts == null && elementAttributes == null) {
                return;
            }

            if (asserts != null && elementAttributes == null) {
                Assert.IsTrue(false);
            }

            if (asserts == null && elementAttributes != null) {
                Assert.IsTrue(false);
            }

            Assert.AreEqual(asserts.Length, elementAttributes.Count);
            for (int i = 0; i < asserts.Length; i++) {
                Assert.AreEqual(asserts[i].name, elementAttributes[i].name);
                Assert.AreEqual(asserts[i].value, elementAttributes[i].value);
            }
        }

        public class ElementAssertion {

            public Type type;
            public ElementAttribute[] attributes;
            public ElementAssertion[] children;
            public string textContent;

            public ElementAssertion(Type type) {
                this.type = type;
            }

        }
        
        public static string PrintCode(IList<Expression> expressions, bool printNamespaces = true) {
            string retn = "";
            bool old = CSharpWriter.printNamespaces;
            CSharpWriter.printNamespaces = printNamespaces;
            for (int i = 0; i < expressions.Count; i++) {
                retn += expressions[i].ToCSharpCode();
                if (i != expressions.Count - 1) {
                    retn += "\n";
                }
            }

            CSharpWriter.printNamespaces = old;
            return retn;
        }

        public static string PrintCode(Expression expression, bool printNamespaces = true) {
            bool old = CSharpWriter.printNamespaces;
            CSharpWriter.printNamespaces = printNamespaces;
            string retn = expression.ToCSharpCode();
            CSharpWriter.printNamespaces = old;
            return retn;
        }

        public static void LogCode(Expression expression, bool printNamespaces = true) {
            bool old = CSharpWriter.printNamespaces;
            CSharpWriter.printNamespaces = printNamespaces;
            string retn = expression.ToCSharpCode();
            CSharpWriter.printNamespaces = old;
            Debug.Log(retn);
        }

    }

}
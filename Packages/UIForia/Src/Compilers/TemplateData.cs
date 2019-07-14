using System;
using System.Linq.Expressions;
using UIForia.Elements;
using UIForia.Systems;
using UIForia.Util;

namespace UIForia.Compilers {

    public class TemplateData {

        public int AddContextProviderLambda(LambdaExpression expression) {
            contextProviderLambdas.Add(expression);
            return contextProviderLambdas.Count - 1;
        }

        public int AddSharedBindingLambda(LambdaExpression expression) {
            sharedBindingLambdas.Add(expression);
            return sharedBindingLambdas.Count - 1;
        }

        internal LightList<LambdaExpression> contextProviderLambdas = new LightList<LambdaExpression>();
        internal LightList<LambdaExpression> sharedBindingLambdas = new LightList<LambdaExpression>();
        internal LightList<LambdaExpression> instanceBindingFns = new LightList<LambdaExpression>();

        internal Func<UIElement, UIElement, TemplateContext>[] contextProviderFns;
        internal Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>[] sharedBindingFns;

        // load dll and copy array or call compile on all the fns.
        public void Build() {
            contextProviderFns = new Func<UIElement, UIElement, TemplateContext>[contextProviderLambdas.Count];
            for (int i = 0; i < contextProviderLambdas.Count; i++) {
                contextProviderFns[i] = (Func<UIElement, UIElement, TemplateContext>) contextProviderLambdas[i].Compile();
            }

            sharedBindingFns = new Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>[sharedBindingLambdas.Count];
            for (int i = 0; i < contextProviderLambdas.Count; i++) {
                sharedBindingFns[i] = (Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>) sharedBindingLambdas[i].Compile();
            }
        }

    }

}
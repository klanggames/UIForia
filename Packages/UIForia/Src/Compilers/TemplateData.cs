using System;
using System.Linq.Expressions;
using Mono.Linq.Expressions;
using UIForia.Elements;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;

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
        
        public int AddTemplate(LambdaExpression expression) {
            templateLambdas.Add(expression);
            return templateLambdas.size - 1;
        }
        
        internal LightList<LambdaExpression> contextProviderLambdas = new LightList<LambdaExpression>();
        internal LightList<LambdaExpression> sharedBindingLambdas = new LightList<LambdaExpression>();
        internal LightList<LambdaExpression> instanceBindingFns = new LightList<LambdaExpression>();
        internal LightList<LambdaExpression> templateLambdas = new LightList<LambdaExpression>();

        internal Func<UIElement, UIElement, TemplateContext>[] contextProviderFns;
        internal LightList<Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>> sharedBindingFns;
        internal LightList<Func<UIElement, TemplateScope2, UIElement>> templateFns;

        public static readonly Action<UIElement, UIElement, StructStack<TemplateContextWrapper>> onUpdate = (a, b, c) => b.OnUpdate();

        // load dll and copy array or call compile on all the fns.
        public void Build() {
            
            if (templateFns != null) return;
            
            contextProviderFns = new Func<UIElement, UIElement, TemplateContext>[contextProviderLambdas.Count];

            for (int i = 0; i < contextProviderLambdas.Count; i++) {
                contextProviderFns[i] = (Func<UIElement, UIElement, TemplateContext>) contextProviderLambdas[i].Compile();
            }

            sharedBindingFns = new Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>[sharedBindingLambdas.Count];

            for (int i = 0; i < sharedBindingLambdas.Count; i++) {
                if (sharedBindingLambdas[i] == null) {
                    // todo -- this is shitty but it will work, better but more wasteful would be store a tuple of type & lambda
                    sharedBindingFns[i] = onUpdate;
                }
                else {
                    Debug.Log(sharedBindingLambdas[i].ToCSharpCode());
                    sharedBindingFns[i] = (Action<UIElement, UIElement, StructStack<TemplateContextWrapper>>) sharedBindingLambdas[i].Compile();
                }
            }

            templateFns = new LightList<Func<UIElement, TemplateScope2, UIElement>>(templateLambdas.size);
            
            for (int i = 0; i < templateLambdas.size; i++) {
                templateFns[i] = (Func<UIElement, TemplateScope2, UIElement>) templateLambdas[i].Compile();
            }
            
        }

      
    }

}

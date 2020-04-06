using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Src.Systems;
using UIForia;
using UIForia.Animation;
using UIForia.Elements;
using UIForia.Routing;
using UIForia.Systems;
using UnityEngine;
using Application = UIForia.Application;

namespace Tests.Mocks {

    public class MockApplication : Application {

        public static bool s_GenerateCode;
        public static bool s_UsePreCompiledTemplates;

        protected MockApplication(UISettings settings) : base(settings) { }

        protected override void CreateSystems() {
            styleSystem = new StyleSystem();
            layoutSystem = new MockLayoutSystem(this);
            inputSystem = new MockInputSystem(layoutSystem);
            renderSystem = new MockRenderSystem(Camera, this);
            routingSystem = new RoutingSystem();
            animationSystem = new AnimationSystem();
            linqBindingSystem = new LinqBindingSystem();
        }

        public static void Generate(bool shouldGenerate = true) {
            s_GenerateCode = shouldGenerate;
            s_UsePreCompiledTemplates = shouldGenerate;
        }
        
        public static TemplateSettings GetDefaultSettings(string appName) {
            TemplateSettings settings = new TemplateSettings();
            settings.applicationName = appName;
            settings.templateRoot = "Data";
            settings.assemblyName = typeof(MockApplication).Assembly.GetName().Name;
            settings.outputPath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Tests", "UIForiaGenerated");
            settings.codeFileExtension = "generated.xml.cs";
            settings.preCompiledTemplatePath = "Assets/UIForia_Generated/" + appName;
            settings.templateResolutionBasePath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Tests");
            return settings;
        }

        public static MockApplication Setup<T>(string appName = null, List<Type> dynamicTemplateTypes = null) where T : UIElement {
            if (appName == null) {
                StackTrace stackTrace = new StackTrace();
                appName = stackTrace.GetFrame(1).GetMethod().Name;
            }

            TemplateSettings settings = new TemplateSettings();
            settings.applicationName = appName;
            settings.templateRoot = "Data";
            settings.assemblyName = typeof(MockApplication).Assembly.GetName().Name;
            settings.outputPath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Tests", "UIForiaGenerated");
            settings.codeFileExtension = ".generated.xml.cs";
            settings.preCompiledTemplatePath = "Assets/UIForia_Generated/" + appName;
            settings.templateResolutionBasePath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Tests");
            settings.rootType = typeof(T);
            settings.dynamicallyCreatedTypes = dynamicTemplateTypes;
            
            if (s_GenerateCode) {
                TemplateCodeGenerator.Generate(typeof(T), settings);
            }

            MockApplication app = new MockApplication(new UISettings {
                templateSettings = settings, 
                isPreCompiled = s_UsePreCompiledTemplates
            });
            app.Initialize();
            return app;
        }

         public static MockApplication Setup(TemplateSettings settings,  bool usePreCompiledTemplates = false) {
            MockApplication app = new MockApplication(new UISettings {
                templateSettings = settings, 
                isPreCompiled = usePreCompiledTemplates
            });
            app.Initialize();
            return app;
        }
         
        public new MockInputSystem InputSystem => (MockInputSystem) inputSystem;
        public UIElement RootElement => windowManager.windows[0];

        public void SetViewportRect(Rect rect) {
            windowManager.windows[0].Viewport = rect;
        }

    }

    public class MockRenderSystem : VertigoRenderSystem {

        public override void OnUpdate() {
            // do nothing
        }

        public MockRenderSystem(Camera camera, Application application) : base(camera, application) { }

    }

}
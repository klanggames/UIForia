using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Tests.Mocks;
using UIForia.Attributes;
using UIForia.Elements;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LifeCycle {

    [TestFixture]
    public class LifeCycleTests {

        [Template(TemplateType.String, @"
        <UITemplate>
            <Style>
                style child1 {
                    MinWidth = 300px;
                }
            </Style>
            <Contents style.layoutType='LayoutType.Flex'>
                <Group x-id='child0' style='child1' style.preferredWidth='100f' style.preferredHeight='100f'/>
                <Group x-id='child1' style.preferredWidth='100f' style.preferredHeight='100f'/>
                <Group x-id='child2' style.preferredWidth='100f' style.preferredHeight='100f'/>
            </Contents>
        </UITemplate>
    ")]
        public class LifeCycleTestThing : UIElement {

            public UIGroupElement child0;
            public UIGroupElement child1;
            public UIGroupElement child2;
            public List<int> list;

            public override void OnCreate() {
                child0 = FindById<UIGroupElement>("child0");
                child1 = FindById<UIGroupElement>("child1");
                child2 = FindById<UIGroupElement>("child2");
            }

        }

        [Test]
        public void ViewInvokesAddedEvents() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));
            int count = 0;
            view.onElementCreated += (elements) => { count++; };
            UIElement root = view.CreateElement<LifeCycleTestThing>();
            Assert.AreEqual(4, count);
        }

        [DebuggerDisplay("{name}")]
        public class LifeCycleElement : UIElement {

            public string name;
            public static List<string> output;

            public Action<UIElement> onCreate;
            public Action<UIElement> onReady;
            public Action<UIElement> onEnable;
            public Action<UIElement> onDisable;
            public Action<UIElement> onUpdate;
            public Action<UIElement> onDestroy;

            public LifeCycleElement(string name, params LifeCycleElement[] children) {
                this.name = name;
                if (children != null) {
                    for (int i = 0; i < children.Length; i++) {
                        AddChild(children[i]);
                    }
                }
            }

            public LifeCycleElement(bool enabled, string name, params LifeCycleElement[] children) {
                this.name = name;
                if (!enabled) flags &= ~UIElementFlags.Enabled;
                if (children != null) {
                    for (int i = 0; i < children.Length; i++) {
                        AddChild(children[i]);
                    }
                }
            }


            public override void OnCreate() {
                output.Add(name + ".create");
                onCreate?.Invoke(this);
            }

//            public override void OnReady() {
//                output.Add(name + ".ready");
//                onReady?.Invoke(this);
//            }

            public override void OnEnable() {
                output.Add(name + ".enable");
                onEnable?.Invoke(this);
            }

            public override void OnDisable() {
                output.Add(name + ".disable");
                onDisable?.Invoke(this);
            }

            public override void OnDestroy() {
                output.Add(name + ".destroy");
                onDestroy?.Invoke(this);
            }

            public override void OnUpdate() {
                output.Add(name + ".update");
                onUpdate?.Invoke(this);
            }

        }

        [Test]
        public void CallsLifeCycleInProperOrderOnCreate() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;
            UIElement root = view.AddChild(new LifeCycleElement("root"));

            Assert.AreEqual(new[] {
                "root.create",
                "root.enable",
                "root.ready"
            }, list.ToArray());
        }

        [Test]
        public void CallsLifeCycleInProperOrderOnCreateForChildren() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            view.AddChild(new LifeCycleElement("root",
                new LifeCycleElement("child0"),
                new LifeCycleElement("child1")
            ));


            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child1.create",
                "root.enable",
                "child0.enable",
                "child1.enable",
                "root.ready",
                "child0.ready",
                "child1.ready"
            }, list.ToArray());
        }

        [Test]
        public void CallsLifeCycleInProperOrderOnCreateForChildrenDisabled() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            view.AddChild(new LifeCycleElement("root",
                new LifeCycleElement(false, "child0"),
                new LifeCycleElement("child1")
            ));


            Assert.AreEqual(new[] {
                "root.create",
                "child1.create",
                "root.enable",
                "child1.enable",
                "root.ready",
                "child1.ready"
            }, list.ToArray());
        }

        [Test]
        public void LifeCycleDisableOnCreate() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0"),
                new LifeCycleElement("child1")
            );

            root.onCreate = (e) => e.SetEnabled(false);

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "root.disable"
            }, list.ToArray());

            root.SetEnabled(true);

            Assert.AreEqual(new[] {
                "root.create",
                "root.disable",
                "child0.create",
                "child1.create",
                "root.enable",
                "child0.enable",
                "child1.enable",
                "root.ready",
                "child0.ready",
                "child1.ready"
            }, list.ToArray());
        }

        [Test]
        public void LifeCycleDisableOnEnable() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0"),
                new LifeCycleElement("child1")
            );

            root.onEnable = (e) => e.SetEnabled(false);

            view.AddChild(root);
            
            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child1.create",
                "root.enable",
                "root.disable"
            }, list.ToArray());
        }

        [Test]
        public void LifeCycleDisableOnReadyThenEnable() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0"),
                new LifeCycleElement("child1")
            );

            root.onReady = (e) => {
                e.SetEnabled(false);
                e.SetEnabled(true);
            };

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child1.create",
                "root.enable",
                "child0.enable",
                "child1.enable",
                "root.ready",
                "root.disable",
                "child0.disable",
                "child1.disable",
                "root.enable",
                "child0.enable",
                "child1.enable",
                "child0.ready",
                "child1.ready",
            }, list.ToArray());
        }

        [Test]
        public void LifeCycleMultipleChildLayers() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0",
                    new LifeCycleElement("child0_0"),
                    new LifeCycleElement("child0_1")
                ),
                new LifeCycleElement("child1")
            );

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child0_0.create",
                "child0_1.create",
                "child1.create",
                "root.enable",
                "child0.enable",
                "child0_0.enable",
                "child0_1.enable",
                "child1.enable",
                "root.ready",
                "child0.ready",
                "child0_0.ready",
                "child0_1.ready",
                "child1.ready",
            }, list.ToArray());
        }

        [Test]
        public void CreateChildrenOnCreate() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root");

            root.onCreate = (el) => {
                LifeCycleElement child0 = new LifeCycleElement("child0");
                child0.onCreate = (e) => { e.AddChild(new LifeCycleElement("child0_0")); };
                el.AddChild(child0);
            };

            view.AddChild(root);
            
            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child0_0.create",
                "root.enable",
                "child0.enable",
                "child0_0.enable",
                "root.ready",
                "child0.ready",
                "child0_0.ready",
            }, list.ToArray());
        }

        [Test]
        public void CreateChildrenOnReady() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root");

            root.onReady = (el) => {
                LifeCycleElement child0 = new LifeCycleElement("child0");
                child0.onCreate = (e) => { e.AddChild(new LifeCycleElement("child0_0")); };
                el.AddChild(child0);
            };

            view.AddChild(root);
            
            Assert.AreEqual(new[] {
                "root.create",
                "root.enable",
                "root.ready",
                "child0.create",
                "child0_0.create",
                "child0.enable",
                "child0_0.enable",
                "child0.ready",
                "child0_0.ready",
            }, list.ToArray());
        }

        [Test]
        public void CreateDisabledChildrenOnReady() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root");

            root.onReady = (el) => {
                LifeCycleElement child0 = new LifeCycleElement("child0");
                child0.onCreate = (e) => { e.AddChild(new LifeCycleElement("child0_0")); };
                child0.SetEnabled(false);
                el.AddChild(child0);
            };

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "root.enable",
                "root.ready",
            }, list.ToArray());

            root.GetChild(0).SetEnabled(true);

            Assert.AreEqual(new[] {
                "root.create",
                "root.enable",
                "root.ready",
                "child0.create",
                "child0_0.create",
                "child0.enable",
                "child0_0.enable",
                "child0.ready",
                "child0_0.ready"
            }, list.ToArray());
        }

        [Test]
        public void LifeCycleDestroyChildOnCreate() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0")
            );

            root.onCreate = (el) => { el.GetChild(0).Destroy(); };

            view.AddChild(root);
            
            Assert.AreEqual(new[] {
                "root.create",
                "child0.destroy",
                "root.enable",
                "root.ready",
            }, list.ToArray());
        }

        // todo -- test depth & sibling index after hierarchy manipulation
        [Test]
        public void AddSiblingElement() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0")
            );

            LifeCycleElement uiElement = (LifeCycleElement) root.GetChild(0);
            uiElement.onCreate = (e) => {
                LifeCycleElement lifeCycleElement = new LifeCycleElement("s1");
                root.AddChild(lifeCycleElement);

                lifeCycleElement.onReady = s1e => { uiElement.AddChild(new LifeCycleElement("child0_sub1")); };
            };

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "s1.create",
                "root.enable",
                "child0.enable",
                "s1.enable",
                "root.ready",
                "child0.ready",
                "s1.ready",
                "child0_sub1.create",
                "child0_sub1.enable",
                "child0_sub1.ready",
            }, list.ToArray());
        }


        [Test]
        public void AddSiblingElementToRoot() {
            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
            UIView view = app.CreateView("View0", new Rect(0, 0, 500, 500));

            List<string> list = new List<string>();
            LifeCycleElement.output = list;

            LifeCycleElement root = new LifeCycleElement("root",
                new LifeCycleElement("child0")
            );

            LifeCycleElement uiElement = (LifeCycleElement) root.GetChild(0);
            uiElement.onCreate = (e) => {
                LifeCycleElement lifeCycleElement = new LifeCycleElement("child1");
                root.AddChild(lifeCycleElement);

                lifeCycleElement.onEnable = s1e => { view.AddChild(new LifeCycleElement("root1", new LifeCycleElement("root1_child0"))); };
            };

            view.AddChild(root);

            Assert.AreEqual(new[] {
                "root.create",
                "child0.create",
                "child1.create",
                "root.enable",
                "child0.enable",
                "child1.enable",
                "root1.create",
                "root1_child0.create",
                "root1.enable",
                "root1_child0.enable",
                "root1.ready",
                "root1_child0.ready",
                "root.ready",
                "child0.ready",
                "child1.ready",
            }, list.ToArray());
        }

//        [Test]
//        public void GathersProperLayoutElements() {
//            MockApplication app = new MockApplication(typeof(LifeCycleTestThing));
//            UIView view = app.AddView("View0", new Rect(0, 0, 500, 500));
//
//            UIElement root = view.CreateElement<LifeCycleTestThing>();
//
//            root.AddChild(new UIGroupElement());
//            root.AddChild(new UIGroupElement());
//            root.AddChild(new UIGroupElement());
//
//            Assert.AreEqual(root.View, view);
//            Assert.AreEqual(6, root.children.Count);
//            for (int i = 0; i < root.children.Count; i++) {
//                Assert.AreEqual(view, root.children[i].View);
//            }
//        }

    }

}
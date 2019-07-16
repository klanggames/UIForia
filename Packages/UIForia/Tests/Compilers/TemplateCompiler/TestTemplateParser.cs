using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Linq.Expressions;
using NUnit.Framework;
using Tests.Mocks;
using UIForia.Attributes;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Systems;
using UnityEngine;
using static Tests.Compilers.TemplateCompiler.TestTemplateUtils;

[TestFixture]
public class TestTemplateParser {

    [Template(TemplateType.String, @"
    <UITemplate>
        <Content attr:stuff='yep'>

            <Div attr:id='hello0'/>

            <CompileTestChildElement attr:id='hello1' floatValue='4f'>

           

            </CompileTestChildElement>

            <CompileTestChildElement attr:id='hello2' floatValue='14f'/>

        </Content>
    </UITemplate>
    ")]
    public class CompileTestElement : UIElement { }

    [Template(TemplateType.String, @"
        <UITemplate>
        <Content attr:isChild='yep'>

           <Text>{floatValue}</Text>

        </Content>
        </UITemplate>
    ")]
    public class CompileTestChildElement : UIElement {

        public float floatValue;

    }

    [Test]
    public void ParseTemplate2() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(CompileTestElement));

        UIElement r = compiledTemplate.Create(null, new TemplateScope2(application, new LinqBindingNode(), null));

        Assert.IsInstanceOf<CompileTestElement>(r);
        Assert.AreEqual(3, r.children.size);
        Assert.IsInstanceOf<UIDivElement>(r.children[0]);
        Assert.IsInstanceOf<CompileTestChildElement>(r.children[1]);
        Assert.IsInstanceOf<CompileTestChildElement>(r.children[2]);
        Assert.AreEqual(1, r.attributes.size);
        Assert.AreEqual("stuff", r.attributes[0].name);
        Assert.AreEqual("yep", r.attributes[0].value);
        Assert.AreEqual(1, r.children[0].attributes.size);
        Assert.AreEqual(2, r.children[1].attributes.size);
        Assert.AreEqual(2, r.children[2].attributes.size);

        Assert.AreEqual("id", r.children[0].attributes[0].name);
        Assert.AreEqual("hello0", r.children[0].attributes[0].value);

        Assert.AreEqual("id", r.children[1].attributes[0].name);
        Assert.AreEqual("hello1", r.children[1].attributes[0].value);
        Assert.AreEqual("isChild", r.children[1].attributes[1].name);
        Assert.AreEqual("yep", r.children[1].attributes[1].value);

        Assert.AreEqual("id", r.children[2].attributes[0].name);
        Assert.AreEqual("hello2", r.children[2].attributes[0].value);
        Assert.AreEqual("isChild", r.children[2].attributes[1].name);
        Assert.AreEqual("yep", r.children[2].attributes[1].value);

        UIElement element = application.CreateElementFromPool(typeof(CompileTestElement), null, compiledTemplate.childCount);
        compiledTemplate.Create(element, new TemplateScope2(application, new LinqBindingNode(), null));
        
    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <Div>
                <Text>Outer Content</Text>
                <DefineSlot:Slot0>
                    <Text>Default Slot0 Content</Text>
                    <DefineSlot:Slot1>
                        <Text>Default Slot1 Content</Text>
                    </DefineSlot:Slot1>
                </DefineSlot:Slot0>
            </Div>
        </Content>
    </UITemplate>
    ")]
    public class TemplateWithNestedSlots : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithNestedSlots>
                <Slot:Slot1>
                    <Text>Replaced Slot1 Content</Text>
                </Slot:Slot1>
            </TemplateWithNestedSlots>
        </Content>
    </UITemplate>
    ")]
    public class TemplateReplaceInnerSlot : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithNestedSlots>
                <Slot:Slot0>
                    <Text>Replaced Slot0 Content</Text>
                </Slot:Slot0>
            </TemplateWithNestedSlots>
        </Content>
    </UITemplate>
    ")]
    public class TemplateReplaceOuterSlot : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithNestedSlots>
                <Slot:Slot0>
                    <Text>Replaced Slot0 Content</Text>
                </Slot:Slot0>
                <Slot:Slot1>
                    <Text>Replaced Slot1 Content</Text>
                </Slot:Slot1>
            </TemplateWithNestedSlots>
        </Content>
    </UITemplate>
    ")]
    public class TemplateReplaceInnerAndOuterSlot : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithSlots>
                <Slot:Slot0>
                    <Text>Replaced Slot0 Content</Text>
                </Slot:Slot0>
            </TemplateWithSlots>
        </Content>
    </UITemplate>
    ")]
    public class TemplateUsingSlots : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <Text>Outer Content</Text>
            <DefineSlot:Slot0>
                <Text>Default SlotContent</Text>
            </DefineSlot:Slot0>
        </Content>
    </UITemplate>
    ")]
    public class TemplateWithSlotsSimple : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithSlotsSimple>
                <Slot:Slot0>
                    <Text>Replaced Slot0 Content</Text>
                </Slot:Slot0>
                <Slot:Slot0>
                    <Text>Replaced Slot0 Content</Text>
                </Slot:Slot0>
            </TemplateWithSlotsSimple>
        </Content>
    </UITemplate>
    ")]
    public class DuplicateSlotInput : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <Slot:Slot0>
                <Text>Replaced Slot0 Content</Text>
            </Slot:Slot0>
        </Content>
    </UITemplate>
    ")]
    public class OrphanedSlotContent : UIElement { }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithSlotsSimple>
                <Slot:NotHere>
                    <Text>Replaced NotHere Content</Text>
                </Slot:NotHere>
            </TemplateWithSlotsSimple>
        </Content>
    </UITemplate>
    ")]
    public class UnmatchedSlotContent : UIElement { }

    [Template(TemplateType.String, @"
         <UITemplate>    
             <Content>
     
                 <DefineSlot:TemplateSlot attr:template='true'>
     
                     <Text>Original Template Content Here</Text>
     
                 </DefineSlot:TemplateSlot>
     
                 <Div attr:id='attach-point'/>
     
                 <Children/>
     
             </Content>
         </UITemplate>
         ")]
         public class CompileAsTemplateFn : UIElement {
             
             public override void OnCreate() {
                 //FindById("attach-point").AddChild(GetStoredTemplate("TemplateSlot"));
             }
     
         }
    
    [Test]
    public void TestSlotTemplate() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(TemplateWithSlotsSimple));

        UIElement element = new TemplateWithSlotsSimple();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        AssertElementHierarchy(new ElementAssertion(typeof(TemplateWithSlotsSimple)) {
            children = new[] {
                new ElementAssertion(typeof(UITextElement)) {
                    textContent = "Outer Content"
                },
                new ElementAssertion(typeof(UISlotContent)) {
                    children = new[] {
                        new ElementAssertion(typeof(UITextElement)) {
                            textContent = "Default SlotContent"
                        }
                    }
                }
            }
        }, element);
    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>
            <TemplateWithSlotsSimple>

                <Slot:Slot0>

                    <Text>Override SlotContent</Text>
 
                </Slot:Slot0>

            </TemplateWithSlotsSimple>
        </Content>
    </UITemplate>
    ")]
    public class TestSimpleSlotReplace : UIElement { }

    [Test]
    public void SimpleSlotReplace() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(TestSimpleSlotReplace));

        UIElement element = new TestSimpleSlotReplace();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        AssertElementHierarchy(new ElementAssertion(typeof(TestSimpleSlotReplace)) {
            children = new[] {
                new ElementAssertion(typeof(TemplateWithSlotsSimple)) {
                    children = new[] {
                        new ElementAssertion(typeof(UITextElement)) {
                            textContent = "Outer Content"
                        },
                        new ElementAssertion(typeof(UISlotContent)) {
                            children = new[] {
                                new ElementAssertion(typeof(UITextElement)) {
                                    textContent = "Override SlotContent"
                                }
                            }
                        }
                    }
                }
            }
        }, element);
    }

    [Test]
    public void NestedSlot_ReplaceInner() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(TemplateReplaceInnerSlot));

        UIElement element = new TemplateReplaceInnerSlot();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        AssertElementHierarchy(new ElementAssertion(typeof(TemplateReplaceInnerSlot)) {
            children = new[] {
                new ElementAssertion(typeof(TemplateWithNestedSlots)) {
                    children = new[] {
                        new ElementAssertion(typeof(UIDivElement)) {
                            children = new[] {
                                new ElementAssertion(typeof(UITextElement)) {
                                    textContent = "Outer Content"
                                },
                                new ElementAssertion(typeof(UISlotContent)) {
                                    children = new[] {
                                        new ElementAssertion(typeof(UITextElement)) {
                                            textContent = "Default Slot0 Content"
                                        },
                                        new ElementAssertion(typeof(UISlotContent)) {
                                            children = new[] {
                                                new ElementAssertion(typeof(UITextElement)) {
                                                    textContent = "Replaced Slot1 Content"
                                                },
                                            }
                                        },
                                    },
                                },
                            }
                        },
                    }
                }
            }
        }, element);
    }

    [Test]
    public void NestedSlot_ReplaceOuter() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(TemplateReplaceOuterSlot));

        UIElement element = new TemplateReplaceOuterSlot();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        AssertElementHierarchy(new ElementAssertion(typeof(TemplateReplaceOuterSlot)) {
            children = new[] {
                new ElementAssertion(typeof(TemplateWithNestedSlots)) {
                    children = new[] {
                        new ElementAssertion(typeof(UIDivElement)) {
                            children = new[] {
                                new ElementAssertion(typeof(UITextElement)) {
                                    textContent = "Outer Content"
                                },
                                new ElementAssertion(typeof(UISlotContent)) {
                                    children = new[] {
                                        new ElementAssertion(typeof(UITextElement)) {
                                            textContent = "Replaced Slot0 Content"
                                        },
                                    },
                                },
                            }
                        },
                    }
                }
            }
        }, element);
    }

    [Test]
    public void NestedSlot_ReplaceOuterAndInner() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        TemplateParseException exception = Assert.Throws<TemplateParseException>(() => { compiler.GetCompiledTemplate(typeof(TemplateReplaceInnerAndOuterSlot)); });
    }

    [Test]
    public void DuplicateSlotInputShouldFail() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        TemplateParseException exception = Assert.Throws<TemplateParseException>(() => { compiler.GetCompiledTemplate(typeof(DuplicateSlotInput)); });
    }

    [Test]
    public void OrphanedSlotContentShouldFail() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        TemplateParseException exception = Assert.Throws<TemplateParseException>(() => { compiler.GetCompiledTemplate(typeof(OrphanedSlotContent)); });
    }
    
    [Test]
    public void UnmatchedSlotNamesShouldFail() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        TemplateParseException exception = Assert.Throws<TemplateParseException>(() => { compiler.GetCompiledTemplate(typeof(UnmatchedSlotContent)); });
    }

    [Test]
    public void CompileSlotDefaultToTemplateFunction() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(CompileAsTemplateFn));

        UIElement element = new CompileAsTemplateFn();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        Assert.IsNotNull(element.storedTemplates);
        
    }

    
}
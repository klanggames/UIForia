using NUnit.Framework;
using Tests.Mocks;
using UIForia.Attributes;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Systems;
using static Tests.Compilers.TemplateCompiler.TestTemplateUtils;

[TestFixture]
public class TestTemplateParser_Context {

    public class TestTemplateContext : TemplateContext {

        public int value;

    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>

          <Div ctx:context='someContext'>
              {$context.value}  
          </Div>

        </Content>
    </UITemplate>
    ")]
    public class TestSimpleContext : UIElement {

        public TestTemplateContext someContext = new TestTemplateContext() {
            value = 1341
        };

    }

    [Test]
    public void CompileContext_Single() {
        MockApplication application = MockApplication.CreateWithoutView();

        TemplateCompiler compiler = new TemplateCompiler(application);

        CompiledTemplate compiledTemplate = compiler.GetCompiledTemplate(typeof(TestSimpleContext));

        application.templateData.Build();

        TestSimpleContext element = new TestSimpleContext();
        LinqBindingNode linqBindingNode = new LinqBindingNode();
        compiledTemplate.Create(element, new TemplateScope2(application, linqBindingNode, null));

        UITextElement textElement = (UITextElement) element.children[0].children[0];
    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>

          <Div ctx:context='someContext' ctxvar:times2='value * 2'>
              {$times2}  
          </Div>

        </Content>
    </UITemplate>
    ")]
    public class TestSimpleContext2 : UIElement {

        public TestTemplateContext someContext = new TestTemplateContext() {
            value = 1341
        };

    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>

          <Div ctx:context='someContext' ctxvar:times2='value * 2'>
              <DefineSlot:SlotWithContext>
                {$times2}  
              </DefineSlot:SlotWithContext>
          </Div>

        </Content>
    </UITemplate>
    ")]
    public class TestSimpleContext_SlotDefiner : UIElement {

        public TestTemplateContext someContext = new TestTemplateContext() {
            value = 1341
        };

    }

    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>

            <TestSimpleContext_SlotDefiner>

              <Slot:SlotWithContext>
                    text is: {$times2}  
              </Slot:SlotWithContext>

            </TestSimpleContext_SlotDefiner>

        </Content>
    </UITemplate>
    ")]
    public class TestSimpleContext_SlotUser : UIElement { }

    [Test]
    public void CompileContext_SlotUserHasContext() {
        MockApplication application = new MockApplication(typeof(TestSimpleContext_SlotUser));
        application.Update();
    }

}
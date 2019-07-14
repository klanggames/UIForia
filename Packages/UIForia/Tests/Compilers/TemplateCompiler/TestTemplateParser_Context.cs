using NUnit.Framework;
using Tests.Mocks;
using UIForia.Attributes;
using UIForia.Compilers;
using UIForia.Elements;
using static Tests.Compilers.TemplateCompiler.TestTemplateUtils;

[TestFixture]
public class TestTemplateParser_Context {

    public class TestTemplateContext {

        public int value;

    }
    
    [Template(TemplateType.String, @"
    <UITemplate>    
        <Content>

          <Div ctx:someContext='context'>
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
        compiledTemplate.Compile();

        TestSimpleContext element = new TestSimpleContext();

        compiledTemplate.Create(element, new TemplateScope2(application, null, null));

        
    }

}
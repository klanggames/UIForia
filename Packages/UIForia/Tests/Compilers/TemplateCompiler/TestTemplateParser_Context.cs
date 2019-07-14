using System.Text;
using NUnit.Framework;
using Tests.Mocks;
using UIForia.Attributes;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Systems;
using static Tests.Compilers.TemplateCompiler.TestTemplateUtils;

public class TextBinding : LinqBinding {

    string[] parts;
    
    private static readonly StringBuilder builder = new StringBuilder(1024);
    
    public override void Execute(UIElement templateRoot, UIElement current, TemplateContext context) {
    
        // todo -- don't use the builder, evaluate parts individually and pass all to text info 
        // or intercept expression output and write to char array w/ offset instead
        
//        UITextElement textElement = (UITextElement) current;
//        
//        textElement.SetText();
        
    }

    public override bool CanBeShared => true;

}

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
        compiledTemplate.Compile();
        
        TestSimpleContext element = new TestSimpleContext();
        LinqBindingNode linqBindingNode = new LinqBindingNode();
        compiledTemplate.Create(element, new TemplateScope2(application, linqBindingNode, null));

        
    }

}
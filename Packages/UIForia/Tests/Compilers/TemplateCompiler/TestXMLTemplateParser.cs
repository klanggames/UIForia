using NUnit.Framework;
using UIForia.Compilers;
using UIForia.Util;
using Assert = UnityEngine.Assertions.Assert;

[TestFixture]
public class TestXMLTemplateParser {

    [Test]
    public void ProcessText_Constant() {
        const string input = "this is a normal piece of text";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(1, output.size);
        Assert.AreEqual("'" + input + "'", output[0]);
    }
    
    [Test]
    public void ProcessText_ConstantWithWhiteSpace() {
        const string input = "\n\n\tthis is a normal piece of text";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(1, output.size);
        Assert.AreEqual("'" + input + "'", output[0]);
    }
    
    [Test]
    public void ProcessText_Expression() {
        const string input = "{some expression}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(1, output.size);
        Assert.AreEqual("some expression", output[0]);
    }
    
    [Test]
    public void ProcessText_ExpressionWithWhitespace() {
        const string input = "\n\n\n\n{some expression}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(2, output.size);
        Assert.AreEqual("'\n\n\n\n'", output[0]);
        Assert.AreEqual("some expression", output[1]);
    }
    
    [Test]
    public void ProcessText_ExpressionWithNestedBraces() {
        const string input = "\n\n\n\n{some expression{}}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(2, output.size);
        Assert.AreEqual("'\n\n\n\n'", output[0]);
        Assert.AreEqual("some expression{}", output[1]);
    }
    
    [Test]
    public void ProcessText_EscapedBrace() {
        const string input = "&obrc;";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(1, output.size);
        Assert.AreEqual("'{'", output[0]);
    }
    
    [Test]
    public void ProcessText_EscapedBracePair() {
        const string input = "&obrc;stuff&cbrc;";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(1, output.size);
        Assert.AreEqual("'{stuff}'", output[0]);
    }
    
    [Test]
    public void ProcessText_MultipleExpressions() {
        const string input = "{hello}{there}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(2, output.size);
        Assert.AreEqual("hello", output[0]);
        Assert.AreEqual("there", output[1]);
    }
    
    [Test]
    public void ProcessText_MultipleExpressionsWithConstants() {
        const string input = "{hello}{'there'}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(2, output.size);
        Assert.AreEqual("hello", output[0]);
        Assert.AreEqual("'there'", output[1]);
    }
    
    [Test]
    public void ProcessText_MultipleExpressionsMixed() {
        const string input = "{hello}hi there{'there'}";
        LightList<string> output = new LightList<string>();
        XMLTemplateParser.ProcessTextExpressions(input, output);
        Assert.AreEqual(3, output.size);
        Assert.AreEqual("hello", output[0]);
        Assert.AreEqual("'hi there'", output[1]);
        Assert.AreEqual("'there'", output[2]);
    }
    
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Linq.Expressions;
using NUnit.Framework;
using UIForia.Bindings;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Expressions;
using UIForia.Extensions;
using UIForia.Parsing.Expression;
using UIForia.Parsing.Expression.AstNodes;
using UIForia.Test.NamespaceTest.SomeNamespace;
using UnityEngine;
using Expression = System.Linq.Expressions.Expression;

[TestFixture]
public class TestLinqCompiler {

    private class ExpressionErrorLogger {

        public string error;

        public void OnError(string error) {
            this.error = error;
        }

    }

    private class LinqThing {

        public float floatValue;

        public ValueHolder<Vector3> refValueHolderVec3 = new ValueHolder<Vector3>();
        public ValueHolder<float> valueHolderFloat = new ValueHolder<float>();
        public ValueHolder<ValueHolder<Vector3>> nestedValueHolder = new ValueHolder<ValueHolder<Vector3>>();
        public StructValueHolder<Vector3> svHolderVec3;
        public Vector3[] vec3Array;
        public List<Vector3> vec3List;
        public int intVal;
        public Dictionary<string, Vector3> vec3Dic;
        public string stringField;
        public const float ConstFloatValue = 14242;
        public LinqThing self => this;
        public Indexable indexable;

        public Func<int, int> FnReturningFunction() {
            return (int val) => val;
        }

        public int GetIntValue(float val) {
            return (int) val;
        }

        public Vector3 GetVectorValue() {
            return svHolderVec3.value;
        }

        public Vector3 GetVectorValueWith1Arg(int arg) {
            return svHolderVec3.value;
        }

        public float OverloadWithValue(int value) {
            stringField = nameof(OverloadWithValue) + "_int";
            return value;
        }

        public float OverloadWithValue(float value = 10.02424f) {
            stringField = nameof(OverloadWithValue) + "_float";
            return value;
        }

        public int methodCallCount = 0;

        public LinqThing Method() {
            methodCallCount++;
            return this;
        }

        public LinqThing Method(int arg0) {
            methodCallCount++;
            return this;
        }

        public LinqThing Method(int arg0, int arg1) {
            methodCallCount++;
            return this;
        }


        public LinqThing Method(int arg0, int arg1, int arg2) {
            methodCallCount++;
            return this;
        }

    }

    public struct Indexable {

        public string this[int idx] {
            get => idx.ToString();
        }

    }

    public struct StructValueHolder<T> {

        public T value;

        public StructValueHolder(T value) {
            this.value = value;
        }

    }

    public class ValueHolder<T> {

        public T value;

        public ValueHolder(T value = default) {
            this.value = value;
        }

    }

    private static readonly Type LinqType = typeof(LinqThing);

    public abstract class LinqBinding {

        public abstract void Execute(ExpressionContext ctx);

    }

    public class ReadBinding : LinqBinding {

        public override void Execute(ExpressionContext ctx) { }

    }

    // if no listeners and field or auto prop then just assign, no need to check

    // compiler.Assign(left, Expression.Constant(34f));
    // compiler.EnableNullChecking(null);
    // compiler.SetOutOfBoundsHandler(HandlerSettings);
    // compiler.TryCatch(() => {}, () => {})
//            compiler.Variable("left", attributeDefinition.key, () => compiler.Call(typeof(LinqCompiler).GetMethod("DebugLog"), root, fieldName, astNode));
//            compiler.Variable("right", attributeDefinition.value, "errorHandler", NotNullChecked | NeverOutOfBounds);
//            compiler.IfEqual("left", "right", (c) => {
//                compiler.Assign("left", "right + 1");
//                compiler.Invoke("root.ChangeHandler()");
//                compiler.Invoke("root.ChangeHandler()");
//                compiler.Invoke("root.ChangeHandler()");
//                compiler.Invoke("root.ChangeHandler(oldValue, newValue)");
//                compiler.Invoke("root", changeHandler, "newValue, "oldValue", "1f", compiler.Constant(new Vector3(14, 24, 21));
//            });
//          compiler.Return("() => value");
    // return = assign to output variable; goto return;

    // collapse stage 'optimizes' by folding constants, removing variable writes for return statements where not needed, don't generate return label when not needed.
    // there is no 'current block' the compiler has context and thats it.
    // signature must be set before calling other methods. no error will thrown but it is incorrect.


    public class BindingCompiler : LinqCompiler {

        public LambdaExpression BuildMemberReadBinding(Type root, Type elementType, AttributeDefinition attributeDefinition) {
            LinqCompiler compiler = new LinqCompiler();

            MethodInfo[] changedHandlers = GetPropertyChangedHandlers(elementType, "fieldName");

            compiler.SetSignature(
                new Parameter(root, "root", ParameterFlags.NeverNull),
                new Parameter(elementType, "element", ParameterFlags.NeverNull)
            );

            // left must be a member access expression or variable
            LHSStatementChain left = compiler.AssignableStatement(attributeDefinition.key);

            Expression accessor = compiler.AccessorStatement(left.targetExpression.Type, attributeDefinition.value);
            Expression right = null;

            // todo -- we really want the assignment chain to only be unrolled when we perform the assignment. We have extra struct copies right now :(

            if (accessor is ConstantExpression) {
                right = accessor;
            }
            else {
                right = compiler.AddVariable(left.targetExpression.Type, "right");
                compiler.Assign(right, accessor);
            }

            // todo -- can eliminate the if here if the assignment is to a simple field and no handlers are used
            compiler.IfNotEqual(left, right, () => {
                compiler.Assign(left, right);
                if (changedHandlers != null) {
                    for (int i = 0; i < changedHandlers.Length; i++) {
                        //compiler.Invoke(rootParameter, changedHandlers[i], compiler.GetVariable("previousValue"));
                    }
                }

                if (elementType.Implements(typeof(IPropertyChangedHandler))) {
                    //compiler.Invoke("element", "OnPropertyChanged", compiler.GetVariable("currentValue"));
                }
            });

            compiler.Log();
            return compiler.BuildLambda();
        }

        public LinqBinding CompileMemberReadBinding(Type root, Type elementType, AttributeDefinition attributeDefinition) {
            return null; //BuildMemberReadBinding(root, elementType, attributeDefinition).Compile();
        }

        private MethodInfo[] GetPropertyChangedHandlers(Type targetType, string fieldname) {
            return null;
        }

    }


    private class TestElement : UIElement { }

    [Test]
    public void CompileConstant() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();
        compiler.Return("5f");
        compiler.Log();
        AssertStringsEqual(@"
        () => 
        {
            return 5f;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileFieldAccess_NullChecked() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing")
        );
        compiler.Return("thing.floatValue");
        compiler.Log();
        AssertStringsEqual(@"
       (TestLinqCompiler.LinqThing thing) =>
        {
            float retn_val;

            retn_val = default(float);
            if (thing == null)
            {
                goto retn;
            }
            retn_val = thing.floatValue;
        retn:
            return retn_val;
        }
        ", compiler.Print());
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.floatValue = 24;
        Assert.AreEqual(24, fn(thing));
        Assert.AreEqual(0, fn(null));
    }

    [Test]
    public void CompileFieldAccess_NoNullCheck() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing", ParameterFlags.NeverNull)
        );
        compiler.Return("thing.floatValue");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return thing.floatValue;
        }
        ", compiler.Print());
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.floatValue = 24;
        Assert.AreEqual(24, fn(thing));
        Assert.Throws<NullReferenceException>(() => { fn(null); });
    }

    [Test]
    public void CompileArrayIndex_NoNullCheck() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetNullCheckingEnabled(false);
        compiler.SetOutOfBoundsCheckingEnabled(false);
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing")
        );
        compiler.Return("thing.vec3Array[0].x");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return thing.vec3Array[0].x;
        }
        ", compiler.Print());
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.vec3Array = new[] {new Vector3(1, 2, 3)};
        Assert.AreEqual(1, fn(thing));
        Assert.Throws<NullReferenceException>(() => { fn(null); });
    }

    [Test]
    public void CompileArrayIndex_NullCheck_BoundsCheck() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetNullCheckingEnabled(true);
        compiler.SetOutOfBoundsCheckingEnabled(true);
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing")
        );
        compiler.Return("thing.vec3Array[0].x");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            float retn_val;
            UnityEngine.Vector3[] nullCheck;

            retn_val = default(float);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (0 >= nullCheck.Length)
            {
                goto retn;
            }
            retn_val = nullCheck[0].x;
        retn:
            return retn_val;
        }
        ", compiler.Print());
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.vec3Array = new[] {new Vector3(1, 2, 3)};
        Assert.AreEqual(1, fn(thing));
    }

    [Test]
    public void CompileArrayIndex_BoundsCheck() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetNullCheckingEnabled(false);
        compiler.SetOutOfBoundsCheckingEnabled(true);
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing")
        );
        compiler.Return("thing.vec3Array[0].x");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            float retn_val;
            UnityEngine.Vector3[] toBoundsCheck;

            retn_val = default(float);
            toBoundsCheck = thing.vec3Array;
            if (0 >= toBoundsCheck.Length)
            {
                goto retn;
            }
            retn_val = toBoundsCheck[0].x;
        retn:
            return retn_val;
        }
        ", compiler.Print());
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.vec3Array = new[] {new Vector3(1, 2, 3)};
        Assert.AreEqual(1, fn(thing));
    }


    [Test]
    public void CompileArrayIndex_Constant() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("thing", ParameterFlags.NeverNull));
        compiler.Return("thing.vec3Array[1]");
        Assert.AreEqual(thing.vec3Array[1], compiler.Compile<Func<LinqThing, Vector3>>()(thing));
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            UnityEngine.Vector3 retn_val;
            UnityEngine.Vector3[] nullCheck;

            retn_val = default(UnityEngine.Vector3);
            nullCheck = thing.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (1 >= nullCheck.Length)
            {
                goto retn;
            }
            retn_val = nullCheck[1];
        retn:
            return retn_val;
        }
        ", compiler.Print());
        compiler.Reset();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("thing"));
        compiler.Return("thing.vec3Array[1 + 1]");
        compiler.Log();
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, Vector3>>()(thing));
        compiler.Reset();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("thing"));
        compiler.Return("thing.vec3Array[99999]");
        Assert.AreEqual(default(Vector3), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
        compiler.Reset();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("thing"));
        compiler.Return("thing.vec3Array[-14]");
        Assert.AreEqual(default(Vector3), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
    }

    [Test]
    public void CompileArrayIndex_Expression_NullAndBoundsChecked() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(
            new Parameter<LinqThing>("thing"),
            new Parameter<int>("arg0"),
            new Parameter<int>("arg1")
        );

        compiler.Return("thing.vec3Array[arg0 + thing.intVal - arg1]");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing, int arg0, int arg1) =>
        {
            UnityEngine.Vector3 retn_val;
            UnityEngine.Vector3[] nullCheck;
            int indexer;

            retn_val = default(UnityEngine.Vector3);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            indexer = (arg0 + thing.intVal) - arg1;
            if ((indexer < 0) || (indexer >= nullCheck.Length))
            {
                goto retn;
            }
            retn_val = nullCheck[indexer];
        retn:
            return retn_val;
        }", compiler.Print());
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
    }

    [Test]
    public void CompileArrayIndex_Expression_NullChecked() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(
            new Parameter<LinqThing>("thing"),
            new Parameter<int>("arg0"),
            new Parameter<int>("arg1")
        );

        compiler.SetOutOfBoundsCheckingEnabled(false);
        compiler.Return("thing.vec3Array[arg0 + thing.intVal - arg1]");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing, int arg0, int arg1) =>
        {
            UnityEngine.Vector3 retn_val;
            UnityEngine.Vector3[] nullCheck;

            retn_val = default(UnityEngine.Vector3);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            retn_val = nullCheck[(arg0 + thing.intVal) - arg1];
        retn:
            return retn_val;
        }", compiler.Print());
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
    }

    [Test]
    public void CompileArrayIndex_Expression_NoChecks() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(
            new Parameter<LinqThing>("thing"),
            new Parameter<int>("arg0"),
            new Parameter<int>("arg1")
        );

        compiler.SetOutOfBoundsCheckingEnabled(false);
        compiler.SetNullCheckingEnabled(false);
        compiler.Return("thing.vec3Array[arg0 + thing.intVal - arg1]");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing, int arg0, int arg1) =>
        {
            return thing.vec3Array[(arg0 + thing.intVal) - arg1];
        }",
            compiler.Print());
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
    }

    [Test]
    public void CompileArrayIndex_SafeAccess() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(2, 2, 2),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(
            new Parameter<LinqThing>("thing"),
            new Parameter<int>("arg0"),
            new Parameter<int>("arg1")
        );

        compiler.AddNamespace("UnityEngine");
        compiler.SetNullCheckingEnabled(false);

        compiler.Return("thing.vec3Array?[arg0 + thing.intVal - arg1] ?? Vector3.one");
        compiler.Log();

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing, int arg0, int arg1) =>
        {
            UnityEngine.Vector3 retn_val;
            UnityEngine.Vector3? nullableAccess;

            retn_val = default(UnityEngine.Vector3);
            nullableAccess = default(UnityEngine.Vector3?);
            if (thing.vec3Array != null)
            {
                int indexer;
                UnityEngine.Vector3[] toBoundsCheck;

                indexer = (arg0 + thing.intVal) - arg1;
                toBoundsCheck = thing.vec3Array;
                if ((indexer < 0) || (indexer >= toBoundsCheck.Length))
                {
                    goto retn;
                }
                nullableAccess = (UnityEngine.Vector3?)toBoundsCheck[indexer];
            }
            retn_val = nullableAccess ?? UnityEngine.Vector3.one;
        retn:
            return retn_val;
        }    
        ", compiler.Print());
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
        thing.vec3Array = null;
        Assert.AreEqual(Vector3.one, compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
    }

    [Test]
    public void CompileArrayIndex_InvalidExpression() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("thing"));
        CompileException exception = Assert.Throws<CompileException>(() => { compiler.Return("thing.vec3Array[thing.vec3Dic]"); });
        Assert.AreEqual(CompileException.InvalidTargetType(typeof(int), typeof(Dictionary<string, Vector3>)).Message, exception.Message);
    }

    [Test]
    public void CompileFieldAccess_Static() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<Color>();
        compiler.Return("UnityEngine.Color.red");
        AssertStringsEqual(@"
        () => 
        {
            return UnityEngine.Color.red;
        }    
        ", compiler.Print());
        Assert.AreEqual(Color.red, compiler.Compile<Func<Color>>()());
    }

    [Test]
    public void CompileFieldAccess_Const() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();
        compiler.AddNamespace("UIForia.Test.NamespaceTest.SomeNamespace");
        compiler.Return("NamespaceTestClass.FloatValueConst");
        AssertStringsEqual(@"
        () => 
        {
            return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValueConst;
        }    
        ", compiler.Print());
        Assert.AreEqual(NamespaceTestClass.FloatValueConst, compiler.Compile<Func<float>>()());
    }

    [Test]
    public void CompileFieldAccess_Const_NonPublicType() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();

        CompileException exception = Assert.Throws<CompileException>(() => { compiler.Return("TestLinqCompiler.LinqThing.ConstFloatValue"); });
        Assert.AreEqual(CompileException.NonPublicType(typeof(LinqThing)).Message, exception.Message);
    }

    [Test]
    public void CompileFieldAccess_NullChecked_CustomError() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(
            new Parameter<LinqThing>("thing"),
            new Parameter<LinqThing>("thing2"),
            new Parameter<ExpressionErrorLogger>("logger")
        );

        compiler.SetNullCheckHandler((c, expression) => {
            ParameterExpression parameterExpression = expression as ParameterExpression;
            c.Assign("logger.error", $"'{parameterExpression.Name} was null'");
        });

        compiler.Return("thing.floatValue + thing2.floatValue");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing, TestLinqCompiler.LinqThing thing2, TestLinqCompiler.ExpressionErrorLogger logger) =>
        {
        float retn_val;

        retn_val = default(float);
        if (thing == null)
        {
            logger.error = ""thing was null"";
            goto retn;
        }
        if (thing2 == null)
        {
            logger.error = ""thing2 was null"";
            goto retn;
        }
        retn_val = thing.floatValue + thing2.floatValue;
        retn:
            return retn_val;
        }
        ", compiler.Print());
        Func<LinqThing, LinqThing, ExpressionErrorLogger, float> fn = compiler.Compile<Func<LinqThing, LinqThing, ExpressionErrorLogger, float>>();
        LinqThing thing = new LinqThing();
        LinqThing thing2 = new LinqThing();
        thing.floatValue = 24;
        thing2.floatValue = 284;
        ExpressionErrorLogger logger = new ExpressionErrorLogger();

        Assert.AreEqual(thing.floatValue + thing2.floatValue, fn(thing, thing2, logger));
        Assert.AreEqual(null, logger.error);

        Assert.AreEqual(0, fn(null, thing2, logger));
        Assert.AreEqual("thing was null", logger.error);

        Assert.AreEqual(0, fn(thing, null, logger));
        Assert.AreEqual("thing2 was null", logger.error);
    }

    [Test]
    public void CompileClosure() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Func<int>>(new Parameter<LinqThing>("root"));

        compiler.AddVariable(typeof(Vector3[]), "vectors");
        compiler.Assign("vectors", "root.vec3Array");
        compiler.Return("() => root.intVal");

        Func<LinqThing, Func<int>> fn = compiler.Compile<Func<LinqThing, Func<int>>>();
        LinqThing element = new LinqThing();
        LinqThing element1 = new LinqThing();

        element.intVal = 12042;
        element1.intVal = 12044;

        Assert.AreEqual(element.intVal, fn(element)());
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            UnityEngine.Vector3[] vectors;
            System.Func<int> retn_val;

            retn_val = default(System.Func<int>);
            if (root == null)
            {
                goto retn;
            }
            vectors = root.vec3Array;
            retn_val = () =>
            {
                int retn_val_1;

                retn_val_1 = default(int);
                if (root == null)
                {
                    goto retn_1;
                }
                retn_val_1 = root.intVal;
            retn_1:
                return retn_val_1;
            };
        retn:
            return retn_val;
        }
        ", compiler.Print());
        compiler.Reset();

        compiler.SetSignature<Func<LinqThing, int>>(new Parameter<LinqThing>("root"));
        compiler.Return("(el) => root.intVal + el.intVal");
        compiler.Log();
        Func<LinqThing, Func<LinqThing, int>> fn2 = compiler.Compile<Func<LinqThing, Func<LinqThing, int>>>();
        Assert.AreEqual(element.intVal + element1.intVal, fn2(element)(element1));
    }

    [Test]
    public void CompileClosure_UntypedArguments() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Func<float, int, int>>(new Parameter<UIElement>("root"));

        compiler.Return("(intVal, intVal2) => (intVal + root.id) + intVal2");

        Func<UIElement, Func<float, int, int>> fn = compiler.Compile<Func<UIElement, Func<float, int, int>>>();

        TestElement element = new TestElement();

        Assert.AreEqual(15f + element.id + 5, fn(element)(15f, 5));
    }

    [Test]
    public void CompileStringConcat_StringWithString() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<string>();

        compiler.Return("'str0' + 'str1'");
        compiler.Log();
        Assert.AreEqual("str0str1", compiler.Compile<Func<string>>()());
    }

    [Test]
    public void CompileStringConcat_StringWithNonString() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<string>();

        compiler.Return("'str0' + 1");
        compiler.Log();

        Assert.AreEqual("str01", compiler.Compile<Func<string>>()());
    }

    [Test]
    public void CompileStringConcat_NonStringWithString() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<string>();

        compiler.Return("0 + 'str1'");
        compiler.Log();
        AssertStringsEqual(@"
        () =>
        {
            return ""0str1"";
        }",
            compiler.Print());
        Assert.AreEqual("0str1", compiler.Compile<Func<string>>()());
    }

    [Test]
    public void CompileStringConcat_LeftNonConstWithNumeric() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<string>(new Parameter<Tuple<string>>("s"));
        compiler.Return("s.Item1 + 1");
        Tuple<string> s = new Tuple<string>("hello");
        Assert.AreEqual("hello1", compiler.Compile<Func<Tuple<string>, string>>()(s));
    }

    [Test]
    public void CompileStringConcat_RightNonConstWithNumeric() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<string>(new Parameter<Tuple<string>>("s"));
        compiler.Return("1 + s.Item1");
        Tuple<string> s = new Tuple<string>("hello");
        AssertStringsEqual(@"
        (System.Tuple<string> s) =>
        {
            string retn_val;

            retn_val = default(string);
            if (s == null)
            {
                goto retn;
            }
            retn_val = string.Concat(""1"", s.Item1);
                retn:
                return retn_val;
            }
        ",
            compiler.Print());
        Assert.AreEqual("1hello", compiler.Compile<Func<Tuple<string>, string>>()(s));
    }

    [Test]
    public void CompileReadFromValueChain() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(new Parameter<LinqThing>("root", ParameterFlags.NeverNull));
        compiler.Return("root.svHolderVec3.value.z");
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.svHolderVec3.value.z = 12;
        Assert.AreEqual(12, fn(thing));
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) => 
        {
            return root.svHolderVec3.value.z;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileReadFromValueWithNullChecksChain() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<float>(new Parameter<LinqThing>("root", ParameterFlags.NeverNull));

        compiler.Return("root.refValueHolderVec3.value.z");

        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        LinqThing thing = new LinqThing();
        thing.refValueHolderVec3 = new ValueHolder<Vector3>();
        thing.refValueHolderVec3.value.z = 12;
        Assert.AreEqual(12, fn(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            float retn_val;
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck;

            retn_val = default(float);
            nullCheck = root.refValueHolderVec3;
            if (nullCheck == null)
            {
                goto retn;
            }
            retn_val = nullCheck.value.z;
        retn:
            return retn_val;
        }
        ", compiler.Print());

        compiler.Reset();

        compiler.SetSignature<float>(new Parameter<LinqThing>("root", ParameterFlags.NeverNull));
        compiler.Return("root.nestedValueHolder.value.value.z");

        fn = compiler.Compile<Func<LinqThing, float>>();
        thing.nestedValueHolder.value = new ValueHolder<Vector3>();
        thing.nestedValueHolder.value.value = new Vector3(10, 11, 12);
        Assert.AreEqual(12, fn(thing));
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            float retn_val;
            TestLinqCompiler.ValueHolder<TestLinqCompiler.ValueHolder<UnityEngine.Vector3>> nullCheck;
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck_0;

            retn_val = default(float);
            nullCheck = root.nestedValueHolder;
            if (nullCheck == null)
            {
                goto retn;
            }
            nullCheck_0 = nullCheck.value;
            if (nullCheck_0 == null)
            {
                goto retn;
            }
            retn_val = nullCheck_0.value.z;
        retn:
            return retn_val;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileSimpleMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "4f"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        Assert.AreEqual(0, element.floatValue);
        fn.Invoke(root, element);
        Assert.AreEqual(4, element.floatValue);
    }

    [Test]
    public void CompileDotAccessRefMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // todo handle implicit conversion casting
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.valueHolderFloat.value"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.valueHolderFloat = new ValueHolder<float>(42);
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.valueHolderFloat.value);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.ValueHolder<float> nullCheck;
            float right;

            nullCheck = root.valueHolderFloat;
            if (nullCheck == null)
            {
                goto retn;
            }
            right = nullCheck.value;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileDotAccessStructMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.svHolderVec3.value.z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.svHolderVec3 = new StructValueHolder<Vector3>(new Vector3(0, 0, 42));
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.svHolderVec3.value.z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            float right;

            right = root.svHolderVec3.value.z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileDotAccessMixedStructRefMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.refValueHolderVec3.value.z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.refValueHolderVec3 = new ValueHolder<Vector3>(new Vector3(0, 0, 42));
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.refValueHolderVec3.value.z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck;
            float right;

            nullCheck = root.refValueHolderVec3;
            if (nullCheck == null)
            {
                goto retn;
            }
            right = nullCheck.value.z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_ConstIndex_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3Array[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3Array = new[] {
            new Vector3(),
            new Vector3(),
            new Vector3(),
            new Vector3(0, 0, 42)
        };

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3Array[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            UnityEngine.Vector3[] nullCheck;
            float right;

            nullCheck = root.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (3 >= nullCheck.Length)
            {
                goto retn;
            }
            right = nullCheck[3].z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_ArrayNullable() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3Array?[3].z ?? 2f"));

        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();

        root.vec3Array = new[] {
            new Vector3(),
            new Vector3(),
            new Vector3(),
            new Vector3(0, 0, 42)
        };

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3Array[3].z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            float? nullableAccess;
            float right;

            nullableAccess = default(float?);
            if (root.vec3Array != null)
            {
                UnityEngine.Vector3[] toBoundsCheck;

                toBoundsCheck = root.vec3Array;
                if (3 >= toBoundsCheck.Length)
                {
                    goto retn;
                }
                nullableAccess = (float?)toBoundsCheck[3].z;
            }
            right = nullableAccess ?? 2f;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_IListNullable() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3List?[3].z ?? 2f"));

        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();

        root.vec3List = new List<Vector3>(new[] {
            new Vector3(),
            new Vector3(),
            new Vector3(),
            new Vector3(0, 0, 42)
        });

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3List[3].z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            float? nullableAccess;
            float right;

            nullableAccess = default(float?);
            if (root.vec3List != null)
            {
                System.Collections.Generic.List<UnityEngine.Vector3> toBoundsCheck;

                toBoundsCheck = root.vec3List;
                if (3 >= toBoundsCheck.Count)
                {
                    goto retn;
                }
                nullableAccess = (float?)toBoundsCheck[3].z;
            }
            right = nullableAccess ?? 2f;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_IListNullable_NoBoundsCheck() {
        LinqThing root = new LinqThing();

        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(new Parameter<LinqThing>("root", ParameterFlags.NeverNull));

        compiler.SetOutOfBoundsCheckingEnabled(false);
        compiler.Return("root.vec3List?[3].z ?? 2f");

        compiler.Log();
        root.vec3List = new List<Vector3>(new[] {
            new Vector3(),
            new Vector3(),
            new Vector3(),
            new Vector3(0, 0, 42)
        });

        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();
        Assert.AreEqual(42f, fn(root));
        root.vec3List = null;
        Assert.AreEqual(2f, fn(root));

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            float? nullableAccess;

            nullableAccess = default(float?);
            if (root.vec3List != null)
            {
                nullableAccess = (float?)root.vec3List[3].z;
            }
            return nullableAccess ?? 2f;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileIndexAccess_NonListNullable() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3Dic?['two'].z ?? 2f"));

        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();

        root.vec3Dic = new Dictionary<string, Vector3>();
        root.vec3Dic.Add("two", new Vector3(0, 0, 42));

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3Dic["two"].z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
       (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            UnityEngine.Vector3 outVar;
            float? nullableAccess;
            float right;

            nullableAccess = default(float?);
            if (root.vec3Dic != null)
            {
                if (root.vec3Dic.TryGetValue(""two"", out outVar) == true)
                {
                    nullableAccess = (float?)outVar.z;
                }
                else
                {
                    goto retn;
                }
            }
            right = nullableAccess ?? 2f;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_NonArray_ConstIndex_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3List[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));


        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3List[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            System.Collections.Generic.List<UnityEngine.Vector3> nullCheck;
            float right;

            nullCheck = root.vec3List;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (3 >= nullCheck.Count)
            {
                goto retn;
            }
            right = nullCheck[3].z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_StringDictionary_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3Dic['two'].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3Dic = new Dictionary<string, Vector3>();
        root.vec3Dic["one"] = new Vector3(1, 1, 1);
        root.vec3Dic["two"] = new Vector3(2, 2, 2);
        root.vec3Dic["three"] = new Vector3(3, 3, 3);

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(2, root.vec3Dic["two"].z);

        fn.Invoke(root, element);

        Assert.AreEqual(2, element.floatValue);

        root.vec3Dic.Remove("two");

        element.floatValue = 14415;
        fn.Invoke(root, element);

        Assert.AreEqual(14415, element.floatValue);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            UnityEngine.Vector3 outVar;
            float right;
        
            if (root.vec3Dic.TryGetValue(""two"", out outVar) != true)
            {
                goto retn;
            }
            right = outVar.z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_AttemptTypeCast() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.floatValue", "root.vec3List[3f].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3List[3].z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            System.Collections.Generic.List<UnityEngine.Vector3> nullCheck;
            float right;

            nullCheck = root.vec3List;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (3 >= nullCheck.Count)
            {
                goto retn;
            }
            right = nullCheck[3].z;
            if (element.floatValue != right)
            {
                element.floatValue = right;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Constant() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.svHolderVec3.value.x", "34"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();

        Assert.AreEqual(0, element.svHolderVec3.value.x);

        fn.Invoke(root, element);

        Assert.AreEqual(34, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3_assign;
            UnityEngine.Vector3 value_assign;
            float x_assign;

            svHolderVec3_assign = element.svHolderVec3;
            value_assign = svHolderVec3_assign.value;
            x_assign = value_assign.x;
            if (x_assign != 34f)
            {
                value_assign.x = 34f;
                svHolderVec3_assign.value = value_assign;
                element.svHolderVec3 = svHolderVec3_assign;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Variable() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.svHolderVec3.value.x", "root.floatValue"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.floatValue = 35;
        Assert.AreEqual(0, element.svHolderVec3.value.x);

        fn.Invoke(root, element);

        Assert.AreEqual(35, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3_assign;
            UnityEngine.Vector3 value_assign;
            float x_assign;
            float right;

            svHolderVec3_assign = element.svHolderVec3;
            value_assign = svHolderVec3_assign.value;
            x_assign = value_assign.x;
            right = root.floatValue;
            if (x_assign != right)
            {
                value_assign.x = right;
                svHolderVec3_assign.value = value_assign;
                element.svHolderVec3 = svHolderVec3_assign;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Accessor() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("element.svHolderVec3.value.x", "root.vec3List[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));

        Assert.AreEqual(0, element.svHolderVec3.value.x);
        Assert.AreEqual(42, root.vec3List[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3_assign;
            UnityEngine.Vector3 value_assign;
            float x_assign;
            System.Collections.Generic.List<UnityEngine.Vector3> nullCheck;
            float right;

            svHolderVec3_assign = element.svHolderVec3;
            value_assign = svHolderVec3_assign.value;
            x_assign = value_assign.x;
            nullCheck = root.vec3List;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (3 >= nullCheck.Count)
            {
                goto retn;
            }
            right = nullCheck[3].z;
            if (x_assign != right)
            {
                value_assign.x = right;
                svHolderVec3_assign.value = value_assign;
                element.svHolderVec3 = svHolderVec3_assign;
            }
        retn:
            return;
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileNumericOperators() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input, Type type) where T : Delegate {
            compiler.SetSignature(type);
            compiler.Return(input);
            AssertStringsEqual(@"() => 
            {
                return {input};
            }".Replace("{input}", input), compiler.Print());
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual(5 + 4, CompileAndReset<Func<int>>("5 + 4", typeof(int)));
        Assert.AreEqual(5 - 4, CompileAndReset<Func<int>>("5 - 4", typeof(int)));
        Assert.AreEqual(5 * 4, CompileAndReset<Func<int>>("5 * 4", typeof(int)));
        Assert.AreEqual(5 % 4, CompileAndReset<Func<int>>("5 % 4", typeof(int)));
        Assert.AreEqual(5 / 4, CompileAndReset<Func<int>>("5 / 4", typeof(int)));
        Assert.AreEqual(5 >> 4, CompileAndReset<Func<int>>("5 >> 4", typeof(int)));
        Assert.AreEqual(5 << 4, CompileAndReset<Func<int>>("5 << 4", typeof(int)));
        Assert.AreEqual(5 | 4, CompileAndReset<Func<int>>("5 | 4", typeof(int)));
        Assert.AreEqual(5 & 4, CompileAndReset<Func<int>>("5 & 4", typeof(int)));

        Assert.AreEqual(5f + 4f, CompileAndReset<Func<float>>("5f + 4f", typeof(float)));
        Assert.AreEqual(5f - 4f, CompileAndReset<Func<float>>("5f - 4f", typeof(float)));
        Assert.AreEqual(5f * 4f, CompileAndReset<Func<float>>("5f * 4f", typeof(float)));
        Assert.AreEqual(5f % 4f, CompileAndReset<Func<float>>("5f % 4f", typeof(float)));
        Assert.AreEqual(5f / 4f, CompileAndReset<Func<float>>("5f / 4f", typeof(float)));
    }

    [Test]
    public void CompileNumericOperators_MultipleOperators() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input) where T : Delegate {
            compiler.SetSignature(typeof(int));
            compiler.Return(input);
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual(5 + 4 * 7, CompileAndReset<Func<int>>("5 + 4 * 7"));
        Assert.AreEqual(5 - 4 * 7, CompileAndReset<Func<int>>("5 - 4 * 7"));
        Assert.AreEqual(5 * 4 * 7, CompileAndReset<Func<int>>("5 * 4 * 7"));
        Assert.AreEqual(5 % 4 * 7, CompileAndReset<Func<int>>("5 % 4 * 7"));
        Assert.AreEqual(5 / 4 * 7, CompileAndReset<Func<int>>("5 / 4 * 7"));
        Assert.AreEqual(5 >> 4 * 7, CompileAndReset<Func<int>>("5 >> 4 * 7"));
        Assert.AreEqual(5 << 4 * 7, CompileAndReset<Func<int>>("5 << 4 * 7"));
        Assert.AreEqual(5 | 4 * 7, CompileAndReset<Func<int>>("5 | 4 * 7"));
        Assert.AreEqual(5 & 4 * 7, CompileAndReset<Func<int>>("5 & 4 * 7"));
    }

    [Test]
    public void CompileNumericOperators_MultipleOperators_Parens() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input) where T : Delegate {
            compiler.SetSignature<int>();
            compiler.Return(input);
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual((124 + 4) * 7, CompileAndReset<Func<int>>("(124 + 4) * 7"));
        Assert.AreEqual((124 - 4) * 7, CompileAndReset<Func<int>>("(124 - 4) * 7"));
        Assert.AreEqual((124 * 4) * 7, CompileAndReset<Func<int>>("(124 * 4) * 7"));
        Assert.AreEqual((124 % 4) * 7, CompileAndReset<Func<int>>("(124 % 4) * 7"));
        Assert.AreEqual((124 / 4) * 7, CompileAndReset<Func<int>>("(124 / 4) * 7"));
        Assert.AreEqual((124 >> 4) * 7, CompileAndReset<Func<int>>("(124 >> 4) * 7"));
        Assert.AreEqual((124 << 4) * 7, CompileAndReset<Func<int>>("(124 << 4) * 7"));
        Assert.AreEqual((124 | 4) * 7, CompileAndReset<Func<int>>("(124 | 4) * 7"));
        Assert.AreEqual((124 & 4) * 7, CompileAndReset<Func<int>>("(124 & 4) * 7"));

        Assert.AreEqual(124 + (4 * 7), CompileAndReset<Func<int>>("124 + (4 * 7)"));
        Assert.AreEqual(124 - (4 * 7), CompileAndReset<Func<int>>("124 - (4 * 7)"));
        Assert.AreEqual(124 * (4 * 7), CompileAndReset<Func<int>>("124 * (4 * 7)"));
        Assert.AreEqual(124 % (4 * 7), CompileAndReset<Func<int>>("124 % (4 * 7)"));
        Assert.AreEqual(124 / (4 * 7), CompileAndReset<Func<int>>("124 / (4 * 7)"));
        Assert.AreEqual(124 >> (4 * 7), CompileAndReset<Func<int>>("124 >> (4 * 7)"));
        Assert.AreEqual(124 << (4 * 7), CompileAndReset<Func<int>>("124 << (4 * 7)"));
        Assert.AreEqual(124 | (4 * 7), CompileAndReset<Func<int>>("124 | (4 * 7)"));
        Assert.AreEqual(124 & (4 * 7), CompileAndReset<Func<int>>("124 & (4 * 7)"));
    }

    private class OperatorOverloadTest {

        public Vector3 v0;
        public Vector3 v1;

    }

    [Test]
    public void CompileOperatorOverloads() {
        LinqCompiler compiler = new LinqCompiler();

        Func<OperatorOverloadTest, Vector3> CompileAndReset(string input) {
            compiler.SetSignature<Vector3>(new Parameter<OperatorOverloadTest>("opOverload"));
            compiler.Return(input);
            Func<OperatorOverloadTest, Vector3> retn = compiler.Compile<Func<OperatorOverloadTest, Vector3>>();

            compiler.Reset();
            return retn;
        }

        OperatorOverloadTest overloadTest = new OperatorOverloadTest();

        overloadTest.v0 = new Vector3(1124, 522, 241);
        overloadTest.v1 = new Vector3(1124, 522, 241);

        Assert.AreEqual(overloadTest.v0 + overloadTest.v1, CompileAndReset("opOverload.v0 + opOverload.v1")(overloadTest));
        Assert.AreEqual(overloadTest.v0 - overloadTest.v1, CompileAndReset("opOverload.v0 - opOverload.v1")(overloadTest));
        CompileException exception = Assert.Throws<CompileException>(() => { CompileAndReset("opOverload.v0 / opOverload.v1")(overloadTest); });
        Assert.AreEqual(exception.Message, CompileException.MissingBinaryOperator(OperatorType.Divide, typeof(Vector3), typeof(Vector3)).Message);
    }

    [Test]
    public void CompileTypeOfConstant() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Type>();
        compiler.Return("typeof(int)");
        Assert.AreEqual(typeof(int), compiler.Compile<Func<Type>>()());
        compiler.Reset();

        compiler.SetSignature<Type>();
        compiler.Return("typeof(int[])");
        Assert.AreEqual(typeof(int[]), compiler.Compile<Func<Type>>()());
    }

    [Test]
    public void CompileUnaryNot() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<bool>();
        compiler.Return("!true");
        Assert.AreEqual(false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.SetSignature<bool>();
        compiler.Return("!true && false");
        Assert.AreEqual(!true && false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.SetSignature<bool>();
        compiler.Return("!false");
        Assert.AreEqual(!false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.SetSignature<bool>();
        compiler.Return("false && !true");
        Assert.AreEqual(false && !true, compiler.Compile<Func<bool>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileUnaryMinus() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<int>();
        compiler.Return("-10");
        Assert.AreEqual(-10, compiler.Compile<Func<int>>()());
        compiler.Reset();

        compiler.SetSignature<float>();
        compiler.Return("-1425.24f");
        Assert.AreEqual(-1425.24f, compiler.Compile<Func<float>>()());
        compiler.Reset();

        compiler.SetSignature<double>();
        compiler.Return("-1425.24d");
        Assert.AreEqual(-1425.24d, compiler.Compile<Func<double>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileUnaryBitwiseNot() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<int>();
        compiler.Return("~10");
        Assert.AreEqual(~10, compiler.Compile<Func<int>>()());
        compiler.Reset();

        compiler.SetSignature<int>();
        compiler.Return("~(1425 & 4)");
        Assert.AreEqual(~(1425 & 4), compiler.Compile<Func<int>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileIs() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<bool>(new Parameter<LinqThing>("thing", ParameterFlags.NeverNull));
        compiler.SetOutOfBoundsCheckingEnabled(false);
        compiler.SetNullCheckingEnabled(false);
        compiler.AddNamespace("System.Collections.Generic");
        compiler.Return("thing.vec3Array[0].x is System.Collections.Generic.List<float>");
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return thing.vec3Array[0].x is System.Collections.Generic.List<float>;
        }", compiler.Print());
        Assert.AreEqual(false, compiler.Compile<Func<LinqThing, bool>>()(thing));
    }

    [Test]
    public void CompileAs() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.AddNamespace("System.Collections");
        compiler.SetSignature<IList>(new Parameter<LinqThing>("thing", ParameterFlags.NeverNull));
        compiler.Return("thing.vec3Array as IList");
        compiler.Log();
        Assert.AreEqual(thing.vec3Array, compiler.Compile<Func<LinqThing, IList>>()(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return thing.vec3Array as System.Collections.IList;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileDirectCast() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetSignature<IReadOnlyList<Vector3>>(
            new Parameter<LinqThing>("thing", ParameterFlags.NeverNull)
        );
        compiler.AddNamespace("System.Collections.Generic");
        compiler.AddNamespace("UnityEngine");
        compiler.Return("(IReadOnlyList<Vector3>)thing.vec3Array");
        compiler.Log();
        Assert.AreEqual(thing.vec3Array, compiler.Compile<Func<LinqThing, IReadOnlyList<Vector3>>>()(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return (System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3>)thing.vec3Array;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileNewExpression_NoArguments() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<Vector3>();
        compiler.AddNamespace("UnityEngine");
        compiler.Return("new Vector3()");
        Assert.AreEqual(new Vector3(), compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
        () =>
        {
            return new UnityEngine.Vector3();
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileNewExpression_WithConstantArguments() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Vector3>();
        compiler.AddNamespace("UnityEngine");
        compiler.Return("new Vector3(1f, 2f, 3f)");

        Assert.AreEqual(new Vector3(1, 2, 3), compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
        () =>
        {
            return new UnityEngine.Vector3(1f, 2f, 3f);
        }
        ", compiler.Print());
    }


    [Test]
    public void CompileNewExpression_WithOptionalArguments() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<ThingWithOptionals>();
        compiler.Return("new ThingWithOptionals(8)");

        ThingWithOptionals thing = compiler.Compile<Func<ThingWithOptionals>>()();
        Assert.AreEqual(8, thing.x);
        Assert.AreEqual(2, thing.y);
        Assert.AreEqual(0, thing.f);
        AssertStringsEqual(@"
           () =>
            {
                return new ThingWithOptionals(8, 2);
            }
        ", compiler.Print());
    }

    [Test]
    public void CompileNewExpression_WithUnmatchedConstructor() {
        LinqCompiler compiler = new LinqCompiler();


        CompileException ex = Assert.Throws<CompileException>(() => {
            compiler.SetSignature<ThingWithOptionals>();
            compiler.Return("new ThingWithOptionals(8, 10, 20, 24, 52)");
            compiler.Compile<Func<ThingWithOptionals>>()();
        });
        Assert.AreEqual(CompileException.UnresolvedConstructor(typeof(ThingWithOptionals), new Type[] {
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int)
        }).Message, ex.Message);
    }

    [Test]
    public void CompileNewExpression_WithNestedNew() {
        LinqCompiler compiler = new LinqCompiler();


        compiler.SetSignature<ThingWithOptionals>();
        compiler.Return("new ThingWithOptionals(new ThingWithOptionals(12f), 2)");

        ThingWithOptionals thing = compiler.Compile<Func<ThingWithOptionals>>()();
        Assert.AreEqual(0, thing.x);
        Assert.AreEqual(2, thing.y);
        Assert.AreEqual(12, thing.f);
        AssertStringsEqual(@"
           () =>
            {
                return new ThingWithOptionals(new ThingWithOptionals(12f, 2), 2);
            }
        ", compiler.Print());
    }

    [Test]
    public void CompileEnumAccess() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<TestEnum>();
        compiler.Return("TestEnum.One");
        Assert.AreEqual(TestEnum.One, compiler.Compile<Func<TestEnum>>()());
        AssertStringsEqual(@"
        () =>
        {
            return TestEnum.One;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileNamespacePath() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValue");
        compiler.Log();
        Assert.AreEqual(NamespaceTestClass.FloatValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
        () =>
        {
            return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValue;
        }
        ", compiler.Print());

        compiler.Reset();
        compiler.SetSignature<float>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatArray[0]");
        compiler.Log();

        Assert.AreEqual(NamespaceTestClass.FloatArray[0], compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
        () =>
        {
            float retn_val;
            float[] nullCheck;

            retn_val = default(float);
            nullCheck = UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatArray;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (0 >= nullCheck.Length)
            {
                goto retn;
            }
            retn_val = nullCheck[0];
        retn:
            return retn_val;
        }
       ", compiler.Print());
    }

    [Test]
    public void CompileTypeChain() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();
        compiler.AddNamespace("UIForia.Test.NamespaceTest.SomeNamespace");
        compiler.Return("TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue");
        Assert.AreEqual(TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
        () =>
        {
            return UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue;
        }
        ", compiler.Print());

        compiler.Reset();

        compiler.SetSignature<Vector3>();
        compiler.AddNamespace("UIForia.Test.NamespaceTest.SomeNamespace");
        compiler.AddNamespace("UnityEngine");
        compiler.Return("TypeChainTest.TypeChainChild.TypeChainEnd<Vector3>.Value");
        Assert.AreEqual(TypeChainTest.TypeChainChild.TypeChainEnd<Vector3>.Value, compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
        () =>
        {
            return UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd<UnityEngine.Vector3>.Value;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileNamespacePath_NestedType() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1.FloatValue");

        Assert.AreEqual(NamespaceTestClass.SubType1.FloatValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1.FloatValue;
            }
        ", compiler.Print());

        compiler.Reset();
        compiler.SetSignature<int>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string>.IntValue");

        Assert.AreEqual(NamespaceTestClass.SubType1<string>.IntValue, compiler.Compile<Func<int>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string>.IntValue;
            }
        ", compiler.Print());

        compiler.Reset();
        compiler.SetSignature<string>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string, UnityEngine.Vector3>.StringValue");

        Assert.AreEqual(NamespaceTestClass.SubType1<string, Vector3>.StringValue, compiler.Compile<Func<string>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string, UnityEngine.Vector3>.StringValue;
            }
        ", compiler.Print());

        compiler.Reset();
        compiler.SetSignature<int>();
        compiler.Return("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue");

        Assert.AreEqual(NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue, compiler.Compile<Func<int>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue;
            }
        ", compiler.Print());
    }

    [Test]
    public void CompileInstanceMethod_NoArgs() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("root", ParameterFlags.NeverNull));
        compiler.Return("root.GetVectorValue()");
        compiler.Log();
        LinqThing thing = new LinqThing();
        thing.svHolderVec3 = new StructValueHolder<Vector3>(new Vector3(10, 11, 12));
        Assert.AreEqual(thing.GetVectorValue(), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
    }

    [Test]
    public void CompileInstanceMethod_1Arg() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("root"));
        compiler.Return("root.GetVectorValueWith1Arg(1)");
        LinqThing thing = new LinqThing();
        thing.svHolderVec3 = new StructValueHolder<Vector3>(new Vector3(10, 11, 12));
        Assert.AreEqual(thing.GetVectorValueWith1Arg(1), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
    }

    [Test]
    public void CompileInstanceMethod_1Arg_ImplicitConversion() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<Vector3>(new Parameter<LinqThing>("root"));
        compiler.Return("root.GetVectorValueWith1Arg(1f)");
        LinqThing thing = new LinqThing();
        thing.svHolderVec3 = new StructValueHolder<Vector3>(new Vector3(10, 11, 12));
        Assert.AreEqual(thing.GetVectorValueWith1Arg(1), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
    }

    [Test]
    public void CompileInstanceMethod_1Arg_Overload() {
        LinqThing thing = new LinqThing();
        thing.floatValue = 100;
        thing.intVal = 200;
        thing.stringField = null;

        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<float>(new Parameter<LinqThing>("root"));
        compiler.Return("root.OverloadWithValue(root.floatValue)");
        compiler.Log();
        Func<LinqThing, float> fn = compiler.Compile<Func<LinqThing, float>>();

        Assert.AreEqual(thing.floatValue, fn(thing));
        Assert.AreEqual(thing.stringField, "OverloadWithValue_float");

        compiler.Reset();
        compiler.SetSignature<float>(new Parameter<LinqThing>("root"));
        compiler.Return("root.OverloadWithValue(root.intVal)");
        fn = compiler.Compile<Func<LinqThing, float>>();

        Assert.AreEqual(thing.intVal, fn(thing));
        Assert.AreEqual(thing.stringField, "OverloadWithValue_int");

        compiler.Reset();
        compiler.SetSignature<float>(new Parameter<LinqThing>("root"));
        compiler.Return("root.OverloadWithValue()");
        fn = compiler.Compile<Func<LinqThing, float>>();

        Assert.AreEqual(10.02424f, fn(thing));
        Assert.AreEqual(thing.stringField, "OverloadWithValue_float");
    }

    [Test]
    public void CompileStaticProperty_NestedAccess_NoNamespace() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<float>();
        compiler.Return("UnityEngine.Color.red.r");

        AssertStringsEqual(@"
        () =>
        {
            return UnityEngine.Color.red.r;
        }", compiler.Print());
        Assert.AreEqual(1f, compiler.Compile<Func<float>>()());
    }

    [Test]
    public void CompileStaticProperty_NoNamespace() {
        LinqCompiler compiler = new LinqCompiler();

        Color expected = Color.red;
        compiler.SetSignature<Color>();
        compiler.Return("UnityEngine.Color.red");

        AssertStringsEqual(@"
        () =>
        {
            return UnityEngine.Color.red;
        }", compiler.Print());
        Assert.AreEqual(expected, compiler.Compile<Func<Color>>()());
    }

    [Test]
    public void CompileStaticMethodNoNamespace() {
        LinqCompiler compiler = new LinqCompiler();

        Color expected = Color.HSVToRGB(0.5f, 0.5f, 0.5f);
        compiler.SetSignature<Color>();
        compiler.AddNamespace("UnityEngine");
        compiler.Return("Color.HSVToRGB(0.5f, 0.5f, 0.5f)");

        AssertStringsEqual(@"
        () =>
        {
            return UnityEngine.Color.HSVToRGB(0.5f, 0.5f, 0.5f);
        }", compiler.Print());
        Assert.AreEqual(expected, compiler.Compile<Func<Color>>()());
    }

    [Test]
    public void CompileStaticMethodWithNamespace() {
        LinqCompiler compiler = new LinqCompiler();

        Color expected = Color.HSVToRGB(0.5f, 0.5f, 0.5f);
        compiler.SetSignature<Color>();
        compiler.Return("UnityEngine.Color.HSVToRGB(0.5f, 0.5f, 0.5f)");

        AssertStringsEqual(@"
        () =>
        {
            return UnityEngine.Color.HSVToRGB(0.5f, 0.5f, 0.5f);
        }", compiler.Print());

        Assert.AreEqual(expected, compiler.Compile<Func<Color>>()());
    }

    [Test]
    public void CompileTernary_Full() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<bool>();
        compiler.Return("3 > 4 ? true : false");
        compiler.Log();
        AssertStringsEqual(@"
        () =>
        {
            bool ternaryOutput;

            if (3 > 4)
            {
                ternaryOutput = true;
            }
            else
            {
                ternaryOutput = false;
            }
            return ternaryOutput;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileTernary_Partial() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<float>(new Parameter<LinqThing>("thing", ParameterFlags.NeverNull));
        compiler.Return("thing ? thing.floatValue");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            float ternaryOutput;

            if (thing != null)
            {
                ternaryOutput = thing.floatValue;
            }
            else
            {
                ternaryOutput = default(float);
            }
            return ternaryOutput;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileTernary_NestedFull() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<int>(new Parameter<LinqThing>("thing"));
        compiler.SetNullCheckingEnabled(false);
        compiler.Return("thing != null ? thing.floatValue > 5 ? 1 : 0 : 10");

        var thing = new LinqThing();

        compiler.Log();
        Func<LinqThing, int> fn = compiler.Compile<Func<LinqThing, int>>();
        thing.floatValue = 10;
        Assert.AreEqual(1, fn(thing));
        thing.floatValue = 1;
        Assert.AreEqual(0, fn(thing));

        Assert.AreEqual(10, fn(null));

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            int ternaryOutput;

            if (thing != null)
            {
                int ternaryOutput_0;

                if (thing.floatValue > (float)5)
                {
                    ternaryOutput_0 = 1;
                }
                else
                {
                    ternaryOutput_0 = 0;
                }
                ternaryOutput = ternaryOutput_0;
            }
            else
            {
                ternaryOutput = 10;
            }
            return ternaryOutput;
        }
        ", compiler.Print());
    }

    [Test]
    public void CompileTernary_NestedPartial() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<int>(new Parameter<LinqThing>("thing"));
        compiler.Return("thing ? thing.floatValue > 5 ? 1");
        compiler.Log();
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            int ternaryOutput;

            if (thing != null)
            {
                int ternaryOutput_0;

                if (thing.floatValue > (float)5)
                {
                    ternaryOutput_0 = 1;
                }
                else
                {
                    ternaryOutput_0 = default(int);
                }
                ternaryOutput = ternaryOutput_0;
            }
            else
            {
                ternaryOutput = default(int);
            }
            return ternaryOutput;
        }
        ", compiler.Print());
    }


    // todo -- test bad enum values
    // todo -- test bad constant values
    // todo -- test missing fields & properties
    // todo -- test missing type paths
    // todo -- test valid type path with invalid generic
    // todo -- test non public fields
    // todo -- test non public properties
    // todo -- test non public static fields & properties
    // todo -- test list initializer
    // todo -- test splat operator
    // todo -- test alias identifiers
    // todo -- test alias methods
    // todo -- test alias indexers
    // todo -- test alias constructors
    // todo -- test alias splat
    // todo -- test alias list initializer
    // todo -- test initializer syntax { x: 4 }
    // todo -- test out of bounds handler
    // todo -- test elvis with objects
    // todo -- test elvis with methods call chains
    // todo -- expressions should not be null checked again unless assigned to after last null check

    [Test]
    public void CompileListInitializer() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<int[]>();

        compiler.Return("[1, 2, 3]");

        // var x = new ValueContainer<T>();
        // ...props (if type and name match for input object, pass to target assignment, for public & non-readonly properties & fields)
    }

    [Test]
    public void CompileObjectInitializer() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature<int[]>();
        compiler.Return("{x = 4, y = 13, z = 'str'}");
    }

    [Test]
    public void CompileMethodCallChain_NoArgs() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<LinqThing>(
            new Parameter<LinqThing>("thing")
        );

        compiler.Return("thing.Method().self.Method().Method()");
        compiler.Log();

        Func<LinqThing, LinqThing> fn = compiler.Compile<Func<LinqThing, LinqThing>>();

        LinqThing thing = new LinqThing();

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            TestLinqCompiler.LinqThing retn_val;
            TestLinqCompiler.LinqThing nullCheck;
            TestLinqCompiler.LinqThing nullCheck_0;
            TestLinqCompiler.LinqThing nullCheck_1;

            retn_val = default(TestLinqCompiler.LinqThing);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.Method();
            if (nullCheck == null)
            {
                goto retn;
            }
            nullCheck_0 = nullCheck.self;
            if (nullCheck_0 == null)
            {
                goto retn;
            }
            nullCheck_1 = nullCheck_0.Method();
            if (nullCheck_1 == null)
            {
                goto retn;
            }
            retn_val = nullCheck_1.Method();
        retn:
            return retn_val;
        }
        ", compiler.Print());
        fn(thing);

        Assert.AreEqual(3, thing.methodCallCount);
    }

    [Test]
    public void CompileMethodCallChain_Args() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<LinqThing>(
            new Parameter<LinqThing>("thing")
        );

        compiler.Return("thing.Method(1).Method().Method(1, 2).Method(1, 2, 3)");

        compiler.Log();

        Func<LinqThing, LinqThing> fn = compiler.Compile<Func<LinqThing, LinqThing>>();

        LinqThing thing = new LinqThing();

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            TestLinqCompiler.LinqThing retn_val;
            TestLinqCompiler.LinqThing nullCheck;
            TestLinqCompiler.LinqThing nullCheck_0;
            TestLinqCompiler.LinqThing nullCheck_1;

            retn_val = default(TestLinqCompiler.LinqThing);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.Method(1);
            if (nullCheck == null)
            {
                goto retn;
            }
            nullCheck_0 = nullCheck.Method();
            if (nullCheck_0 == null)
            {
                goto retn;
            }
            nullCheck_1 = nullCheck_0.Method(1, 2);
            if (nullCheck_1 == null)
            {
                goto retn;
            }
            retn_val = nullCheck_1.Method(1, 2, 3);
        retn:
            return retn_val;
        }
        ", compiler.Print());
        fn(thing);

        Assert.AreEqual(4, thing.methodCallCount);
    }

    [Test]
    public void CompileMethodCallChain_Nested() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<LinqThing>(new Parameter<LinqThing>("thing"));
        compiler.Return("thing.Method(thing.GetIntValue(thing.vec3Array[0].y))");

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            TestLinqCompiler.LinqThing retn_val;
            UnityEngine.Vector3[] nullCheck;

            retn_val = default(TestLinqCompiler.LinqThing);
            if (thing == null)
            {
                goto retn;
            }
            nullCheck = thing.vec3Array;
            if (nullCheck == null)
            {
                goto retn;
            }
            if (0 >= nullCheck.Length)
            {
                goto retn;
            }
            retn_val = thing.Method(thing.GetIntValue(nullCheck[0].y));
        retn:
            return retn_val;
        }
        ", compiler.Print());
    }


    [Test]
    public void CompileChained_Invoke() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<int>(
            new Parameter<LinqThing>("thing", ParameterFlags.NeverNull)
        );

        LinqThing thing = new LinqThing();
        compiler.Return("thing.FnReturningFunction()(123)");
        compiler.Log();

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            int retn_val;
            System.Func<int, int> nullCheck;

            retn_val = default(int);
            nullCheck = thing.FnReturningFunction();
            if (nullCheck == null)
            {
                goto retn;
            }
            retn_val = nullCheck(123);
        retn:
            return retn_val;
        }
        ", compiler.Print());

        Func<LinqThing, int> fn = compiler.Compile<Func<LinqThing, int>>();
        Assert.AreEqual(123, fn(thing));
    }

    [Test]
    public void CompileChained_Index() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetSignature<int>(
            new Parameter<LinqThing>("thing", ParameterFlags.NeverNull)
        );

        LinqThing thing = new LinqThing();
        compiler.SetNullCheckingEnabled(false);
        compiler.Return("thing.indexable[123456][2]");
        compiler.Log();

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return (int)thing.indexable[123456][2];
        }
        ", compiler.Print());

        Func<LinqThing, int> fn = compiler.Compile<Func<LinqThing, int>>();
        Assert.AreEqual((int) thing.indexable[123456][2], fn(thing));
    }

    public class StaticThing {

        public static int value;

        public static void Increment() {
            value++;
        }

    }

    [Test]
    public void CompileStatement_StaticMethodCall() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetSignature();
        StaticThing.value = 10;
        compiler.Statement("TestLinqCompiler.StaticThing.Increment()");
        compiler.Log();
        Action fn = compiler.Compile<Action>();
        fn();
        Assert.AreEqual(11, StaticThing.value);
        AssertStringsEqual(@"
        () =>
        {
            TestLinqCompiler.StaticThing.Increment();
        }", compiler.Print());
    }

    public void AssertStringsEqual(string a, string b) {
        string[] splitA = a.Trim().Split('\n');
        string[] splitB = b.Trim().Split('\n');

        Assert.AreEqual(splitA.Length, splitB.Length);

        for (int i = 0; i < splitA.Length; i++) {
            Assert.AreEqual(splitA[i].Trim(), splitB[i].Trim());
        }
    }

    private static string PrintCode(IList<Expression> expressions) {
        string retn = "";
        for (int i = 0; i < expressions.Count; i++) {
            retn += expressions[i].ToCSharpCode();
            if (i != expressions.Count - 1) {
                retn += "\n";
            }
        }

        return retn;
    }

    private static string PrintCode(Expression expression) {
        return expression.ToCSharpCode();
    }

}

namespace UIForia.Test.NamespaceTest.SomeNamespace {

    public class TypeChainTest {

        public class TypeChainChild {

            public class TypeChainEnd {

                public static float SomeValue = 123;

            }

            public class TypeChainEnd<T> {

                public static T Value { get; set; }

            }

            public class TypeChainEnd<T, U> { }

        }

    }

    public class NamespaceTestClass {

        public static float FloatValue = 1;
        public const float FloatValueConst = 142;
        public static float[] FloatArray = {1};

        public class SubType1 {

            public static float FloatValue = 2;

        }

        public class SubType1<T> {

            public static int IntValue;
            public static float FloatValue = 2;

            public class NestedSubType1<TNested> {

                public static int NestedIntValue = 3;

            }

        }

        public class SubType1<T, U> {

            public static string StringValue = "hello";
            public static float FloatValue = 2;

        }

    }

}

public enum TestEnum {

    One,
    Two

}

public class ThingWithOptionals {

    public readonly int x;
    public readonly int y;
    public readonly float f;

    public ThingWithOptionals(ThingWithOptionals other, int y = 2) {
        this.f = other.f;
        this.x = other.x;
        this.y = y;
    }

    public ThingWithOptionals(float f, int y = 2) {
        this.f = f;
        this.y = y;
    }

    public ThingWithOptionals(int x, int y = 2) {
        this.x = x;
        this.y = y;
    }

}
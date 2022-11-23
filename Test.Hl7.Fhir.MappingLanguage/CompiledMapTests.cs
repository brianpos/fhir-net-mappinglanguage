using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq.Expressions;
using System;
using System.Xml.Serialization;
using Expression = System.Linq.Expressions.Expression;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Utility;
using Hl7.Fhir.FhirPath;
using System.Diagnostics;
using System.Data.SqlTypes;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

namespace Test.FhirMappingLanguage
{
    [TestClass]
    public class CompiledMapTests
    {
        FhirXmlSerializationSettings _settings = new FhirXmlSerializationSettings() { Pretty = true };
        internal static StructureMapUtilitiesAnalyze.IWorkerContext CreateWorker()
        {
            var source = new CachedResolver(new MultiResolver(
                new DirectorySource(@"c:\temp\analyzetests"),
                ZipSource.CreateValidationSource()
                ));
            source.Load += Source_Load;
            var worker = new TestWorker(source);
            return worker;
        }

        private static void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            // This is a hack/workaround to make the structure definition from the tutorials work
            // with the Firely serialization engine
            if (e.Resource is StructureDefinition sd)
            {
                sd.Abstract = false;
                if (sd.Snapshot == null)
                {
                    sd.Snapshot = new StructureDefinition.SnapshotComponent();
                    sd.Snapshot.Element.AddRange(sd.Differential.Element);
                }
            }
        }

        [TestMethod]
        public void TestFhirPathNativeExpressionVisitor()
        {
            // string fhirpath = "name[0]\n.given[0]";
            string fhirpath = "name.where(family='Pos')\n.given[1]";
            // string fhirpath = "name[0]\n.given[0] & ' - smile'";
            // string fhirpath = "deceased";
            // string fhirpath = "gender = 'male'";
            var pat = new Patient()
            {
                Id = "pat1",
                Gender = AdministrativeGender.Male,
                BirthDate = "1970-01-01",
                Deceased = new FhirBoolean(false)
            };
            pat.Name.Add(new HumanName().WithGiven("Brian").AndFamily("Pos"));
            pat.Name[0].GivenElement.Add(new FhirString("R"));
            pat.Name.Add(new HumanName().WithGiven("Maiden").AndFamily("Postlethwaite"));
          
            // this is the linq version
            var v = pat.Name.Where(n => n.Family == "Pos").SelectMany(v => v.GivenElement.Skip(1).First());
            v = pat.Name.Where(val => (val.FamilyElement.Value == "Pos")).SelectMany(val => val.GivenElement).Skip(1).First();

            System.Diagnostics.Trace.WriteLine($"---");
            foreach (var val in v)
                System.Diagnostics.Trace.WriteLine($"{val.Value}");
            System.Diagnostics.Trace.WriteLine($"---");

            var fpc = new FhirPathCompiler();
            var ex = fpc.Parse(fhirpath);
            var e = fpc.Compile(fhirpath);
            var result = e(pat.ToTypedElement(), new EvaluationContext());
            System.Diagnostics.Trace.WriteLine($"{ex.Dump()}");
            System.Diagnostics.Trace.WriteLine($"---");
            foreach (var val in result)
                System.Diagnostics.Trace.WriteLine($"{val.Value}");
            System.Diagnostics.Trace.WriteLine($"---");

            FHIRPathEngineOriginal fpe = new FHIRPathEngineOriginal(null);
            var fo = fpe.parse(fhirpath);
            Console.WriteLine(fo.Canonical());

            // Remap to a Linq style Expression direct on the object model!
            var ctx = Expression.Parameter(pat.GetType(), "%resource");
            var ce = RemapNative(ex, ctx);
            Console.WriteLine($"{ce}");
            if (ce.CanReduce)
                ce = ce.ReduceAndCheck();
            var q = Expression.Lambda<Func<Patient, object>>(ce, ctx);
            var eval = q.Compile();
            var fe = eval(pat);
            System.Diagnostics.Trace.WriteLine($"---");
            //if (fe is IEnumerable)
            //{
            //    foreach (var val in (IEnumerable)fe)
            //        System.Diagnostics.Trace.WriteLine($"{val}");
            //}
            //else
                System.Diagnostics.Trace.WriteLine($"{fe}");
            System.Diagnostics.Trace.WriteLine($"---");

            // Now lets do some performance testing
            var sw = new Stopwatch();
            int count = 10000;
            sw.Start();
            for (int i = 0; i < count; i++)
                pat.Select(fhirpath);
            sw.Stop();
            Console.WriteLine($"Regular Firely fhirpath: {sw.ElapsedMilliseconds}ms");

            var te = pat.ToTypedElement();
            sw.Restart();
            for (int i = 0; i < count; i++)
                e(te, new EvaluationContext());
            sw.Stop();
            Console.WriteLine($"Compiled Firely fhirpath: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            for (int i = 0; i < count; i++)
                eval(pat);
            sw.Stop();
            Console.WriteLine($"Brian's Linq compiled fhirpath: {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void TestFhirPathITypedElementExpressionVisitor()
        {
            string fhirpath = "name[0]\n.given[0]";
            // string fhirpath = "name[0]\n.given[0] & ' - smile'";
            // string fhirpath = "deceased";
            var pat = new Patient()
            {
                Id = "pat1",
                Gender = AdministrativeGender.Male,
                BirthDate = "1970-01-01",
                Deceased = new FhirBoolean(false)
            };
            pat.Name.Add(new HumanName().WithGiven("Brian").AndFamily("Pos"));
            pat.Name[0].GivenElement.Add(new FhirString("R"));
            pat.Name.Add(new HumanName().WithGiven("Maiden").AndFamily("Postlethwaite"));

            var fpc = new FhirPathCompiler();
            var ex = fpc.Parse(fhirpath);
            var e = fpc.Compile(fhirpath);
            var result = e(pat.ToTypedElement(), new EvaluationContext());
            System.Diagnostics.Trace.WriteLine($"{ex.Dump()}");
            System.Diagnostics.Trace.WriteLine($"{result}");

            FHIRPathEngineOriginal fpe = new FHIRPathEngineOriginal(null);
            var fo = fpe.parse(fhirpath);
            Console.WriteLine(fo.Canonical());

            // Remap to a Linq style Expression direct on the object model!
            var ctx = Expression.Parameter(pat.GetType(), "%resource");
            var ce = RemapITypedElement(ex, ctx);
            Console.WriteLine($"{ce}");
            if (ce.CanReduce)
                ce = ce.ReduceAndCheck();
            var q = Expression.Lambda<Func<Patient, object>>(ce, ctx);
            var eval = q.Compile();
            var fe = eval(pat);
            System.Diagnostics.Trace.WriteLine($"{fe}");

            // Now lets do some performance testing
            var sw = new Stopwatch();
            int count = 10000;
            sw.Start();
            for (int i = 0; i < count; i++)
                pat.Select(fhirpath);
            sw.Stop();
            Console.WriteLine($"Regular Firely fhirpath: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            for (int i = 0; i < count; i++)
                e(pat.ToTypedElement(), new EvaluationContext());
            sw.Stop();
            Console.WriteLine($"Compiled Firely fhirpath: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            for (int i = 0; i < count; i++)
                eval(pat);
            sw.Stop();
            Console.WriteLine($"Brian's Linq compiled fhirpath: {sw.ElapsedMilliseconds}ms");
        }

        static Expression RemapITypedElement(global::Hl7.FhirPath.Expressions.Expression input, Expression context = null)
        {
            if (input is global::Hl7.FhirPath.Expressions.ConstantExpression ce)
            {
                return Expression.Constant(ce.Value);
            }
            if (input is global::Hl7.FhirPath.Expressions.FunctionCallExpression func)
            {
                var focus = RemapITypedElement(func.Focus, context);
                switch (func)
                {
                    case ChildExpression:
                        break;
                }
                if (func is global::Hl7.FhirPath.Expressions.ChildExpression child)
                {
                    if (ClassMapping.TryGetMappingForType(focus.Type, FhirRelease.R4B, out var cm))
                    {
                        // System.Diagnostics.Trace.WriteLine($"{cm.Name} seeking {child.ChildName}");
                        var n = cm.FindMappedElementByName(child.ChildName);
                        if (n != null)
                        {
                            // System.Diagnostics.Trace.WriteLine($"{n.Name} at {n.NativeProperty.Name}");
                            return Expression.PropertyOrField(focus, n.NativeProperty.Name);
                        }
                    }
                }
                if (input is global::Hl7.FhirPath.Expressions.IndexerExpression ie)
                {
                    if (focus.Type.IsArray)
                        return Expression.ArrayIndex(focus, RemapITypedElement(ie.Index, focus));
                    var mi = focus.Type.GetMethod("get_Item");
                    if (mi != null)
                        return Expression.Call(focus, mi, RemapITypedElement(ie.Index, focus));
                    // There was no item getter function, so check if there is an iterator, then use the linq Skip().first()
                    mi = focus.Type.GetMethod("GetEnumerator");
                    if (mi != null)
                        return Expression.Call(focus, mi, RemapITypedElement(ie.Index, focus));
                    // otherwise let it fall through
                    System.Diagnostics.Trace.WriteLine($"Cannot handle indexer on {focus.Type.Name}");
                    return null;
                }
            }
            if (input is global::Hl7.FhirPath.Expressions.AxisExpression ae)
            {
                if (ae.Name == AxisExpression.That.Name)
                    return context;
            }
            //if (input is global::Hl7.FhirPath.Expressions.BinaryExpression be)
            //{
            //    var focus = RemapITypedElement(be.Focus, context);
            //    var left = RemapITypedElement(be.Left, focus);
            //    var right = RemapITypedElement(be.Right, focus);
            //    if (be.Op == "&") // string concatenation
            //    {
            //        return context;
            //    }
            //}
            System.Diagnostics.Trace.WriteLine($"Missing type {input.GetType().Name}");
            return null;
        }

        static Expression RemapNative(global::Hl7.FhirPath.Expressions.Expression input, Expression context = null)
        {
            if (input is global::Hl7.FhirPath.Expressions.ConstantExpression ce)
            {
                return Expression.Constant(ce.Value);
            }
            if (input is global::Hl7.FhirPath.Expressions.FunctionCallExpression func)
            {
                var focus = RemapNative(func.Focus, context);
                if (input is global::Hl7.FhirPath.Expressions.IndexerExpression ie)
                {
                    if (focus.Type.IsArray)
                        return Expression.ArrayIndex(focus, RemapNative(ie.Index, focus));
                    var mi = focus.Type.GetMethod("get_Item");
                    if (mi != null)
                        return Expression.Call(focus, mi, RemapNative(ie.Index, focus));
                    // There was no item getter function, so check if there is an iterator, then use the linq Skip(n).first()
                    mi = focus.Type.GetMethod("GetEnumerator");
                    if (mi != null)
                    {
                        var index = RemapNative(ie.Index, focus);
                        mi = typeof(System.Linq.Enumerable).GetMethods().Where(m => m.Name == "Skip").First();
                        mi = mi.MakeGenericMethod(focus.Type.GenericTypeArguments.First());
                        var mi2 = typeof(System.Linq.Enumerable).GetMethods().Where(m => m.Name == "First").First();
                        mi2 = mi2.MakeGenericMethod(focus.Type.GenericTypeArguments.First());
                        if (index is System.Linq.Expressions.ConstantExpression ce2 && ce2.Value.Equals(0))
                            return Expression.Call(mi2, focus);
                        // Use skip!
                        return Expression.Call(mi2, Expression.Call(mi, focus, index));
                    }
                    // otherwise let it fall through
                    System.Diagnostics.Trace.WriteLine($"Cannot handle indexer on {focus.Type.Name}");
                    return null;
                }
                if (func.FunctionName == "where")
                {
                    // create a linq where clause!
                    // public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
                    var mi = typeof(System.Linq.Enumerable).GetMethods().Where(m => m.Name == "Where").First();
                    mi = mi.MakeGenericMethod(focus.Type.GenericTypeArguments.First());
                    var predSource = Expression.Variable(focus.Type.GenericTypeArguments.First(), "val");

                    // TODO: get the predicate
                    var predicate = RemapNative(func.Arguments.First(), predSource);
                    var exp = Expression.Lambda(predicate, predSource);

                    return Expression.Call(mi, focus, exp);
                }
                var realFocus = focus;
                if (focus.Type.IsArray || ((focus.Type.Name == "List`1" || focus.Type.Name == "IEnumerable`1") && func.FunctionName != "where"))
                {
                    focus = Expression.Variable(focus.Type.GenericTypeArguments.First(), "val");
                }
                Expression resultExpr = null;
                if (func is global::Hl7.FhirPath.Expressions.ChildExpression child)
                {
                    if (ClassMapping.TryGetMappingForType(focus.Type, FhirRelease.R4B, out var cm))
                    {
                        System.Diagnostics.Trace.WriteLine($"{cm.Name} seeking {child.ChildName}");
                        var n = cm.FindMappedElementByName(child.ChildName);
                        if (n != null)
                        {
                            System.Diagnostics.Trace.WriteLine($"{n.Name} at {n.NativeProperty.Name}");
                            resultExpr = Expression.PropertyOrField(focus, n.NativeProperty.Name);
                        }
                    }
                }

                if (resultExpr != null)
                {
                    if (focus != realFocus)
                    {
                        // this type can be selected over
                        // public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
                        var mi = typeof(System.Linq.Enumerable).GetMethods().Where(m => m.Name == "SelectMany").First();
                        mi = mi.MakeGenericMethod(focus.Type, resultExpr.Type.GenericTypeArguments.FirstOrDefault() ?? resultExpr.Type);

                        var exp = Expression.Lambda(resultExpr, focus as ParameterExpression);
                        return Expression.Call(mi, realFocus, exp);
                    }
                    return resultExpr;
                }
            }
            if (input is global::Hl7.FhirPath.Expressions.AxisExpression ae)
            {
                if (ae.Name == AxisExpression.That.Name)
                    return context;
            }
            if (input is global::Hl7.FhirPath.Expressions.BinaryExpression be)
            {
                var focus = RemapNative(be.Focus, context);
                var left = RemapNative(be.Arguments.First(), focus);
                var right = RemapNative(be.Arguments.Skip(1).First(), focus);
                if (be.Op == "=") // equality operator
                {
                    if (left.Type == right.Type)
                        return Expression.Equal(left, right, true, null);
                    if ((left.Type.BaseType.Name == "PrimitiveType" || right.Type.BaseType.Name == "PrimitiveType")
                        && (left.Type.Namespace == "System" || right.Type.Namespace == "System"))
                    {
                        if (left.Type.BaseType.Name == "PrimitiveType")
                            left = ConvertToType(Expression.PropertyOrField(left, "ObjectValue"), right.Type);
                        if (right.Type.BaseType.Name == "PrimitiveType")
                            right = ConvertToType(Expression.PropertyOrField(right, "ObjectValue"), left.Type);
                        return Expression.Equal(left, right, true, null);
                    }
                }
            }
            System.Diagnostics.Trace.WriteLine($"Missing type {input.GetType().Name}");
            return null;
        }

        static Expression ConvertToType(Expression expr, Type t)
        {
            if (expr.Type == t)
                return expr;
            return Expression.Convert(expr, t);
        }

        [TestMethod]
        public void SimpleBlockExpression()
        {
            var varTest = BlockExpression.Variable(typeof(double), "test");
            var varAssignTest = Expression.Assign(varTest, Expression.Constant(2.1));
            var paramPower = Expression.Parameter(typeof(double), "power");
            var retExpr = Expression.Power(varTest, paramPower);
            var parameters = new[] { paramPower };
            var block = Expression.Block(typeof(double), new[] { varTest }, new Expression[] { varAssignTest, retExpr });
            var compiledBlock = Expression.Lambda<Func<double, double>>(block, parameters).Compile();
            var result = compiledBlock(2);
            Console.WriteLine(result);
            result = compiledBlock(3);
            Console.WriteLine(result);
        }

        [TestMethod]
        public void CompilePatientMap()
        {
            var mapText = @"
            // Title of this map,Author
            map ""http://github.com/FHIR/fhir-test-cases/r5/fml/syntax"" = ""syntax""

            uses ""http://hl7.org/fhir/StructureDefinition/Patient"" alias Patient as source // Source Documentation
            uses ""http://hl7.org/fhir/StructureDefinition/Basic"" alias Basic as target // Target Documentation

            // Groups
            // rule for patient group
            group Patient(source src : Patient, target tgt : Basic) {
              // Comment to rule
              src -> tgt.extension as ext, ext.value = create('Reference') as reference, reference.reference = reference(src) ""value"";
              // Copy identifier short syntax
              src.identifier -> tgt.identifier;
              // FHIR Path expression
              // ('urn:uuid:' + r.lower())
              src -> tgt.identifier as ext, ext.system = ('urn:uuid:' + r.lower()) ""rootuuid"";
            }

            // Patient2Patient ropt
            group Patient2Patient(source src : Patient, target tgt : Patient) {
	            src.identifier -> tgt.identifier;
            }";
            var pat = new Patient()
            {
                Id = "42",
                Gender = AdministrativeGender.Female
            };
            pat.Name.Add(new HumanName().WithGiven("Jane").AndFamily("Doe"));

            var parser = new StructureMapUtilitiesParse();
            var sm = parser.parse(mapText, null);
            var worker = CreateWorker();
            var provider = new PocoStructureDefinitionSummaryProvider();
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);
            var target = engine.GenerateEmptyTargetOutputStructure(sm);

            try
            {
                engine.transform(null, pat.ToTypedElement(), sm, target);
                System.Diagnostics.Trace.WriteLine(target.ToXml(_settings));

                // var c = engine.compile(sm);
                // var o2 = c(pat.ToTypedElement());
                // System.Diagnostics.Trace.WriteLine(o2.ToXml(_settings));
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }
    }
}
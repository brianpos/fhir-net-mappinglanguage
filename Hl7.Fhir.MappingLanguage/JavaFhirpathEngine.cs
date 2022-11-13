/*
  Copyright (c) 2011+, HL7, Inc.
  All rights reserved.

  Redistribution and use in source and binary forms, with or without modification,
  are permitted provided that the following conditions are met:

   * Redistributions of source code must retain the above copyright notice, this
     list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above copyright notice,
     this list of conditions and the following disclaimer in the documentation
     and/or other materials provided with the distribution.
   * Neither the name of HL7 nor the names of its contributors may be used to
     endorse or promote products derived from this software without specific
     prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.

*/

// Port (partial) from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/utils/FHIRPathEngine.java
// The execution engine was not ported (will use the Firely SDK version)

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static Hl7.Fhir.MappingLanguage.ExpressionNode;
using static Hl7.Fhir.MappingLanguage.FHIRPathEngineOriginal;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.MappingLanguage.TypeDetails;
using static Hl7.Fhir.Model.ElementDefinition;

namespace Hl7.Fhir.MappingLanguage
{
    internal class FHIRPathEngine
    {
        FHIRPathEngineOriginal _engine;
        public FHIRPathEngine()
        {
            _engine = new FHIRPathEngineOriginal(null);
        }

        public FHIRPathEngine(IWorkerContext worker)
        {
            _engine = new FHIRPathEngineOriginal(worker);
        }

        internal ExpressionNode parse(FHIRLexer lexer)
        {
            _engine.setHostServices(new DotnetFhirPathEngineEnvironment());
            return _engine.parse(lexer);
        }

        public ExpressionNode parse(String path)
        {
            _engine.setHostServices(new DotnetFhirPathEngineEnvironment());
            return _engine.parse(path);
        }

        public ExpressionNode parse(String path, String name)
        {
            _engine.setHostServices(new DotnetFhirPathEngineEnvironment());
            return _engine.parse(path, name);
        }

        public IEvaluationContext getHostServices()
        {
            return _engine.getHostServices();
        }


        public void setHostServices(IEvaluationContext constantResolver)
        {
            _engine.setHostServices(constantResolver);
        }

        internal TypeDetails check(VariablesForProfiling vars, object value, ExpressionNode expr)
        {
            // TODO: BRIAN recheck the params here, quite the disconnect
            return _engine.check(vars, null, null, expr);
        }

        internal string evaluateToString(Variables vars, object value1, object value2, ITypedElement data, ExpressionNode expr)
        {
            var st = new SymbolTable(FhirPathCompiler.DefaultSymbolTable);
            foreach (var variable in vars.All())
            {
                st.AddVar(variable.Name, variable.getObject());
            }
            FhirPathCompiler fpc = new FhirPathCompiler(st);
            var exprCompiled = fpc.Compile(expr.ToString());
            var results = exprCompiled(data, new FhirEvaluationContext());
            if (!results.Any())
                return null;
            if (results.First().Value is string str)
                return str;
            if (results.First().Value is FhirString fstr)
                return fstr.Value;
            // TODO: BRIAN Should this throw if you don't get what you expect?
            return null;
        }

        internal bool evaluateToBoolean(Variables vars, object value1, object value2, ITypedElement data, ExpressionNode expr)
        {
            var st = new SymbolTable(FhirPathCompiler.DefaultSymbolTable);
            foreach (var variable in vars.All())
            {
                st.AddVar(variable.Name, variable.getObject());
            }
            FhirPathCompiler fpc = new FhirPathCompiler(st);
            var exprCompiled = fpc.Compile(expr.ToString());
            var result = exprCompiled.Predicate(data, new FhirEvaluationContext());
            return result;
        }

        internal IEnumerable<ITypedElement> evaluate(Variables vars, object value1, object value2, ITypedElement data, ExpressionNode expr)
        {
            var st = new SymbolTable(FhirPathCompiler.DefaultSymbolTable);
            foreach (var variable in vars.All())
            {
                st.AddVar(variable.Name, variable.getObject());
            }
            FhirPathCompiler fpc = new FhirPathCompiler(st);
            var exprCompiled = fpc.Compile(expr.ToString());
            var results = exprCompiled(data, new FhirEvaluationContext());
            return results;
        }
    }

    /**
     *
     * @author Grahame Grieve
     *
     */
    public class FHIRPathEngineOriginal
    {
        public const String NS_SYSTEM_TYPE = "http://hl7.org/fhirpath/System.";
        private enum Equality { Null, True, False }

        /// <summary>
        /// This is just a typing for the types with prefix % (varaible) and @
        /// </summary>
        internal class FHIRConstant : FhirString
        {
            public FHIRConstant(string value)
                : base(value)
            {
            }
        }

        private IEvaluationContext hostServices;
        private IWorkerContext worker;
        private StringBuilder _log = new StringBuilder();

        public class FunctionDetails
        {
            private String description;
            private int minParameters;
            private int maxParameters;
            public FunctionDetails(String description, int minParameters, int maxParameters)
            {
                this.description = description;
                this.minParameters = minParameters;
                this.maxParameters = maxParameters;
            }
            public String getDescription()
            {
                return description;
            }
            public int getMinParameters()
            {
                return minParameters;
            }
            public int getMaxParameters()
            {
                return maxParameters;
            }

        }

        // if the fhir path expressions are allowed to use constants beyond those defined in the specification
        // the application can implement them by providing a constant resolver
        public interface IEvaluationContext
        {
            /**
             * A constant reference - e.g. a reference to a name that must be resolved in context.
             * The % will be removed from the constant name before this is invoked.
             *
             * This will also be called if the host invokes the FluentPath engine with a context of null
             *
             * @param appContext - content passed into the fluent path engine
             * @param name - name reference to resolve
             * @param beforeContext - whether this is being called before the name is resolved locally, or not
             * @return the value of the reference (or null, if it's not valid, though can throw an exception if desired)
             */
            public List<ITypedElement> resolveConstant(Object appContext, String name, bool beforeContext);
            public TypeDetails resolveConstantType(Object appContext, String name);

            /**
             * when the .log() function is called
             *
             * @param argument
             * @param focus
             * @return
             */
            public bool log(String argument, List<Base> focus);

            // extensibility for functions
            /**
             *
             * @param functionName
             * @return null if the function is not known
             */
            public FunctionDetails resolveFunction(String functionName);

            /**
             * Check the function parameters, and throw an error if they are incorrect, or return the type for the function
             * @param functionName
             * @param parameters
             * @return
             */
            public TypeDetails checkFunction(Object appContext, String functionName, List<TypeDetails> parameters);

            /**
             * @param appContext
             * @param functionName
             * @param parameters
             * @return
             */
            public List<Base> executeFunction(Object appContext, List<Base> focus, String functionName, List<List<Base>> parameters);

            /**
             * Implementation of resolve() function. Passed a string, return matching resource, if one is known - else null
             * @appContext - passed in by the host to the FHIRPathEngine
             * @param url the reference (Reference.reference or the value of the canonical
             * @return
             * @throws FHIRException
             */
            public Base resolveReference(Object appContext, String url);

            public bool conformsToProfile(Object appContext, Base item, String url);

            /*
             * return the value set referenced by the url, which has been used in memberOf()
             */
            public ValueSet resolveValueSet(Object appContext, String url);
        }


        public FHIRPathEngineOriginal(IWorkerContext worker)
        {
            this.worker = worker;
        }


        // --- 3 methods to override in children -------------------------------------------------------
        // if you don't override, it falls through to the using the base reference implementation
        // HAPI overrides to these to support extending the base model

        public IEvaluationContext getHostServices()
        {
            return hostServices;
        }


        public void setHostServices(IEvaluationContext constantResolver)
        {
            this.hostServices = constantResolver;
        }


        // --- public API -------------------------------------------------------
        /**
         * Parse a path for later use using execute
         *
         * @param path
         * @return
         * @throws PathEngineException
         * @throws Exception
         */
        public ExpressionNode parse(String path)
        {
            return parse(path, null);
        }

        public ExpressionNode parse(String path, String name)
        {
            FHIRLexer lexer = new FHIRLexer(path, name);
            if (lexer.done())
                throw lexer.error("Path cannot be empty");
            ExpressionNode result = parseExpression(lexer, true);
            if (!lexer.done())
                throw lexer.error("Premature ExpressionNode termination at unexpected token \"" + lexer.getCurrent() + "\"");
            result.check();
            return result;
        }

        public sealed class ExpressionNodeWithOffset
        {
            private int offset;
            private ExpressionNode node;
            public ExpressionNodeWithOffset(int offset, ExpressionNode node)
            {
                this.offset = offset;
                this.node = node;
            }
            public int getOffset()
            {
                return offset;
            }
            public ExpressionNode getNode()
            {
                return node;
            }

        }

        /**
         * Parse a path for later use using execute
         *
         * @param path
         * @return
         * @throws PathEngineException
         * @throws Exception
         */
        public ExpressionNodeWithOffset parsePartial(String path, int i)
        {
            FHIRLexer lexer = new FHIRLexer(path, i);
            if (lexer.done())
                throw lexer.error("Path cannot be empty");
            ExpressionNode result = parseExpression(lexer, true);
            result.check();
            return new ExpressionNodeWithOffset(lexer.getCurrentStart(), result);
        }

        /**
         * Parse a path that is part of some other syntax
         *
         * @return
         * @throws PathEngineException
         * @throws Exception
         */
        public ExpressionNode parse(FHIRLexer lexer)
        {
            ExpressionNode result = parseExpression(lexer, true);
            result.check();
            return result;
        }


        private ExpressionNode parseExpression(FHIRLexer lexer, bool proximal)
        {
            ExpressionNode result = new ExpressionNode(lexer.nextId());
            ExpressionNode wrapper = null;
            SourceLocation c = lexer.getCurrentStartLocation();
            result.setStart(lexer.getCurrentLocation());
            // special: +/- represents a unary operation at this point, but cannot be a feature of the lexer, since that's not always true.
            // so we back correct for both +/- and as part of a numeric constant below.

            // special: +/- represents a unary operation at this point, but cannot be a feature of the lexer, since that's not always true.
            // so we back correct for both +/- and as part of a numeric constant below.
            if (Utilities.existsInList(lexer.getCurrent(), "-", "+"))
            {
                wrapper = new ExpressionNode(lexer.nextId());
                wrapper.setKind(ExpressionNode.Kind.Unary);
                wrapper.setOperation(ExpressionNode.fromOperationCode(lexer.take()));
                wrapper.setProximal(proximal);
            }

            if (lexer.isConstant())
            {
                bool isString = lexer.isStringConstant();
                if (!isString && (lexer.getCurrent().StartsWith("-") || lexer.getCurrent().StartsWith("+")))
                {
                    // the grammar says that this is a unary operation; it affects the correct processing order of the inner operations
                    wrapper = new ExpressionNode(lexer.nextId());
                    wrapper.setKind(ExpressionNode.Kind.Unary);
                    wrapper.setOperation(ExpressionNode.fromOperationCode(lexer.getCurrent().Substring(0, 1)));
                    wrapper.setProximal(proximal);
                    lexer.setCurrent(lexer.getCurrent().Substring(1));
                }
                result.setConstant(processConstant(lexer));
                result.setKind(ExpressionNode.Kind.Constant);
                if (!isString && !lexer.done() && (result.getConstant() is Integer || result.getConstant() is FhirDecimal) && (lexer.isStringConstant() || lexer.hasToken("year", "years", "month", "months", "week", "weeks", "day", "days", "hour", "hours", "minute", "minutes", "second", "seconds", "millisecond", "milliseconds")))
                {
                    // it's a quantity
                    String ucum = null;
                    if (lexer.hasToken("year", "years", "month", "months", "week", "weeks", "day", "days", "hour", "hours", "minute", "minutes", "second", "seconds", "millisecond", "milliseconds"))
                    {
                        String s = lexer.take();
                        if (s.Equals("year") || s.Equals("years"))
                            ucum = "a";
                        else if (s.Equals("month") || s.Equals("months"))
                            ucum = "mo";
                        else if (s.Equals("week") || s.Equals("weeks"))
                            ucum = "wk";
                        else if (s.Equals("day") || s.Equals("days"))
                            ucum = "d";
                        else if (s.Equals("hour") || s.Equals("hours"))
                            ucum = "h";
                        else if (s.Equals("minute") || s.Equals("minutes"))
                            ucum = "min";
                        else if (s.Equals("second") || s.Equals("seconds"))
                            ucum = "s";
                        else // (s.Equals("millisecond") || s.Equals("milliseconds"))
                            ucum = "ms";
                    }
                    else
                        ucum = lexer.readConstant("units");
                    decimal value = 0;
                    if (result.getConstant() is Integer i && i.Value.HasValue)
                        value = i.Value.Value;
                    if (result.getConstant() is FhirDecimal fd && fd.Value.HasValue)
                        value = fd.Value.Value;
                    result.setConstant(new Quantity()
                    {
                        Value = value,
                        System = "http://unitsofmeasure.org",
                        Code = ucum,
                    });
                }
                result.setEnd(lexer.getCurrentLocation());
            }
            else if ("(".Equals(lexer.getCurrent()))
            {
                lexer.next();
                result.setKind(ExpressionNode.Kind.Group);
                result.setGroup(parseExpression(lexer, true));
                if (!")".Equals(lexer.getCurrent()))
                    throw lexer.error("Found " + lexer.getCurrent() + " expecting a \")\"");
                result.setEnd(lexer.getCurrentLocation());
                lexer.next();
            }
            else
            {
                if (!lexer.isToken() && !lexer.getCurrent().StartsWith("`"))
                    throw lexer.error("Found " + lexer.getCurrent() + " expecting a token name");
                if (lexer.isFixedName())
                    result.setName(lexer.readFixedName("Path Name"));
                else
                    result.setName(lexer.take());
                result.setEnd(lexer.getCurrentLocation());
                if (!result.checkName())
                    throw lexer.error("Found " + result.getName() + " expecting a valid token name");
                if ("(".Equals(lexer.getCurrent()))
                {
                    ExpressionNode.Function? f = ExpressionNode.fromFunctionCode(result.getName());
                    FunctionDetails details = null;
                    if (f == null)
                    {
                        if (hostServices != null)
                            details = hostServices.resolveFunction(result.getName());
                        if (details == null)
                            throw lexer.error("The name " + result.getName() + " is not a valid function name");
                        f = ExpressionNode.Function.Custom;
                    }
                    result.setKind(ExpressionNode.Kind.Function);
                    result.setFunction(f.Value);
                    lexer.next();
                    while (!")".Equals(lexer.getCurrent()))
                    {
                        result.getParameters().Add(parseExpression(lexer, true));
                        if (",".Equals(lexer.getCurrent()))
                            lexer.next();
                        else if (!")".Equals(lexer.getCurrent()))
                            throw lexer.error("The token " + lexer.getCurrent() + " is not expected here - either a \",\" or a \")\" expected");
                    }
                    result.setEnd(lexer.getCurrentLocation());
                    lexer.next();
                    checkParameters(lexer, c, result, details);
                }
                else
                    result.setKind(ExpressionNode.Kind.Name);
            }
            ExpressionNode focus = result;
            if ("[".Equals(lexer.getCurrent()))
            {
                lexer.next();
                ExpressionNode item = new ExpressionNode(lexer.nextId());
                item.setKind(ExpressionNode.Kind.Function);
                item.setFunction(ExpressionNode.Function.Item);
                item.getParameters().Add(parseExpression(lexer, true));
                if (!lexer.getCurrent().Equals("]"))
                    throw lexer.error("The token " + lexer.getCurrent() + " is not expected here - a \"]\" expected");
                lexer.next();
                result.setInner(item);
                focus = item;
            }
            if (".".Equals(lexer.getCurrent()))
            {
                lexer.next();
                focus.setInner(parseExpression(lexer, false));
            }
            result.setProximal(proximal);
            if (proximal)
            {
                while (lexer.isOp())
                {
                    focus.setOperation(ExpressionNode.fromOperationCode(lexer.getCurrent()));
                    focus.setOpStart(lexer.getCurrentStartLocation());
                    focus.setOpEnd(lexer.getCurrentLocation());
                    lexer.next();
                    focus.setOpNext(parseExpression(lexer, false));
                    focus = focus.getOpNext();
                }
                result = organisePrecedence(lexer, result);
            }
            if (wrapper != null)
            {
                wrapper.setOpNext(result);
                result.setProximal(false);
                result = wrapper;
            }
            return result;
        }

        private ExpressionNode organisePrecedence(FHIRLexer lexer, ExpressionNode node)
        {
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Times, ExpressionNode.Operation.DivideBy, ExpressionNode.Operation.Div, ExpressionNode.Operation.Mod }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Plus, ExpressionNode.Operation.Minus, ExpressionNode.Operation.Concatenate }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Union }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.LessThan, ExpressionNode.Operation.Greater, ExpressionNode.Operation.LessOrEqual, ExpressionNode.Operation.GreaterOrEqual }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Is }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Equals, ExpressionNode.Operation.Equivalent, ExpressionNode.Operation.NotEquals, ExpressionNode.Operation.NotEquivalent }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.And }));
            node = gatherPrecedence(lexer, node, new HashSet<ExpressionNode.Operation>(new[] { ExpressionNode.Operation.Xor, ExpressionNode.Operation.Or }));
            // last: implies
            return node;
        }

        private ExpressionNode gatherPrecedence(FHIRLexer lexer, ExpressionNode start, HashSet<ExpressionNode.Operation> ops)
        {
            //	  work : bool;
            //	  focus, node, group : ExpressionNode;

            System.Diagnostics.Debug.Assert(start.isProximal());

            // is there anything to do?
            bool work = false;
            ExpressionNode focus = start.getOpNext();
            if (start.getOperation().HasValue && ops.Contains(start.getOperation().Value))
            {
                while (focus != null && focus.getOperation() != null)
                {
                    work = work || !ops.Contains(focus.getOperation().Value);
                    focus = focus.getOpNext();
                }
            }
            else
            {
                while (focus != null && focus.getOperation() != null)
                {
                    work = work || ops.Contains(focus.getOperation().Value);
                    focus = focus.getOpNext();
                }
            }
            if (!work)
                return start;

            // entry point: tricky
            ExpressionNode group;
            if (ops.Contains(start.getOperation().Value))
            {
                group = newGroup(lexer, start);
                group.setProximal(true);
                focus = start;
                start = group;
            }
            else
            {
                ExpressionNode node = start;

                focus = node.getOpNext();
                while (!ops.Contains(focus.getOperation().Value))
                {
                    node = focus;
                    focus = focus.getOpNext();
                }
                group = newGroup(lexer, focus);
                node.setOpNext(group);
            }

            // now, at this point:
            //   group is the group we are adding to, it already has a .group property filled out.
            //   focus points at the group.group
            do
            {
                // run until we find the end of the sequence
                while (focus.getOperation().HasValue && ops.Contains(focus.getOperation().Value))
                    focus = focus.getOpNext();
                if (focus.getOperation() != null)
                {
                    group.setOperation(focus.getOperation());
                    group.setOpNext(focus.getOpNext());
                    focus.setOperation(null);
                    focus.setOpNext(null);
                    // now look for another sequence, and start it
                    ExpressionNode node = group;
                    focus = group.getOpNext();
                    if (focus != null)
                    {
                        while (focus != null && focus.getOperation().HasValue && !ops.Contains(focus.getOperation().Value))
                        {
                            node = focus;
                            focus = focus.getOpNext();
                        }
                        if (focus != null)
                        { // && (focus.Operation in Ops) - must be true
                            group = newGroup(lexer, focus);
                            node.setOpNext(group);
                        }
                    }
                }
            }
            while (focus != null && focus.getOperation() != null);
            return start;
        }


        private ExpressionNode newGroup(FHIRLexer lexer, ExpressionNode next)
        {
            ExpressionNode result = new ExpressionNode(lexer.nextId());
            result.setKind(ExpressionNode.Kind.Group);
            result.setGroup(next);
            result.getGroup().setProximal(true);
            return result;
        }

        private Base processConstant(FHIRLexer lexer)
        {
            if (lexer.isStringConstant())
            {
                return new FhirString(processConstantString(lexer.take(), lexer));
            }
            if (Utilities.isInteger(lexer.getCurrent()))
            {
                return new Integer(int.Parse(lexer.take()));
            }
            if (decimal.TryParse(lexer.getCurrent(), out decimal decVal))
            {
                lexer.take();
                return new FhirDecimal(decVal);
            }
            //if (Utilities.isDecimal(lexer.getCurrent(), false))
            //{
            //    return new FhirDecimal(lexer.take());
            //}
            if (lexer.getCurrent() == "true")
            {
                lexer.take();
                return new FhirBoolean(true);
            }
            if (lexer.getCurrent() == "false")
            {
                lexer.take();
                return new FhirBoolean(false);
            }
            if (lexer.getCurrent().Equals("{}"))
            {
                lexer.take();
                return null;
            }
            if (lexer.getCurrent().StartsWith("%") || lexer.getCurrent().StartsWith("@"))
            {
                return new FHIRConstant(lexer.take());
            }

            throw lexer.error("Invalid Constant " + lexer.getCurrent());
        }

        //  procedure CheckParamCount(c : integer);
        //  begin
        //    if exp.Parameters.Count <> c then
        //      raise lexer.error('The function "'+exp.name+'" requires '+inttostr(c)+' parameters', offset);
        //  end;

        private bool checkParamCount(FHIRLexer lexer, SourceLocation location, ExpressionNode exp, int count)
        {
            if (exp.getParameters().Count() != count)
                throw lexer.error($"The function \"{exp.getName()}\" requires {count} parameters", location.ToString());
            return true;
        }

        private bool checkParamCount(FHIRLexer lexer, SourceLocation location, ExpressionNode exp, int countMin, int countMax)
        {
            if (exp.getParameters().Count() < countMin || exp.getParameters().Count() > countMax)
                throw lexer.error($"The function \"{exp.getName()}\" requires between {countMin} and {countMax} parameters (provided {exp.getParameters().Count()})", location.ToString());
            return true;
        }

        private bool checkParameters(FHIRLexer lexer, SourceLocation location, ExpressionNode exp, FunctionDetails details)
        {
            switch (exp.getFunction())
            {
                case ExpressionNode.Function.Empty: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Not: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Exists: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.SubsetOf: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.SupersetOf: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.IsDistinct: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Distinct: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Count: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Where: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Select: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.All: return checkParamCount(lexer, location, exp, 0, 1);
                case ExpressionNode.Function.Repeat: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Aggregate: return checkParamCount(lexer, location, exp, 1, 2);
                case ExpressionNode.Function.Item: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.As: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.OfType: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Type: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Is: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Single: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.First: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Last: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Tail: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Skip: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Take: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Union: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Combine: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Intersect: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Exclude: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Iif: return checkParamCount(lexer, location, exp, 2, 3);
                case ExpressionNode.Function.Lower: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Upper: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToChars: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.IndexOf: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Substring: return checkParamCount(lexer, location, exp, 1, 2);
                case ExpressionNode.Function.StartsWith: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.EndsWith: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Matches: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.ReplaceMatches: return checkParamCount(lexer, location, exp, 2);
                case ExpressionNode.Function.Contains: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Replace: return checkParamCount(lexer, location, exp, 2);
                case ExpressionNode.Function.Length: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Children: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Descendants: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.MemberOf: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Trace: return checkParamCount(lexer, location, exp, 1, 2);
                case ExpressionNode.Function.Check: return checkParamCount(lexer, location, exp, 2);
                case ExpressionNode.Function.Today: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Now: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Resolve: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Extension: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.AllFalse: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.AnyFalse: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.AllTrue: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.AnyTrue: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.HasValue: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.Alias: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.AliasAs: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.HtmlChecks: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToInteger: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToDecimal: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToString: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToQuantity: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToBoolean: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToDateTime: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ToTime: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToInteger: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToDecimal: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToString: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToQuantity: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToBoolean: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToDateTime: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConvertsToTime: return checkParamCount(lexer, location, exp, 0);
                case ExpressionNode.Function.ConformsTo: return checkParamCount(lexer, location, exp, 1);
                case ExpressionNode.Function.Custom: return checkParamCount(lexer, location, exp, details.getMinParameters(), details.getMaxParameters());
            }
            return false;
        }

        private String processConstantString(String s, FHIRLexer lexer)
        {
            StringBuilder b = new StringBuilder();
            int i = 1;
            while (i < s.Length - 1)
            {
                char ch = s[i];
                if (ch == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 't':
                            b.Append('\t');
                            break;
                        case 'r':
                            b.Append('\r');
                            break;
                        case 'n':
                            b.Append('\n');
                            break;
                        case 'f':
                            b.Append('\f');
                            break;
                        case '\'':
                            b.Append('\'');
                            break;
                        case '"':
                            b.Append('"');
                            break;
                        case '`':
                            b.Append('`');
                            break;
                        case '\\':
                            b.Append('\\');
                            break;
                        case '/':
                            b.Append('/');
                            break;
                        case 'u':
                            i++;
                            int uc = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber);
                            b.Append((char)uc);
                            i = i + 3;
                            break;
                        default:
                            throw lexer.error("Unknown character escape \\" + s[i]);
                    }
                    i++;
                }
                else
                {
                    b.Append(ch);
                    i++;
                }
            }
            return b.ToString();
        }

        private class ExecutionTypeContext
        {
            private Object appInfo;
            private String resource;
            private TypeDetails context;
            private TypeDetails thisItem;
            private TypeDetails total;


            public ExecutionTypeContext(Object appInfo, string resource, TypeDetails context, TypeDetails thisItem)
            {
                this.appInfo = appInfo;
                this.resource = resource;
                this.context = context;
                this.thisItem = thisItem;

            }
            public string getResource()
            {
                return resource;
            }
            public TypeDetails getThisItem()
            {
                return thisItem;
            }
        }


        public TypeDetails check(Object appContext, string resourceType, string context, ExpressionNode expr)
        {
            // if context is a path that refers to a type, do that conversion now
            TypeDetails types;
            if (context == null)
            {
                types = null; // this is a special case; the first path reference will have to resolve to something in the context
            }
            else if (!context.Contains("."))
            {
                StructureDefinition sd = worker.fetchResource<StructureDefinition>(context);
                types = new TypeDetails(CollectionStatus.SINGLETON, sd.Url);
            }
            else
            {
                string ctxt = context.Substring(0, context.IndexOf('.'));
                if (Utilities.isAbsoluteUrl(resourceType))
                {
                    ctxt = resourceType.Substring(0, resourceType.LastIndexOf("/") + 1) + ctxt;
                }
                StructureDefinition sd = worker.fetchResource<StructureDefinition>(ctxt);
                if (sd == null)
                    throw new PathEngineException("Unknown context " + context, expr.getStart(), expr.ToString());
                ElementDefinitionMatch ed = getElementDefinition(sd, context, true, expr);
                if (ed == null)
                    throw new PathEngineException("Unknown context element " + context, expr.getStart(), expr.ToString());
                if (ed.getFixedType() != null)
                    types = new TypeDetails(CollectionStatus.SINGLETON, ed.getFixedType());
                else if (!ed.getDefinition().Type.Any() || isAbstractType(ed.getDefinition().Type))
                    types = new TypeDetails(CollectionStatus.SINGLETON, ctxt + "#" + context);
                else
                {
                    types = new TypeDetails(CollectionStatus.SINGLETON);
                    foreach (var t in ed.getDefinition().Type)
                        types.addType(t.Code);
                }
            }
            return executeType(new ExecutionTypeContext(appContext, resourceType, types, types), types, expr, true);
        }

        public class ElementDefinitionMatch
        {
            private ElementDefinition definition;
            private string fixedType;
            public ElementDefinitionMatch(ElementDefinition definition, string fixedType)
            {
                this.definition = definition;
                this.fixedType = fixedType;
            }
            public ElementDefinition getDefinition()
            {
                return definition;
            }
            public string getFixedType()
            {
                return fixedType;
            }

        }
        private ElementDefinitionMatch getElementDefinitionById(StructureDefinition sd, string refVal)
        {
            foreach (ElementDefinition ed in sd.Snapshot.Element)
            {
                if (refVal.Equals("#" + ed.ElementId))
                    return new ElementDefinitionMatch(ed, null);
            }
            return null;
        }

        private ElementDefinitionMatch getElementDefinition(StructureDefinition sd, string path, bool allowTypedName, ExpressionNode expr)
        {
            foreach (ElementDefinition ed in sd.Snapshot.Element)
            {
                if (ed.Path.Equals(path))
                {
                    if (!string.IsNullOrEmpty(ed.ContentReference))
                    {
                        return getElementDefinitionById(sd, ed.ContentReference);
                    }
                    else
                        return new ElementDefinitionMatch(ed, null);
                }
                if (ed.Path.EndsWith("[x]") && path.StartsWith(ed.Path.Substring(0, ed.Path.Length - 3)) && path.Length == ed.Path.Length - 3)
                    return new ElementDefinitionMatch(ed, null);
                if (allowTypedName && ed.Path.EndsWith("[x]") && path.StartsWith(ed.Path.Substring(0, ed.Path.Length - 3)) && path.Length > ed.Path.Length - 3)
                {
                    string s = Utilities.uncapitalize(path.Substring(ed.Path.Length - 3));
                    // if (primitiveTypes.contains(s))
                    if (ModelInfo.IsPrimitive(s))
                        return new ElementDefinitionMatch(ed, s);
                    else
                        return new ElementDefinitionMatch(ed, path.Substring(ed.Path.Length - 3));
                }
                if (ed.Path.Contains(".") && path.StartsWith(ed.Path + ".") && (ed.Type.Count() > 0) && !isAbstractType(ed.Type))
                {
                    // now we walk into the type.
                    if (ed.Type.Count() > 1)  // if there's more than one type, the test above would fail this
                        throw new PathEngineException("Internal typing issue....", expr.getStart(), expr.ToString());
                    StructureDefinition nsd = worker.fetchResource<StructureDefinition>(ProfileUtilities.sdNs(ed.Type[0].Code, worker.getOverrideVersionNs()));
                    if (nsd == null)
                        throw new PathEngineException("Unknown type " + ed.Type[0].Code, expr.getStart(), expr.ToString());
                    return getElementDefinition(nsd, nsd.Id + path.Substring(ed.Path.Length), allowTypedName, expr);
                }
                if (!string.IsNullOrEmpty(ed.ContentReference) && path.StartsWith(ed.Path + "."))
                {
                    ElementDefinitionMatch m = getElementDefinitionById(sd, ed.ContentReference);
                    return getElementDefinition(sd, m.getDefinition().Path + path.Substring(ed.Path.Length), allowTypedName, expr);
                }
            }
            return null;
        }

        private bool isAbstractType(List<TypeRefComponent> list)
        {
            return list.Count() != 1 ? true : Utilities.existsInList(list[0].Code, "Element", "BackboneElement", "Resource", "DomainResource");
        }

        private TypeDetails executeType(ExecutionTypeContext context, TypeDetails focus, ExpressionNode exp, bool atEntry)
        {
            // TODO: BRIAN Nothing in this function should be commented out
            //       Was done to get compiling so could move into testing
            TypeDetails result = new TypeDetails(null);
            switch (exp.getKind())
            {
                case Kind.Name:
                    if (atEntry && exp.getName().Equals("$this"))
                        result.update(context.getThisItem());
                    //else if (atEntry && exp.getName().Equals("$total"))
                    //    result.update(anything(CollectionStatus.UNORDERED));
                    //else if (atEntry && focus == null)
                    //    result.update(executeContextType(context, exp.getName(), exp));
                    else
                    {
                        foreach (string s in focus.getTypes())
                        {
                            result.update(executeType(s, exp, atEntry));
                        }
                        if (result.hasNoTypes())
                            throw new PathEngineException("The name " + exp.getName() + " is not valid for any of the possible types: " + focus.describe(), exp.getStart(), exp.ToString());
                    }
                    break;

                //case Kind.Function:
                //    result.update(evaluateFunctionType(context, focus, exp));
                //    break;

                case Kind.Unary:
                    result.addType("integer");
                    break;

                //case Kind.Constant:
                //    result.update(resolveConstantType(context, exp.getConstant(), exp));
                //    break;

                case Kind.Group:
                    result.update(executeType(context, focus, exp.getGroup(), atEntry));
                    break;
            }
            exp.setTypes(result);

            if (exp.getInner() != null)
            {
                result = executeType(context, result, exp.getInner(), false);
            }

            //if (exp.isProximal() && exp.getOperation() != null)
            //{
            //    ExpressionNode next = exp.getOpNext();
            //    ExpressionNode last = exp;
            //    while (next != null)
            //    {
            //        TypeDetails work;
            //        if (last.getOperation() == Operation.Is || last.getOperation() == Operation.As)
            //            work = executeTypeName(context, focus, next, atEntry);
            //        else
            //            work = executeType(context, focus, next, atEntry);
            //        result = operateTypes(result, last.getOperation(), work, exp);
            //        last = next;
            //        next = next.getOpNext();
            //    }
            //    exp.setOpTypes(result);
            //}
            return result;
        }

        private TypeDetails executeType(String type, ExpressionNode exp, bool atEntry)
        {
            if (atEntry && char.IsUpper(exp.getName()[0]) && hashTail(type).Equals(exp.getName())) // special case for start up
                return new TypeDetails(CollectionStatus.SINGLETON, type);
            TypeDetails result = new TypeDetails(null);
            getChildTypesByName(type, exp.getName(), result, exp);
            return result;
        }

        private String hashTail(String type)
        {
            return type.Contains("#") ? "" : type.Substring(type.LastIndexOf("/") + 1);
        }

        private void getChildTypesByName(String type, String name, TypeDetails result, ExpressionNode expr)
        {
            if (Utilities.noString(type))
                throw new PathEngineException("No type provided in BuildToolPathEvaluator.getChildTypesByName", expr.getStart(), expr.ToString());
            if (type.Equals("http://hl7.org/fhir/StructureDefinition/xhtml"))
                return;
            if (type.StartsWith(NS_SYSTEM_TYPE))
                return;

            if (type.Equals(TypeDetails.FP_SimpleTypeInfo))
            {
                getSimpleTypeChildTypesByName(name, result);
            }
            else if (type.Equals(TypeDetails.FP_ClassInfo))
            {
                getClassInfoChildTypesByName(name, result);
            }
            else
            {
                String url = null;
                if (type.Contains("#"))
                {
                    url = type.Substring(0, type.IndexOf("#"));
                }
                else
                {
                    url = type;
                }
                String tail = "";
                StructureDefinition sd = worker.fetchResource<StructureDefinition>(url);
                if (sd == null)
                    throw new DefinitionException("Unknown type " + type); // this really is an error, because we can only get to here if the internal infrastrucgture is wrong
                List<StructureDefinition> sdl = new List<StructureDefinition>();
                ElementDefinitionMatch m = null;
                if (type.Contains("#"))
                    m = getElementDefinition(sd, type.Substring(type.IndexOf("#") + 1), false, expr);
                if (m != null && hasDataType(m.getDefinition()))
                {
                    if (m.getFixedType() != null)
                    {
                        StructureDefinition dt = worker.fetchResource<StructureDefinition>(ProfileUtilities.sdNs(m.getFixedType(), worker.getOverrideVersionNs()));
                        if (dt == null)
                            throw new DefinitionException("unknown data type " + m.getFixedType());
                        sdl.Add(dt);
                    }
                    else
                        foreach (TypeRefComponent t in m.getDefinition().Type)
                        {
                            StructureDefinition dt = worker.fetchResource<StructureDefinition>(ProfileUtilities.sdNs(t.Code, worker.getOverrideVersionNs()));
                            if (dt == null)
                                throw new DefinitionException("unknown data type " + t.Code);
                            sdl.Add(dt);
                        }
                }
                else
                {
                    sdl.Add(sd);
                    if (type.Contains("#"))
                    {
                        tail = type.Substring(type.IndexOf("#") + 1);
                        tail = tail.Substring(tail.IndexOf("."));
                    }
                }

                foreach (StructureDefinition sdi in sdl)
                {
                    String path = sdi.Snapshot.Element[0].Path + tail + ".";
                    if (name.Equals("**"))
                    {
                        System.Diagnostics.Debug.Assert(result.getCollectionStatus() == CollectionStatus.UNORDERED);
                        foreach (ElementDefinition ed in sdi.Snapshot.Element)
                        {
                            if (ed.Path.StartsWith(path))
                                foreach (TypeRefComponent t in ed.Type)
                                {
                                    if (!string.IsNullOrEmpty(t.Code))
                                    {
                                        String tn = null;
                                        if (t.Code.Equals("Element") || t.Code.Equals("BackboneElement"))
                                            tn = sdi.Type + "#" + ed.Path;
                                        else
                                            tn = t.Code;
                                        if (t.Code.Equals("Resource"))
                                        {
                                            foreach (String rn in ModelInfo.SupportedResources) // TODO: BRIAN this should be from the worker that has the supported types in it
                                            {
                                                if (!result.hasType(worker, rn))
                                                {
                                                    getChildTypesByName(result.addType(rn), "**", result, expr);
                                                }
                                            }
                                        }
                                        else if (!result.hasType(worker, tn))
                                        {
                                            getChildTypesByName(result.addType(tn), "**", result, expr);
                                        }
                                    }
                                }
                        }
                    }
                    else if (name.Equals("*"))
                    {
                        System.Diagnostics.Debug.Assert(result.getCollectionStatus() == CollectionStatus.UNORDERED);
                        foreach (ElementDefinition ed in sdi.Snapshot.Element)
                        {
                            if (ed.Path.StartsWith(path) && !ed.Path.Substring(path.Length).Contains("."))
                                foreach (TypeRefComponent t in ed.Type)
                                {
                                    if (Utilities.noString(t.Code)) // Element.id or Extension.url
                                        result.addType("System.string");
                                    else if (t.Code.Equals("Element") || t.Code.Equals("BackboneElement"))
                                        result.addType(sdi.Type + "#" + ed.Path);
                                    else if (t.Code.Equals("Resource"))
                                        result.addTypes(ModelInfo.SupportedResources); // TODO: BRIAN this should be from the worker that has the supported types in it worker.getResourceNames());
                                    else
                                        result.addType(t.Code);
                                }
                        }
                    }
                    else
                    {
                        path = sdi.Snapshot.Element[0].Path + tail + "." + name;

                        ElementDefinitionMatch ed = getElementDefinition(sdi, path, false, expr);
                        if (ed != null)
                        {
                            if (!Utilities.noString(ed.getFixedType()))
                                result.addType(ed.getFixedType());
                            else
                                foreach (TypeRefComponent t in ed.getDefinition().Type)
                                {
                                    if (Utilities.noString(t.Code))
                                    {
                                        if (Utilities.existsInList(ed.getDefinition().ElementId, "Element.id", "Extension.url") || Utilities.existsInList(ed.getDefinition().Base.Path, "Resource.id", "Element.id", "Extension.url"))
                                            result.addType(TypeDetails.FP_NS, "string");
                                        break; // throw new PathEngineException("Illegal reference to primitive value attribute @ "+path);
                                    }

                                    ProfiledType pt = null;
                                    if (t.Code.Equals("Element") || t.Code.Equals("BackboneElement"))
                                        pt = new ProfiledType(sdi.Url + "#" + path);
                                    else if (t.Code.Equals("Resource"))
                                        result.addTypes(ModelInfo.SupportedResources); // TODO: BRIAN this should be from the worker that has the supported types in it worker.getResourceNames());
                                    else
                                        pt = new ProfiledType(t.Code);
                                    if (pt != null)
                                    {
                                        if (t.ProfileElement.Any())
                                            pt.addProfiles(t.ProfileElement);
                                        if (ed.getDefinition().Binding != null)
                                            pt.addBinding(ed.getDefinition().Binding);
                                        result.addType(pt);
                                    }
                                }
                        }
                    }
                }
            }
        }

        private bool hasDataType(ElementDefinition ed)
        {
            return ed.Type.Any() && !(ed.Type[0].Code.Equals("Element") || ed.Type[0].Code.Equals("BackboneElement"));
        }

        private void getClassInfoChildTypesByName(String name, TypeDetails result)
        {
            if (name.Equals("namespace"))
                result.addType(TypeDetails.FP_String);
            if (name.Equals("name"))
                result.addType(TypeDetails.FP_String);
        }

        private void getSimpleTypeChildTypesByName(String name, TypeDetails result)
        {
            if (name.Equals("namespace"))
                result.addType(TypeDetails.FP_String);
            if (name.Equals("name"))
                result.addType(TypeDetails.FP_String);
        }
    }
}

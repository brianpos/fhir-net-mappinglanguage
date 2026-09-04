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

// Port of https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r5/src/main/java/org/hl7/fhir/r5/utils/FHIRLexer.java

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.MappingLanguage.TraceEvent;

namespace Hl7.Fhir.MappingLanguage
{
    public class DebuggerTrace
    {
        public string EngineName { get; set; }
        public DateTime EvaluationTime { get; set; }
        public List<TraceEvent> Events { get; set; } = new List<TraceEvent>();

        private Stack<DebuggerFrame> _callStack = new Stack<DebuggerFrame>();

        /// <summary>
        /// Push an evaluation state onto the call stack
        /// </summary>
        public TraceEvent Push(IAnnotated state, Variables vars)
        {
            _callStack.Push(new DebuggerFrame(state));
            var stateEvent = TraceState(vars);
            stateEvent.Phase = TracePhase.enter;
            return stateEvent;
        }

        public void Trace(Func<TraceEvent> traceEvent)
        {
            Events.Add(traceEvent());
        }

        private TraceEvent CreateEvent()
        {
            var traceEvent = new TraceEvent
            {
                CallDepth = _callStack.Count()
                // CallStack = _callStack.Reverse().ToList()
            };
            return traceEvent;
        }

        public void TraceMessage(string category, Func<string> message)
        {
            var traceEvent = CreateEvent();
            traceEvent.Message = message();
            Events.Add(traceEvent);
        }

        private void SetLocationInformation(TraceEvent eventTrace, IAnnotated processing)
        {

            if (processing?.HasAnnotation<DebugAnnotation>() == true)
            {
                var debugAnnotation = processing.Annotation<DebugAnnotation>();
                eventTrace.CursorStartPosition = debugAnnotation.StartCursor;
                eventTrace.CursorEndPosition = debugAnnotation.EndCursor;
            }

        }

        internal const string OperationOutcomeFileExtension = "http://hl7.org/fhir/StructureDefinition/operationoutcome-file";

        /// <summary>
        /// Trace the current state of the engine
        /// </summary>
        /// <param name="vars"></param>
        /// <param name="processing">An interim state being evaluated that doesn't impact the call stack</param>
        public TraceEvent TraceState(Variables vars, IAnnotated processing = null)
        {
            var traceEvent = CreateEvent();
            List<String> callStackSummary = new List<string>();

            // Replace the location data with that of the incoming processing item
            if (processing != null || _callStack.Any())
            {
                SetLocationInformation(traceEvent, processing ?? _callStack.Peek().State);
                foreach (var frame in _callStack.Select(f => f.State))
                {
                    if (frame is StructureMap.GroupComponent group)
                    {
                        traceEvent.GroupName = group.Name;
                        // also grab the filename from the group level
                        string mapFile = group.GetStringExtension(OperationOutcomeFileExtension);
                        traceEvent.MapFileName = mapFile;
                        callStackSummary.Insert(0, group.Name);
                        break; // only stop when we get to the group level
                    }
                    else if (frame is StructureMap.RuleComponent rule)
                    {
                        traceEvent.RuleName = rule.Name ?? $"rule[???]";
                        callStackSummary.Insert(0, rule.Name ?? $"rule[???]");
                    }
                    else if (frame is StructureMap.SourceComponent source)
                    {
                        callStackSummary.Insert(0, source.Variable ?? $"{source.Context}.{source.Element}");
                    }
                    else if (frame is StructureMap.TargetComponent target)
                    {
                        callStackSummary.Insert(0, target.Variable ?? $"{target.Context}.{target.Element}");
                    }
                    else if (frame is FhirString str)
                    {
                        callStackSummary.Insert(0, str.Value);
                    }
                    else
                    {
                        callStackSummary.Insert(0, "???");
                    }
                }
            }

            SetVariables(traceEvent, vars);
            Events.Add(traceEvent);
            traceEvent.Message = $"{_callStack.Count} " + String.Join(", ", callStackSummary);
            return traceEvent;
        }

        public static void SetVariables(TraceEvent traceEvent, Variables vars)
        {
            traceEvent?.Variables.AddRange(vars?.All()
                .SelectMany(variable => variable.getObject()
                    .Where(value => value != null)
                    .Select(value => new TraceVariable(variable.Mode, variable.Name, value)))
                .ToList() ?? new List<TraceVariable>());
        }

        public IAnnotated Pop(Variables vars)
        {
            var contextCompletedFor = _callStack.Peek();
            var previousEvent = Events.LastOrDefault();
            if (previousEvent?.Phase == TraceEvent.TracePhase.enter)
            {
                Events.RemoveAt(Events.Count-1);
                var stateEvent = TraceState(vars, contextCompletedFor.State);
                stateEvent.Phase = TracePhase.instant;
            }
            else
            {
                var stateEvent = TraceState(vars, contextCompletedFor.State);
                stateEvent.Phase = TracePhase.exit;
            }
            _callStack.Pop();
            return contextCompletedFor.State;
        }
    }

    internal class DebuggerFrame
    {
        public DebuggerFrame(IAnnotated state)
        {
            State = state;
        }

        internal IAnnotated State { get; private set; }
        internal string DisplayLabel { get; set; }
    }

    /// <summary>
    /// The state of the engine after executing a specific step in the mapping (which is referenced by the location/cursor/name props)
    /// All variables in the context of the engine at the evaluated step are captured in the variables prop, which is a list of name/value pairs.
    /// </summary>
    public class TraceEvent
    {
        /// <summary>
        /// Unique identifier for the event, which can be used to correlate with other events in the trace
        /// If there is only one map file in context for the trace, then this is optional
        /// (the top level map is assumed if not present)
        /// </summary>
        /// <remarks>
        /// It is only required to be unique within the context of a single trace, and is not guaranteed to be unique across different traces.
        /// </remarks>
        public string MapFileName { get; set; }

        /// <summary>
        /// Simplified FHIRPath expression referring to the source element that was processed
        /// e.g. "StructureMap.group[0].rule[0].source[2]"
        /// e.g. "StructureMap.group[0].rule[0].target[2].transform"
        /// </summary>
        public string EventLocationExpression { get; set; }

        /// <summary>
        /// offset from the start of the file to the start of the element that was processed
        /// </summary>
        /// <remarks>
        /// These are zero based TUF-16 start-inclusive, end-exclusive offsets, which can be used to extract the relevant substring from the source file.
        /// </remarks>
        public int CursorStartPosition { get; set; }
        public int CursorEndPosition { get; set; }


        public string GroupName { get; set; }
        public string RuleName { get; set; }
        
        // Debugger assisting properties
        public int CallDepth { get; set; }

        /// <summary>
        /// Message to display - used by server debuggers that aren't able to display the full trace, but can display a message for each step in the trace.
        /// For full featured traces, this message is only used for the `log()` function, otherwise is excluded 
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The type of any Message included
        /// </summary>
        public TraceMessageType? MessageType { get; set; } 
        public enum TraceMessageType { error, warning, information }; 


        public List<TraceVariable> Variables { get; set; } = new List<TraceVariable>();

        /// <summary>
        /// If this property is true, then this trace is the final result of a specific rule/group
        /// If false, this is the initial state of a group/rule (so you can step in/over)
        /// </summary>
        /// <remarks>
        /// Stepping through a group will hit each rule twice, the start rule, and end rule (giving final results)
        /// This permits stepping into rules
        /// </remarks>
        public TracePhase Phase { get; set; }
        public enum TracePhase { instant, enter, exit };
    }

    public class TraceVariable
    {
        public TraceVariable(StructureMapUtilitiesAnalyze.VariableMode mode, string name, ITypedElement value)
        {
            Mode = mode;
            Name = name;
            Path = GetPath(value);
            Type = value.InstanceType;

            // convert the value for usage in the parameters object
            IEnumerable<ITypedElement> values = new[] { value };
            var fhirValue = values.ToFhirValues().FirstOrDefault();
            if (fhirValue is DataType dt)
                FhirValue = dt;
            else
                JsonValue = GetValue(value);
        }

        private static string GetPath(ITypedElement value)
        {
            if (value is IShortPathGenerator shortPathGenerator)
                return shortPathGenerator.ShortPath;
            if (value is FhirJsonNode jsonNode)
                return jsonNode.Location;
            return null;
        }

        private static string GetValue(ITypedElement value)
        {
            return value.Value?.ToString() ?? value.ToJson();
        }


        public StructureMapUtilitiesAnalyze.VariableMode Mode { get; }
        public string Name { get; }
        /// <summary>
        /// If the variable is a FHIR element, this is the path to the element in the source resource (e.g. "Patient.name[0].given[0]")
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The datatype of the variable (which will be displayed in the debugger).
        /// For FHIR elements, this is the FHIR type (e.g. "string", "CodeableConcept", "Patient", etc.)
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The FHIR Version of the datatype - if not the same as the FHIR Version of the overall trace
        /// This is only required where the mapping engine is processing resources from different FHIR versions (e.g. R4 and R5) in the same trace.
        /// </summary>
        public string FhirVersion { get; }

        /// <summary>
        /// The value of the variable, which will be displayed in the debugger.
        /// </summary>
        public DataType FhirValue { get; }

        public string JsonValue { get; }
    }
}

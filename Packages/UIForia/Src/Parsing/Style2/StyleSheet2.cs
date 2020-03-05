using System;
using System.Collections.Generic;
using System.Threading;
using UIForia.Rendering;
using UIForia.Selectors;
using UIForia.Style;
using UIForia.Util;
using Unity.Collections;
using UnityEngine;

namespace UIForia.Style2 {

    public struct StylePointer { }

    public struct Style {

        private int styleId;
        private StyleSheet2 styleSheet;

        internal Style(StyleSheet2 sheet, int styleId) {
            this.styleSheet = sheet;
            this.styleId = styleId;
        }

        public int GetPropertyCount(StyleState state = 0) {
            return styleSheet.GetPropertyRange(styleId, state).length;
        }

        public bool TryGetProperty(PropertyId propertyId, out StyleProperty2 property, StyleState state = StyleState.Normal) {
            if (styleSheet == null) {
                property = default;
                return false;
            }

            Range16 range = styleSheet.GetPropertyRange(styleId, state);

            for (int i = range.start; i < range.end; i++) {
                if (styleSheet.properties.array[i].propertyId.id == propertyId.id) {
                    property = styleSheet.properties.array[i];
                    return true;
                }
            }

            property = default;
            return false;
        }

    }

    internal unsafe struct InternalStyle {

        public int id;
        public CharSpan name;
        public fixed uint ranges[4];

        public InternalStyle(CharSpan name, int id) : this() {
            this.id = id;
            this.name = name;
        }

    }

    internal class TopSortNode {

        public int mark;
        public ParsedStyle style;
        public LightList<TopSortNode> dependencies;

    }

    internal class DependencySorter {

        private LightList<TopSortNode> temp;
        private StructList<ParsedStyle> sorted;

        public void Sort(IList<ParsedStyle> styles) {
            LightList<TopSortNode> source = new LightList<TopSortNode>(styles.Count);

            for (int i = 0; i < styles.Count; i++) {
                // need to resolve dependencies and map to top sort nodes
                TopSortNode n = new TopSortNode();
                n.style = styles[i];
                n.mark = 0;
                n.dependencies = new LightList<TopSortNode>();
                source.Add(n);
            }

            while (source.size != 0) {
                Visit(source[0]);
            }
        }

        internal void Visit(TopSortNode node) {
            if (node.mark == 1) {
                // error
            }

            if (node.mark == 0) {
                node.mark = 1;
                // get dependencies
                for (int i = 0; i < node.dependencies.size; i++) {
                    Visit(node.dependencies[i]);
                }

                node.mark = 2;
                sorted.Add(node.style);
            }
        }

    }

    // parser can check xml for for </Style> and create file for it if needed without actually parsing the content

    // parsing != building
    // parsing totally done in parallel because no building is needed
    // building totally done in parallel because all data needed for building is present
    // building probably able to run in jobs
    // if path not found should be reported at parse time
    // if variable or style not found should reported after parse before first build?

    // crunching style sheet -> new implicit sheet = more memory usage (probably configurable)

    // xml style files? xml parser probably needs to either call parser in a 1 off fashion or we parse all xml files, add pseudo style files, parse those too

    // 2nd pass to build style parse dependencies? or encounter as needed?

    public class StyleSheet2 {

        public readonly Module module;
        public readonly string filePath;
        internal readonly char[] source;

        internal StructList<InternalStyle> styles;
        internal StructList<Mixin> mixins;
        internal StructList<Selector> selectors;
        internal StructList<RunCommand> commands;
        internal StructList<PendingConstant> constants;

        internal ParsedStyle[] rawStyles;

        internal StructList<StyleProperty2> properties;

        // animations
        // spritesheets
        // sounds
        // cursors

        internal StyleBodyPart[] parts;
        public readonly ushort id;

        private static int idGenerator;
        [ThreadStatic] private static StructList<char> s_CharBuffer;

        internal StyleSheet2(Module module, string filePath, char[] source) {
            this.id = (ushort) Interlocked.Add(ref idGenerator, 1);
            this.module = module;
            this.filePath = filePath;
            this.source = source;
            this.properties = new StructList<StyleProperty2>();
        }

        public bool TryGetStyle(string styleName, out Style style) {
            for (int i = 0; i < styles.size; i++) {
                if (styles[i].name == styleName) {
                    style = new Style(this, styles[i].id);
                    return true;
                }
            }

            style = default;
            return false;
        }

        private unsafe void BuildExternalStyle(ref InternalStyle style) { }

        // todo -- this needs to be the last of the build steps
        private unsafe void BuildStyle(ref ParsedStyle parsedStyle) {
            int NextMultiple = BitUtil.NextMultipleOf32(PropertyParsers.PropertyCount) >> 5; // divide by 32

            StyleProperty2* normal = stackalloc StyleProperty2[PropertyParsers.PropertyCount];
            StyleProperty2* active = stackalloc StyleProperty2[PropertyParsers.PropertyCount];
            StyleProperty2* hover = stackalloc StyleProperty2[PropertyParsers.PropertyCount];
            StyleProperty2* focus = stackalloc StyleProperty2[PropertyParsers.PropertyCount];
            StyleProperty2** states = stackalloc StyleProperty2*[4];
            IntBoolMap* maps = stackalloc IntBoolMap[4];

            uint* map0 = stackalloc uint[NextMultiple];
            uint* map1 = stackalloc uint[NextMultiple];
            uint* map2 = stackalloc uint[NextMultiple];
            uint* map3 = stackalloc uint[NextMultiple];

            maps[StyleStateIndex.Normal] = new IntBoolMap(map0, NextMultiple);
            maps[StyleStateIndex.Active] = new IntBoolMap(map1, NextMultiple);
            maps[StyleStateIndex.Hover] = new IntBoolMap(map2, NextMultiple);
            maps[StyleStateIndex.Focus] = new IntBoolMap(map3, NextMultiple);

            states[StyleStateIndex.Normal] = normal;
            states[StyleStateIndex.Active] = active;
            states[StyleStateIndex.Hover] = hover;
            states[StyleStateIndex.Focus] = focus;

            StyleBuildData buildData = new StyleBuildData() {
                states = states,
                targetSheet = this,
                targetStyleName = parsedStyle.name,
                maps = maps
            };

            InternalStyle style = new InternalStyle();

            for (int i = parsedStyle.partEnd - 1; i >= parsedStyle.partStart; i--) {
                ref StyleBodyPart part = ref parts[i];

                switch (part.type) {
                    case BodyPartType.Property: {
                        int propertyIdIndex = part.property.propertyId.index;
                        ref IntBoolMap map = ref buildData.maps[buildData.stateIndex];
                        StyleProperty2* state = buildData.states[buildData.stateIndex];

                        if (map.TrySetIndex(propertyIdIndex)) {
                            state[map.Occupancy - 1] = part.property;
                        }

                        break;
                    }

                    case BodyPartType.VariableProperty: {
                        int propertyIdIndex = part.propertyId.index;
                        ref IntBoolMap map = ref buildData.maps[buildData.stateIndex];
                        StyleProperty2* state = buildData.states[buildData.stateIndex];

                        if (map.TrySetIndex(propertyIdIndex)) {
                            // need to evaluate the property

                            // while has match for $ or @ 
                            // get identifier
                            // resolve its value
                            // replace it 

                            // find @identifier
                            // find @identifier.identifier
                            // find $identifier

                            // replace with constant value if @

                            s_CharBuffer = s_CharBuffer ?? new StructList<char>(128);
                            s_CharBuffer.size = 0;

                            StyleProperty2 property = default;

                            CharStream stream = new CharStream(source, part.stringData);

                            int idx = stream.NextIndexOf('@');

                            if (idx != -1) {
                                // stream.CopyTo(s_CharBuffer, 0, stream.Ptr, idx);

                                s_CharBuffer.AddRange(source, stream.IntPtr, idx - stream.IntPtr);

                                stream.AdvanceTo(idx + 1);

                                if (!stream.TryParseIdentifier(out CharSpan identifier)) { }

                                if (stream.TryParseCharacter('.')) {

                                    if (!stream.TryParseIdentifier(out CharSpan dotIdentifier)) {
                                        throw new NotImplementedException();
                                    }

                                    // todo -- figure out how to handle referenced constants
                                    // probably want to localize them at parse time and resolve in the build step. match against whole name

                                    throw new NotImplementedException();

                                }
                                else {

                                    if (TryResolveLocalConstant(identifier, out CharSpan span)) {
                                        s_CharBuffer.EnsureAdditionalCapacity(span.Length);
                                        for (int j = span.rangeStart; j < span.rangeEnd; j++) {
                                            s_CharBuffer.array[s_CharBuffer.size++] = span.data[j];
                                        }
                                    }

                                }

                                CharStream parseStream = new CharStream(s_CharBuffer.array, 0, (uint) s_CharBuffer.size);
                                if (!PropertyParsers.s_parseEntries[propertyIdIndex].parser.TryParse(parseStream, part.propertyId, out property)) { }

                            }

                            state[map.Occupancy - 1] = property;
                        }

                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            style.name = parsedStyle.name;

            // this could easily be converted to a native list if we need that

            int normalCount = buildData.maps[StyleStateIndex.Normal].Occupancy;
            int hoverCount = buildData.maps[StyleStateIndex.Hover].Occupancy;
            int activeCount = buildData.maps[StyleStateIndex.Active].Occupancy;
            int focusCount = buildData.maps[StyleStateIndex.Focus].Occupancy;

            style.ranges[StyleStateIndex.Normal] = new Range16(properties.size, normalCount);

            for (int i = 0; i < normalCount; i++) {
                properties.Add(buildData.states[StyleStateIndex.Normal][i]);
            }

            style.ranges[StyleStateIndex.Hover] = new Range16(properties.size, hoverCount);

            for (int i = 0; i < hoverCount; i++) {
                properties.Add(buildData.states[StyleStateIndex.Hover][i]);
            }

            style.ranges[StyleStateIndex.Active] = new Range16(properties.size, activeCount);

            for (int i = 0; i < activeCount; i++) {
                properties.Add(buildData.states[StyleStateIndex.Active][i]);
            }

            style.ranges[StyleStateIndex.Focus] = new Range16(properties.size, focusCount);

            for (int i = 0; i < focusCount; i++) {
                properties.Add(buildData.states[StyleStateIndex.Focus][i]);
            }

            styles.Add(style);
        }

        private void BuildLocalConstants() {
            List<bool> moduleConditions = module.GetDisplayConditions();

            for (int i = 0; i < constants.size; i++) {

                ref PendingConstant constant = ref constants.array[i];

                constant.resolvedValue = default;

                for (int j = constant.partRange.start; j < constant.partRange.end; j++) {
                    ref StyleBodyPart part = ref parts[j];
                    if (moduleConditions[part.intData]) {
                        constant.resolvedValue = new CharSpan(source, part.stringData.rangeStart, part.stringData.rangeEnd);
                        break;
                    }
                }

                if (constant.resolvedValue != null) {
                    constant.resolvedValue = constant.defaultValue;
                }

            }
        }

        private bool TryResolveLocalConstant(CharSpan identifier, out CharSpan charSpan) {

            if (identifier.Length == 0) {
                charSpan = default;
                return false;
            }

            for (int i = 0; i < constants.size; i++) {

                ref PendingConstant constant = ref constants.array[i];

                if (constant.name != identifier) {
                    continue;
                }

                charSpan = constant.resolvedValue;
                return true;

            }

            charSpan = default;
            return false;

        }

        public unsafe void Build() {

            BuildLocalConstants();

            if (rawStyles != null) {
                // todo -- can probably be an array
                if (styles == null) {
                    styles = new StructList<InternalStyle>(rawStyles.Length);
                }

                for (int i = 0; i < rawStyles.Length; i++) {
                    BuildStyle(ref rawStyles[i]);
                }
            }
        }

        public string GetConstant(string s) {

            CharSpan span = new CharSpan(s);
            if (TryResolveLocalConstant(span, out CharSpan value)) {
                return value.ToString();
            }

            return null;
        }

        internal bool AddLocalConstant(in PendingConstant constant) {
            constants = constants ?? new StructList<PendingConstant>();
            for (int i = 0; i < constants.size; i++) {

                if (constants.array[i].name != constant.name) {
                    continue;
                }

                if (constants.array[i].HasDefinition) {
                    return false;
                }

                constants.array[i] = constant;
                return true;
            }

            constants.Add(constant);
            return true;
        }

        internal void SetParts(StyleBodyPart[] partArray) {
            this.parts = partArray;
        }

        internal void SetStyles(ParsedStyle[] rawStyleArray) {
            this.rawStyles = rawStyleArray;
        }

        public Range16 GetPropertyRange(int styleId, StyleState state) {
            unsafe {
                ref InternalStyle ptr = ref styles.array[styleId];
                switch (state) {
                    case StyleState.Normal:
                        return new Range16(ptr.ranges[StyleStateIndex.Normal]);

                    case StyleState.Hover:
                        return new Range16(ptr.ranges[StyleStateIndex.Hover]);

                    case StyleState.Active:
                        return new Range16(ptr.ranges[StyleStateIndex.Active]);

                    case StyleState.Focused:
                        return new Range16(ptr.ranges[StyleStateIndex.Focus]);

                    default:
                        return new Range16(
                            new Range16(ptr.ranges[StyleStateIndex.Normal]).start,
                            new Range16(ptr.ranges[StyleStateIndex.Focus]).end
                        );
                }
            }
        }

        internal void EnsureConstant(CharSpan identifier) {
            if (constants == null) {
                constants = new StructList<PendingConstant>();
            }

            for (int i = 0; i < constants.size; i++) {
                if (constants.array[i].name == identifier) {
                    return;
                }
            }

            constants.Add(new PendingConstant() {
                name = identifier
            });
        }

    }

}
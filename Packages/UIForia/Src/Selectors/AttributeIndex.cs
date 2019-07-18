using UIForia.Elements;
using UIForia.Util;

namespace UIForia.Selectors {

    internal class StyleIndex {

            

    }

    internal class ElementIndex {

        public void FindWithAttrStyleCount() { }
        
        public void FindWithAttrCount() { }
        
        public void FindWithStyles() { }

        public void FindWithAttribute() {
            
//            for (int i = 0; i < elements.size; i++) {
//                
//            }
            
        }

    }
    
    internal class AttributeIndex {

      
        public StructList<Entry> entries;
        public string attrKey;
        
        public AttributeIndex() {
            this.entries = new StructList<Entry>();
        }
        
        public void Add(UIElement element, string attrValue) {
            entries.Add(new Entry() {
                element = element,
                attrValue = attrValue
            });
        }

        public void Update(UIElement element, string value) {
            Entry[] entryArray = entries.array;
            for (int i = 0; i < entries.size; i++) {
                if (entryArray[i].element == element) {
                    entryArray[i].attrValue = value;
                    return;
                }
            } 
        }

        public void FindMatchingValue(string attrValue, LightList<UIElement> retn) {
            int count = entries.size;
            Entry[] entryArray = entries.array;
            
            for (int i = 0; i < count; i++) {
                StructList<ElementAttribute> attributes = entryArray[i].element.attributes;
                int attrCount = attributes.size;
                ElementAttribute[] attrs = attributes.array;
                
                for (int j = 0; j < attrCount; j++) {
                    if (attrs[i].name == attrKey) {
                        if (attrs[i].value == attrValue) {
                            retn.Add(entryArray[i].element);
                            break;
                        }       
                    }
                }
            }
            
        }
        
       
        public struct Entry {

            public UIElement element;
            public string attrValue;

        }

    }

}
//
//  + next sibling
//        - prev sibling
//        ~ any sibling
//        [attr]
//        @styleName
//        TagName
//        & this
//        >> direct child
//        > descendent
//        => deep descendent (not only this template)
//        #id
//
//        :deep
//        :focus-within
//        :focus-visible
//        :first-child
//        :nth-child
//        :last-child
//        :not()
//
//        %hover
//        %active
//        %focus
//
//
//        StructList<ElementData> data = AttributeIndex("a");
//        for(int i = 0; i < data.size; i++) {
//            
//            if((data[i].flags & enabled) == 0) {
//                continue;
//            }
//            output.Add(data[i].elementLookup[data[i].elementIndex]);
//        }
//
//        for(int i = 0; i < output.size; i++) {
//            if(output[i].nextSiblingEnabled) {
//
//            }
//            next.Add(output[i]);
//        }
//
//        for(int i = 0; i < next.size; i++) {
//            if(next[i].nextSiblingEnabled) {
//                nextSibling.element.style.HasStyle(styleId)
//            }
//        }
//
//        // sibling combinator gives an exact element, no need to look up in index
//
//        // descendent combinator needs to use the index
//
//        // direct descendent does not need to the index since it will have to do a parent check
//
//        // ideally we keep a single store of all element data that can be looked up w/o dereferencing elements for data
//        // kinda bad for element type indexing since we don't have that data available though
//        // elements CAN know which caches they belong to but then updating traversal index in a bunch of places sux
//
//        indices need element data
//        want to avoid dereferencing pointers where possible
//        element can be indexed in many places
//
//        maybe only have caches/indices for what we know is actually in a selector
//
//        element.SetAttribute("attr", "xxx");
//        attrIndex["attr"].Add(element);
//
//        public struct ElementIndex {
//            public int elementId;
//            public ushort typeId;
//            public ushort styleId0;
//            public ushort styleId1;
//            public ushort styleId2;
//            public ushort styleId3;
//            public byte attrCount;
//            public byte styleCount; 
//            public byte nextSibling; // has next sibling & flags or maybe just index
//            public byte prevSibing;  // has prev sibling & flags or maybe just index
//            public byte flags; // can truncate to just take life cycle ones
//            public ushort siblingIndex;
//            public ushort traversalIndex;
//            public ushort nextInLineTraversalIndex; // ie ancestors will be less than this
//        }
//
//        element type indexes
//            build as we go -> auto sorted by traversal order, works if we only partially build, just delete entries > build start
//            need to know which elements have an index
//            somewhat scattered in memory
//            can be refless other than having array to contain data
//            super linear search
//            
//            need to keep flags in sync for enable / disable or re-walk when enable or disable happens on a frame (which is really bad)
//            rewalk doesn't mean recompute all selectors
//
//        attribute indexes
//            remove on element destroyed, can be queued & done in parallel w/something but maybe a lot of removes
//            remove on attr remove, add on attr add, update w/ flags? bad, better to point into traversal tree (ptr will be on element)
//        
//        style indexes
//            same as attribute indexes
//
//        : is a modifer, can't be standalone. would need to be &:first-child or [attr]:nth-child(9), etc
//        within a phrase order shouldn't matter between tag names, attributes and styles. modifiers must come last in any order
//        selector & >> Input[attr] => Div >> @styleName[attrA][attrB]:first-child > Div@styleName + [attr:a="value"] {
//            
//            // for re-evaluation, can generate a pre-filter function for changes that can quickly check if we need to respond to the change based on indices
//
//            // if element type index is not the smallest, then type becomes a requirement filter to the searched index
//
//            at each phrase only 1 index is scanned, always the smallest (need to figure out how to structure this)
//
//            // probably some optimization for attrs, styles, etc being added & removed in on remove if the element removed from was not descendent of any target element
//            // track 'highest' target element
//            // if changes happen above this element, do nothing
//            // if target count == 0 do nothing
//            // if no active targets, do nothing
//
//            // when a style name is added or removed, evaluate
//            selector.Subscribe("styleName");
//
//            // when a placeholder is added or removed, evaluate
//            selector.Subscribe("attr:attrA")
//            selector.Subscribe("attr:attrB")
//            selector.Subcribe("hover")
//
//            // when an input is added, evaluate
//            selector.Subscribe(<Input>)
//
//            Find all elements with a placeholder attribute
//            
//            for each result so far
//
//            if(result is a descendent of any placeholder result)
//                add to next set
//
//            find all input elements
//                if any result is a descendent of input
//                    add to next set
//
//            that is a descendent of (this)
//
//            find elements with attr a where value == value
//            take next siblings of that result set
//
//            for each one who has an ancestor w/ a placeholder attribute
//
//            for each of those who have ancestor of type Input
//
//            for each target
//                if any result is a direct child of the target, match it
//
//            for each selector with query (query) {
//                
//                for each result element
//                    if !element.IsDescendentOf(target)
//                        continue
//                
//                ApplySelectorStyles();
//
//            }
//
//
//        }
//
//        public struct TraversedElement {
//            public int parentIndex;
//            public UIElement element;
//        }
//
//        within a selector phrase the first thing in the sequence is the main filter
//            @styleName[attr:xxx] finds elements w/ style name then rejects all results that do not also have attr xxx
//            [attr:xxx]@styleName finds all elements w/ attr xxx then rejects results that do not also have style style name
//
//            which is faster will depend on the index size, maybe find the smalles index and run that one, favor the first when tied
//
//        for (int i = 0; i < pseudoState.size; i++) {
//
//        }
//
//        for (int i = 0; i < attrIndex.size; i++) {
//
//        }
//
//        for(int i = 0; i < activeCount; i++) {
//            
//            if(array[i].viewId != viewId) continue;
//
//            if(array[i].depth >= depth) continue;
//            
//            if(!array[i].enabled) continue;
//
//            if(array[i].attrCount == 0) continue;
//
//            if(array[i].siblingIndex != target) continue;
//
//            if(array[i].childCount != target) continue;
//            
//            if(array[i].hasNextSibling != target) continue;
//
//            if(array[i].hasPreviousSibling != target) continue;
//
//            if(array[i].pseudoState != target) continue;
//
//            if(array[i].element.HasAttribute(target)) {
//                
//                if(array[i].element.IsDescendentOf(id)) {
//                    output.Add(array[i].element);
//                }
//
//            }
//
//            if(array[i].element.HasStyle(styleId)) {
//
//            }
//
//            if(array[i].element.HasAttributeWithValue(target)) {
//                
//                if(array[i].element.IsDescendentOf(id)) {
//                    output.Add(array[i].element);
//                }
//
//            }
//
//        }
//
//    }
//
//}
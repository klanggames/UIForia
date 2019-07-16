using System;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Parsing.Expression;
using UIForia.Util;

namespace UIForia.Compilers {

    public class CompiledTemplate {

        internal ProcessedType elementType;
        internal AttributeDefinition2[] attributes;
        public string fileName;
        public int templateId;
        public StructList<SlotDefinition> slotDefinitions;
        public int childCount;
        
        public bool TryGetSlotData(string slotName, out SlotDefinition slotDefinition) {
            if (slotDefinitions == null) {
                slotDefinition = default;
                return false;
            }

            for (int i = 0; i < slotDefinitions.Count; i++) {
                if (slotDefinitions[i].tagName == slotName) {
                    slotDefinition = slotDefinitions[i];
                    return true;
                }
            }

            slotDefinition = default;
            return false;
        }

        public int AddSlotData(SlotDefinition slotDefinition) {
            slotDefinitions = slotDefinitions ?? StructList<SlotDefinition>.Get();
            slotDefinition.slotId = (short) slotDefinitions.Count;
            slotDefinitions.Add(slotDefinition);
            return slotDefinition.slotId;
        }

        internal UIElement Create(UIElement root, TemplateScope2 scope) {
            // todo -- get rid of the build call here
            scope.application.templateData.Build();
            return scope.application.templateData.templateFns[templateId](root, scope);
        }

        public int GetSlotId(string slotName) {
            if (slotDefinitions == null) {
                throw new ArgumentOutOfRangeException(slotName, $"Slot name {slotName} was not registered");
            }

            for (int i = 0; i < slotDefinitions.Count; i++) {
                if (slotDefinitions.array[i].tagName == slotName) {
                    return slotDefinitions[i].slotId;
                }
            }

            throw new ArgumentOutOfRangeException(slotName, $"Slot name {slotName} was not registered");
        }


        public void ValidateSlotHierarchy(LightList<string> slotList) {
            // ensure no duplicates
            for (int i = 0; i < slotList.size; i++) {
                string target = slotList[i];
                for (int j = i + 1; j < slotList.size; j++) {
                    if (slotList[j] == target) {
                        throw new TemplateParseException(fileName, $"Invalid slot input, you provided the slot name {target} multiple times");
                    }
                }
            }

            for (int i = 0; i < slotList.size; i++) {
                TryGetSlotData(slotList[i], out SlotDefinition slotDefinition);

                for (int j = 0; j < 4; j++) {
                    int slotId = slotDefinition[j];
                    if (slotId != SlotDefinition.k_UnassignedParent) {
                        SlotDefinition parentSlotDef = slotDefinitions[slotId];
                        if (slotList.Contains(parentSlotDef.tagName)) {
                            throw new TemplateParseException(fileName, $"Invalid slot hierarchy, the template {elementType.rawType} defines {slotDefinition.tagName} to be a child of {parentSlotDef.tagName}. You can only provide one of these.");
                        }
                    }
                    else {
                        break;
                    }
                }
            }
        }

        public string GetValidSlotNameMessage() {
            string retn = "";
            retn = elementType.rawType.Name;
            if (slotDefinitions == null) {
                return retn + " does not define any input slots";
            }

            retn += " defines the following slot inputs: ";
            for (int i = 0; i < slotDefinitions.Count; i++) {
                retn += slotDefinitions[i].tagName;
                if (i != slotDefinitions.size - 1) {
                    retn += ", ";
                }
            }

            return retn;
        }

    }

}
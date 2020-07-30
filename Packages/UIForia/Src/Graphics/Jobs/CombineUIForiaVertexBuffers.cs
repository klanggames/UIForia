﻿using UIForia.ListTypes;
using UIForia.Util;
using UIForia.Util.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace UIForia.Graphics {

    [BurstCompile]
    internal unsafe struct CombineUIForiaVertexBuffers : IJob {

        public DataList<MeshInfo> meshList;

        public DataList<UIForiaVertex>.Shared elementVertexList;
        public DataList<UIForiaVertex>.Shared textVertexList;

        public List_Int32 matrixIdList;
        public List_Int32 clipRectIdList;
        public DataList<UIForiaMaterialInfo>.Shared textMaterialBuffer;
        public DataList<ElementMaterialInfo>.Shared elementMaterialBuffer;

        public void Execute() {

            uint textMaterialOffset = (uint)textMaterialBuffer.size;
            
            textMaterialBuffer.AddRange((UIForiaMaterialInfo*)elementMaterialBuffer.GetArrayPointer(), elementMaterialBuffer.size);
            
            int textVertexCount = textVertexList.size;
            textVertexList.EnsureAdditionalCapacity(elementVertexList.size);
            textVertexList.AddRange(elementVertexList.GetArrayPointer(), elementVertexList.size);

            UIForiaVertex* vertices = textVertexList.GetArrayPointer();

            for (int i = 0; i < meshList.size; i++) {

                ref MeshInfo meshInfo = ref meshList[i];

                // todo -- reimplement elements here, I think its identical, just needs an offset to vertices of textList.size
                if (meshInfo.type == MeshInfoType.Text) {
                    int start = meshInfo.vertexStart;
                    int end = start + meshInfo.vertexCount;

                    int matrixId = matrixIdList.array[i];
                    int clipRectId = clipRectIdList.array[i];
                    uint clipAndMatrix = (uint) BitUtil.SetHighLowBits(clipRectId, matrixId);

                    for (int v = start; v < end; v++) {
                        vertices[v].indices.x = clipAndMatrix;
                    }
                }
                else if (meshInfo.type == MeshInfoType.Element) {
                    int start = textVertexCount + meshInfo.vertexStart;
                    int end = start + meshInfo.vertexCount;

                    int matrixId = matrixIdList.array[i];
                    int clipRectId = clipRectIdList.array[i];
                    uint clipAndMatrix = (uint) BitUtil.SetHighLowBits(clipRectId, matrixId);

                    for (int v = start; v < end; v++) {
                        vertices[v].indices.x = clipAndMatrix;
                        vertices[v].indices.y += textMaterialOffset; // todo if not a full int, need to mask
                    }
                }

            }

        }

    }

}
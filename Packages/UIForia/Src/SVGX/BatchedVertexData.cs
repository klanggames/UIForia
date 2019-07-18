using System;
using System.Collections.Generic;
using UIForia.Extensions;
using UIForia.Rendering;
using UIForia.Text;
using UIForia.Util;
using UnityEngine;

namespace SVGX {

    public class BatchedVertexData {

        internal const int RenderTypeFill = 0;
        internal const int RenderTypeText = 1;
        internal const int RenderTypeStroke = 2;
        internal const int RenderTypeStrokeShape = 3;
        internal const int RenderTypeShadow = 4;

        public readonly Mesh mesh;

        public int triangleIndex;

        private readonly List<Vector3> positionList;
        private readonly List<Vector4> uv0List;
        private readonly List<Vector4> uv1List;
        private readonly List<Vector4> uv2List;
        private readonly List<Vector4> uv3List;
        private readonly List<Vector4> uv4List;
        private readonly List<Color> colorsList;
        private readonly List<int> trianglesList;

        // todo -- figure out bit packing and remove uv3List and uv4List
        // todo -- when unity allows better mesh api, remove lists in favor of array
        // todo -- verify that mesh.Clear doesn't cause a re-allocation

        public BatchedVertexData() {
            positionList = new List<Vector3>(128);
            uv0List = new List<Vector4>(128);
            uv1List = new List<Vector4>(128);
            uv2List = new List<Vector4>(128);
            uv3List = new List<Vector4>(128);
            uv4List = new List<Vector4>(128);
            colorsList = new List<Color>(128);
            trianglesList = new List<int>(128 * 3);
            mesh = new Mesh();
            mesh.MarkDynamic();
        }

        private const int DrawType_Fill = 1;
        private const int DrawType_Stroke = 0;
        private const int VertexType_Near = 1;
        private const int VertexType_Far = 0;

//        private static List<Vector3> s_Vector3List = new List<Vector3>(0);
//        private static List<Vector3> s_PositionList = new List<Vector3>(0);
        
        public Mesh FillMesh() {
            mesh.Clear(true);

            mesh.SetVertices(positionList);
            mesh.SetColors(colorsList);
            mesh.SetUVs(0, uv0List);
            mesh.SetUVs(1, uv1List);
            mesh.SetUVs(2, uv2List);
            mesh.SetUVs(3, uv3List);
            mesh.SetUVs(4, uv4List);
            mesh.SetTriangles(trianglesList, 0);

//            ListAccessor<Vector3>.Set(positionList, p);
            positionList.Clear();
            uv0List.Clear();
            uv1List.Clear();
            uv2List.Clear();
            uv3List.Clear();
            uv4List.Clear();
            colorsList.Clear();
            trianglesList.Clear();
            triangleIndex = 0;

            return mesh;
        }

        public static float EncodeColor(Color color) {
            return Vector4.Dot(color, new Vector4(1f, 1 / 255f, 1 / 65025.0f, 1 / 16581375.0f));
        }

        private void GenerateCapStart(Vector2[] points, int count, LineCap cap, float strokeWidth, float z) {
            Vector2 p0 = points[0];
            Vector2 p1 = points[1];

            Vector3 v0 = new Vector3();
            Vector3 v1 = new Vector3();
            Vector3 v2 = new Vector3();
            Vector3 v3 = new Vector3();

            Vector2 toP1 = (p1 - p0).normalized;
            Vector2 toP1Perp = new Vector2(-toP1.y, toP1.x);

            switch (cap) {
                case LineCap.Butt:
                    v0 = toP1Perp * strokeWidth * 0.5f;
                    v1 = -toP1Perp * strokeWidth * 0.5f;
                    break;
                case LineCap.Square:
                    break;
                case LineCap.Round:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cap), cap, null);
            }

            positionList.Add(v0);
            positionList.Add(v1);
            positionList.Add(v2);
            positionList.Add(v3);
        }

        private void GenerateJoinedPath(Vector2[] points, int count, Color color, float strokeWidth, float z) {
            //  GenerateCapStart();
            //  GenerateCapEnd();
        }

        private void GenerateSegmentBodies(Vector2[] points, Rect scissor, int count, Color color, float strokeWidth, float z) {
            const int join = 0;

            int renderData = BitUtil.SetHighLowBits((int) SVGXShapeType.Path, RenderTypeStroke);

            uint flags0 = BitUtil.SetBytes(1, VertexType_Near, join, 0);
            uint flags1 = BitUtil.SetBytes(1, VertexType_Far, join, 0);
            uint flags2 = BitUtil.SetBytes(0, VertexType_Near, join, 0);
            uint flags3 = BitUtil.SetBytes(0, VertexType_Far, join, 0);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            
            for (int i = 1; i < count + 1; i++) {
                if (points[i].x < minX) minX = points[i].x;
                if (points[i].x > maxX) maxX = points[i].x;
                if (points[i].y < minY) minY = points[i].y;
                if (points[i].y > maxY) maxY = points[i].y;
            }

            float w = maxX - minX;
            float h = maxY - minY;
            
            Vector4 scissorVector = new Vector4(scissor.x, -scissor.y, scissor.xMax, -scissor.yMax);
            
            for (int i = 1; i < count; i++) {
                Vector2 prev = points[i - 1];
                Vector2 curr = points[i];
                Vector2 next = points[i + 1];
                Vector2 far = points[i + 2];

                positionList.Add(new Vector3(curr.x, -curr.y, z));
                positionList.Add(new Vector3(next.x, -next.y, z));
                positionList.Add(new Vector3(curr.x, -curr.y, z));
                positionList.Add(new Vector3(next.x, -next.y, z));

                uv0List.Add(new Vector4(0, 1, w, h));
                uv0List.Add(new Vector4(1, 1, w, h));
                uv0List.Add(new Vector4(1, 0, w, h));
                uv0List.Add(new Vector4(0, 0, w, h));

                uv1List.Add(new Vector4(renderData, flags0, 1, strokeWidth));
                uv1List.Add(new Vector4(renderData, flags1, 1, strokeWidth));
                uv1List.Add(new Vector4(renderData, flags2, -1, strokeWidth));
                uv1List.Add(new Vector4(renderData, flags3, -1, strokeWidth));

                uv2List.Add(new Vector4(prev.x, -prev.y, next.x, -next.y));
                uv2List.Add(new Vector4(curr.x, -curr.y, far.x, -far.y));
                uv2List.Add(new Vector4(prev.x, -prev.y, next.x, -next.y));
                uv2List.Add(new Vector4(curr.x, -curr.y, far.x, -far.y));

                uv3List.Add(new Vector4(1, 1, 0, 0));
                uv3List.Add(new Vector4(1, 0, 0, 0));
                uv3List.Add(new Vector4(-1, 1, 0, 0));
                uv3List.Add(new Vector4(-1, 0, 0, 0));
                
                uv4List.Add(scissorVector);
                uv4List.Add(scissorVector);
                uv4List.Add(scissorVector);
                uv4List.Add(scissorVector);

                colorsList.Add(color);
                colorsList.Add(color);
                colorsList.Add(color);
                colorsList.Add(color);

                trianglesList.Add(triangleIndex + 0);
                trianglesList.Add(triangleIndex + 1);
                trianglesList.Add(triangleIndex + 2);
                trianglesList.Add(triangleIndex + 2);
                trianglesList.Add(triangleIndex + 1);
                trianglesList.Add(triangleIndex + 3);

                triangleIndex += 4;
            }
        }

        internal void CreateStrokeVertices(Vector2[] points, SVGXRenderShape renderShape, GFX.GradientData gradientData, Rect scissor, SVGXStyle style, SVGXMatrix matrix) {
            int start = renderShape.shape.pointRange.start;
            int triIdx = triangleIndex;
            float strokeWidth = Mathf.Clamp(style.strokeWidth, 1f, style.strokeWidth);
            float z = renderShape.zIndex;
            Color color = style.strokeColor;
            color.a *= style.strokeOpacity;

            Vector4 scissorVector = new Vector4(scissor.x, -scissor.y, scissor.xMax, -scissor.yMax);

            switch (renderShape.shape.type) {
                case SVGXShapeType.Path:
                    break;

                case SVGXShapeType.Unset:
                    return;

                case SVGXShapeType.Sector: {
                    int renderData = BitUtil.SetHighLowBits((int) SVGXShapeType.Path, RenderTypeStrokeShape);
                    Vector2 center = points[start + 0];
                    float startAngle = points[start + 1].x;
                    float endAngle = points[start + 1].y;
                    float radius = points[start + 2].x;
                    float direction = points[start + 2].y;

                    // todo -- in arbeit
                    int idx = 0;
                    for (float theta = startAngle; theta < endAngle; theta++) {
                        float x = radius * Mathf.Cos(theta);
                        float y = radius * Mathf.Sin(theta);
                        AddVertex(new Vector2(center.x + x, center.y + y), Color.white, ++idx);
                        AddVertex(new Vector2(center.x + x, center.y + y), Color.white, ++idx);
                        AddVertex(new Vector2(center.x + x, center.y + y), Color.white, ++idx);
                        CompleteTriangle();
                    }
                    
                    
                    break;
                }
                case SVGXShapeType.RoundedRect: {
                    int renderData = BitUtil.SetHighLowBits((int) renderShape.shape.type, RenderTypeStrokeShape);

                    Vector2 pos = points[start + 0];
                    Vector2 wh = points[start + 1];
                    Vector2 borderRadiusXY = points[start + 2];
                    Vector2 borderRadiusZW = points[start + 3];
                    strokeWidth += 0.1f; // not sure why but stroke isn't right here if 

                    int gradientId = gradientData.rowId;
                    int gradientDirection = 0;

                    uint strokeColorMode = (uint) style.strokeColorMode;

                    if (gradientData.gradient is SVGXLinearGradient linearGradient) {
                        gradientDirection = (int) linearGradient.direction;
                    }

                    // if we use negatives then we can bend the outline without bending the box

                    if (style.strokePlacement == StrokePlacement.Outside) {
                        pos -= new Vector2(strokeWidth - 0.5f, strokeWidth - 0.5f);
                        wh += new Vector2(strokeWidth * 2f, strokeWidth * 2f);
                        borderRadiusXY.x += strokeWidth;
                        borderRadiusXY.y += strokeWidth;
                        borderRadiusZW.x += strokeWidth;
                        borderRadiusZW.y += strokeWidth;
                        wh.x -= 1f;
                        wh.y -= 1;
                    }
                    else if (style.strokePlacement == StrokePlacement.Center) {
                        strokeWidth += 0.1f;
                        pos -= new Vector2(strokeWidth * 0.5f, strokeWidth * 0.5f);
                        wh += new Vector2(strokeWidth, strokeWidth);
                        borderRadiusXY.x += strokeWidth * 0.5f;
                        borderRadiusXY.y += strokeWidth * 0.5f;
                        borderRadiusZW.x += strokeWidth * 0.5f;
                        borderRadiusZW.y += strokeWidth * 0.5f;
                    }
                    else {
                        pos.x -= 1f;
                        pos.y -= 1f;
                        wh.x += 1f;
                        wh.y += 1f;
                    }

                    Vector2 p0 = matrix.Transform(new Vector2(pos.x, pos.y));
                    Vector2 p1 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y));
                    Vector2 p2 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y + wh.y));
                    Vector2 p3 = matrix.Transform(new Vector2(pos.x, pos.y + wh.y));

                    positionList.Add(new Vector3(p0.x, -p0.y, z));
                    positionList.Add(new Vector3(p1.x, -p1.y, z));
                    positionList.Add(new Vector3(p2.x, -p2.y, z));
                    positionList.Add(new Vector3(p3.x, -p3.y, z));

                    trianglesList.Add(triIdx + 0);
                    trianglesList.Add(triIdx + 1);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 3);
                    trianglesList.Add(triIdx + 0);

                    uv0List.Add(new Vector4(0, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 0, wh.x, wh.y));
                    uv0List.Add(new Vector4(0, 0, wh.x, wh.y));

                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));

                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));

                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));

                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);

                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);

                    triangleIndex = triIdx + 4;
                    return;
                }

                case SVGXShapeType.Rect:
                case SVGXShapeType.Circle:
                case SVGXShapeType.Ellipse: {
                    int gradientId = gradientData.rowId;
                    int gradientDirection = 0;

                    uint strokeColorMode = (uint) style.strokeColorMode;

                    if (gradientData.gradient is SVGXLinearGradient linearGradient) {
                        gradientDirection = (int) linearGradient.direction;
                    }

                    // todo for strokes cutout the shape of the center, ie build a mesh that produces less discard ops
                    int renderData = BitUtil.SetHighLowBits((int) renderShape.shape.type, RenderTypeStrokeShape);

                    Vector2 pos = points[start + 0];
                    Vector2 wh = points[start + 1];
                    Vector2 borderRadiusXY = wh;
                    Vector2 borderRadiusZW = wh;

                    if (renderShape.shape.type == SVGXShapeType.Rect) {
                        borderRadiusXY = Vector2.zero;
                        borderRadiusZW = Vector2.zero;
                    }

                    if (style.strokePlacement == StrokePlacement.Outside) {
                        pos -= new Vector2(strokeWidth - 0.5f, strokeWidth - 0.5f);
                        wh += new Vector2(strokeWidth * 2f, strokeWidth * 2f);
                    }
                    else if (style.strokePlacement == StrokePlacement.Center) {
                        pos -= new Vector2(strokeWidth * 0.5f, strokeWidth * 0.5f);
                        wh += new Vector2(strokeWidth, strokeWidth);
                    }
                    else {
                        pos.x -= 1;
                        pos.y -= 1;
                        wh.x += 2;
                        wh.y += 2;
                    }

                    Vector2 p0 = matrix.Transform(new Vector2(pos.x, pos.y));
                    Vector2 p1 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y));
                    Vector2 p2 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y + wh.y));
                    Vector2 p3 = matrix.Transform(new Vector2(pos.x, pos.y + wh.y));

                    positionList.Add(new Vector3(p0.x, -p0.y, z));
                    positionList.Add(new Vector3(p1.x, -p1.y, z));
                    positionList.Add(new Vector3(p2.x, -p2.y, z));
                    positionList.Add(new Vector3(p3.x, -p3.y, z));

                    trianglesList.Add(triIdx + 0);
                    trianglesList.Add(triIdx + 1);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 3);
                    trianglesList.Add(triIdx + 0);

                    uv0List.Add(new Vector4(0, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 0, wh.x, wh.y));
                    uv0List.Add(new Vector4(0, 0, wh.x, wh.y));

                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, strokeColorMode, gradientId, gradientDirection));

                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));

                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));
                    uv3List.Add(new Vector4(strokeWidth, 0, 0, 0));

                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);

                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);

                    triangleIndex = triIdx + 4;
                    return;
                }
                case SVGXShapeType.Text:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            LightList<Vector2> pointCache = LightList<Vector2>.Get();
            SVGXGeometryUtil.GenerateStrokeGeometry(pointCache, style.strokePlacement, style.strokeWidth, renderShape.shape.type, matrix, points, renderShape.shape.pointRange, renderShape.shape.isClosed);

            bool isClosed = renderShape.shape.isClosed;
            const int cap = 1;
            if (isClosed) {
                GenerateSegmentBodies(pointCache.Array, scissor, pointCache.Count - 2, color, strokeWidth, renderShape.zIndex);
            }
            else if (renderShape.shape.pointRange.length == 2) {
                int renderData = BitUtil.SetHighLowBits(DrawType_Stroke, RenderTypeStroke);
                int rangeStart = uv1List.Count;
                GenerateSegmentBodies(pointCache.Array, scissor, pointCache.Count - 2, color, strokeWidth, renderShape.zIndex);
                uv1List[rangeStart + 0] = new Vector4(renderData, BitUtil.SetBytes(1, VertexType_Near, cap, 0), 0, strokeWidth);
                uv1List[rangeStart + 1] = new Vector4(renderData, BitUtil.SetBytes(1, VertexType_Far, cap, 0), 0, strokeWidth);
                uv1List[rangeStart + 2] = new Vector4(renderData, BitUtil.SetBytes(0, VertexType_Near, cap, 0), 0, strokeWidth);
                uv1List[rangeStart + 3] = new Vector4(renderData, BitUtil.SetBytes(0, VertexType_Far, cap, 0), 0, strokeWidth);
            }
            else {
                int rangeStart = uv3List.Count;
                GenerateSegmentBodies(pointCache.Array, scissor, pointCache.Count - 2, color, strokeWidth, renderShape.zIndex);
                uv3List[rangeStart + 0] = new Vector4(1, VertexType_Near, cap, 0);
                uv3List[rangeStart + 2] = new Vector4(-1, VertexType_Near, cap, 0);
                uv3List[uv3List.Count - 3] = new Vector4(uv3List[uv3List.Count - 3].x, uv3List[uv3List.Count - 3].y, cap, 0);
                uv3List[uv3List.Count - 1] = new Vector4(uv3List[uv3List.Count - 1].x, uv3List[uv3List.Count - 1].y, cap, 0);
            }

            LightList<Vector2>.Release(ref pointCache);
        }

        internal void CreateFillVertices(Vector2[] points, SVGXRenderShape renderShape, Rect scissorRect, GFX.GradientData gradientData, SVGXStyle style, SVGXMatrix matrix) {
            int start = renderShape.shape.pointRange.start;
            int end = renderShape.shape.pointRange.end;

            int triIdx = triangleIndex;

            Color color = style.fillColor;

            if (style.fillColorMode == ColorMode.Color) {
                color = style.fillColor;
            }
            else if ((style.fillColorMode & ColorMode.Tint) != 0) {
                color = style.fillTintColor;
            }

            int gradientId = gradientData.rowId;
            int gradientDirection = 0;

            uint fillColorModes = (uint) style.fillColorMode;

            if (gradientData.gradient is SVGXLinearGradient linearGradient) {
                gradientDirection = (int) linearGradient.direction;
            }

            float z = renderShape.zIndex;
            float opacity = style.fillOpacity;
            color.a *= opacity;

            int renderData = BitUtil.SetHighLowBits((int) renderShape.shape.type, RenderTypeFill);
            Vector4 scissorVector = new Vector4(scissorRect.x, -scissorRect.y, scissorRect.xMax, -scissorRect.yMax);

            switch (renderShape.shape.type) {
                case SVGXShapeType.Unset:
                    break;
                case SVGXShapeType.Text: {
                    Vector2 p0 = matrix.Transform(points[start]);
                    renderData = BitUtil.SetHighLowBits((int) renderShape.shape.type, RenderTypeText);

                    TextInfo textInfo = renderShape.textInfo;
//   UIForia doesn't want this to do layout but raw system probably does
//                    if (textInfo.layoutBeforeRender && textInfo.LayoutDirty) {
//                        textInfo.Layout(Vector2.zero);
//                    }
                    
                    CharInfo[] charInfos = textInfo.charInfoList.Array;
                    int charCount = textInfo.CharCount;

                    SVGXTextStyle textStyle = textInfo.spanList[0].textStyle;

                    float outlineWidth = Mathf.Clamp01(textStyle.outlineWidth);
                    float outlineSoftness = textStyle.outlineSoftness;

                    Color32 glowColor = Color.green;
                    float glowOuter = textStyle.glowOuter;
                    float glowOffset = textStyle.glowOffset;

                    Vector4 glowAndRenderData = new Vector4(renderData, new StyleColor(glowColor).rgba, glowOuter, glowOffset);

                    int isStroke = 0;

                    Vector4 outline = new Vector4(outlineWidth, outlineSoftness, VertigoUtil.ColorToFloat(textStyle.outlineColor), 0);

                    Color textColor = style.fillColor;

                    // todo -- clip smarter using layout lines
                    for (int i = 0; i < charCount; i++) {
                        
                        if (charInfos[i].character == ' ') continue;
                        
                        Vector2 topLeft = charInfos[i].layoutTopLeft;
                        Vector2 bottomRight = charInfos[i].layoutBottomRight;
//                        topLeft.x = topLeft.x - 2.5f;
//                        bottomRight.x = bottomRight.x + 2.5f;
//                        topLeft.y = topLeft.y - 2.5f;
//                        bottomRight.y = bottomRight.y + 2.5f;

                        positionList.Add(new Vector3(p0.x + topLeft.x, -p0.y + -bottomRight.y, z)); // Bottom Left
                        positionList.Add(new Vector3(p0.x + topLeft.x, -p0.y + -topLeft.y, z)); // Top Left
                        positionList.Add(new Vector3(p0.x + bottomRight.x, -p0.y + -topLeft.y, z)); // Top Right
                        positionList.Add(new Vector3(p0.x + bottomRight.x, -p0.y + -bottomRight.y, z)); // Bottom Right

                        float x = charInfos[i].uv0.x; // - (5 / 1024f);
                        float y = charInfos[i].uv0.y; // - (5 / 1024f);
                        float x1 = charInfos[i].uv1.x; // + (5 / 1024f);
                        float y1 = charInfos[i].uv1.y; // + (5 / 1024f);

                        uv0List.Add(new Vector4(x, y, charInfos[i].uv2.x, isStroke));
                        uv0List.Add(new Vector4(x, y1, charInfos[i].uv2.x, isStroke));
                        uv0List.Add(new Vector4(x1, y1, charInfos[i].uv2.x, isStroke));
                        uv0List.Add(new Vector4(x1, y, charInfos[i].uv2.x, isStroke));

                        uv1List.Add(glowAndRenderData);
                        uv1List.Add(glowAndRenderData);
                        uv1List.Add(glowAndRenderData);
                        uv1List.Add(glowAndRenderData);

                        uv2List.Add(outline);
                        uv2List.Add(outline);
                        uv2List.Add(outline);
                        uv2List.Add(outline);

                        uv3List.Add(default);
                        uv3List.Add(default);
                        uv3List.Add(default);
                        uv3List.Add(default);
                        
                        uv4List.Add(scissorVector);
                        uv4List.Add(scissorVector);
                        uv4List.Add(scissorVector);
                        uv4List.Add(scissorVector);

                        colorsList.Add(textColor);
                        colorsList.Add(textColor);
                        colorsList.Add(textColor);
                        colorsList.Add(textColor);

                        trianglesList.Add(triangleIndex + 0);
                        trianglesList.Add(triangleIndex + 1);
                        trianglesList.Add(triangleIndex + 2);
                        trianglesList.Add(triangleIndex + 2);
                        trianglesList.Add(triangleIndex + 3);
                        trianglesList.Add(triangleIndex + 0);

                        triangleIndex += 4;
                    }

                    break;
                }
                case SVGXShapeType.Ellipse:
                case SVGXShapeType.Circle:
                case SVGXShapeType.Rect: {
                    Vector2 pos = points[start + 0];
                    Vector2 wh = points[start + 1];

//                    pos.x -= 1f;
//                    pos.y -= 1f;
//                    wh.x += 2f;
//                    wh.y += 2f;
                    Vector2 p0 = matrix.Transform(new Vector2(pos.x, pos.y));
                    Vector2 p1 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y));
                    Vector2 p2 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y + wh.y));
                    Vector2 p3 = matrix.Transform(new Vector2(pos.x, pos.y + wh.y));

                    // todo -- probably doesn't work for rotation
                    Rect r = new Rect(p0.x, p0.y, p1.x - p0.x, p3.y - p1.y);

                    Vector2 uv0 = new Vector2(0, 1);
                    Vector2 uv1 = new Vector2(1, 1);
                    Vector2 uv2 = new Vector2(1, 0);
                    Vector2 uv3 = new Vector2(0, 0);

                    Vector2 v0 = new Vector2(p0.x, p0.y);
                    Vector2 v1 = new Vector2(p1.x, p1.y);
                    Vector2 v2 = new Vector2(p2.x, p2.y);
                    Vector2 v3 = new Vector2(p3.x, p3.y);

                    Rect overlap = r.Intersect(scissorRect);
                    if (overlap.width <= 0 && overlap.height <= 0) {
                        return;
                    }

                    if (p0.x < scissorRect.x) {
                        v0.x = scissorRect.x;
                        v3.x = scissorRect.x;
                        uv0.x = (scissorRect.x - p0.x) / r.width;
                        uv3.x = uv0.x;
                    }

                    if (p0.y < scissorRect.y) {
                        v0.y = scissorRect.y;
                        v1.y = scissorRect.y;
                        uv0.y = 1 - ((scissorRect.y - p0.y) / r.height);
                        uv1.y = uv0.y;
                    }

                    if (p1.x > scissorRect.xMax) {
                        v1.x = scissorRect.xMax;
                        v2.x = scissorRect.xMax;
                        uv1.x = 1 - (p1.x - scissorRect.xMax) / r.width;
                        uv2.x = uv1.x;
                    }

                    if (p3.y > scissorRect.yMax) {
                        v2.y = scissorRect.yMax;
                        v3.y = scissorRect.yMax;
                        uv2.y = (p3.y - scissorRect.yMax) / r.height;
                        uv3.y = (p3.y - scissorRect.yMax) / r.height;
                    }

                    positionList.Add(new Vector3(v0.x, -v0.y, z));
                    positionList.Add(new Vector3(v1.x, -v1.y, z));
                    positionList.Add(new Vector3(v2.x, -v2.y, z));
                    positionList.Add(new Vector3(v3.x, -v3.y, z));

                    trianglesList.Add(triIdx + 0);
                    trianglesList.Add(triIdx + 1);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 3);
                    trianglesList.Add(triIdx + 0);

                    uv0List.Add(new Vector4(uv0.x, uv0.y, wh.x, wh.y));
                    uv0List.Add(new Vector4(uv1.x, uv1.y, wh.x, wh.y));
                    uv0List.Add(new Vector4(uv2.x, uv2.y, wh.x, wh.y));
                    uv0List.Add(new Vector4(uv3.x, uv3.y, wh.x, wh.y));

                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));

                    uv2List.Add(new Vector4(0, 0, 0, 0));
                    uv2List.Add(new Vector4(0, 0, 0, 0));
                    uv2List.Add(new Vector4(0, 0, 0, 0));
                    uv2List.Add(new Vector4(0, 0, 0, 0));

                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());

                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);

                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);

                    triangleIndex = triIdx + 4;

                    break;
                }
                case SVGXShapeType.Path: {
                    // assume closed for now
                    // assume convex for now

                    throw new NotImplementedException();
                }
                case SVGXShapeType.RoundedRect: { // or other convex shape without holes

                    Vector2 pos = points[start + 0];
                    Vector2 wh = points[start + 1];

                    Vector2 borderRadiusXY = points[start + 2];
                    Vector2 borderRadiusZW = points[start + 3];

                    Vector2 p0 = matrix.Transform(new Vector2(pos.x, pos.y));
                    Vector2 p1 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y));
                    Vector2 p2 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y + wh.y));
                    Vector2 p3 = matrix.Transform(new Vector2(pos.x, pos.y + wh.y));

                    positionList.Add(new Vector3(p0.x, -p0.y, z));
                    positionList.Add(new Vector3(p1.x, -p1.y, z));
                    positionList.Add(new Vector3(p2.x, -p2.y, z));
                    positionList.Add(new Vector3(p3.x, -p3.y, z));

                    trianglesList.Add(triIdx + 0);
                    trianglesList.Add(triIdx + 1);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 3);
                    trianglesList.Add(triIdx + 0);

                    uv0List.Add(new Vector4(0, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 0, wh.x, wh.y));
                    uv0List.Add(new Vector4(0, 0, wh.x, wh.y));

                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));
                    uv1List.Add(new Vector4(renderData, fillColorModes, gradientId, gradientDirection));

                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));
                    uv2List.Add(new Vector4(borderRadiusXY.x, borderRadiusXY.y, borderRadiusZW.x, borderRadiusZW.y));

                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());
                    uv3List.Add(new Vector4());

                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);
                    uv4List.Add(scissorVector);

                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);

                    triangleIndex = triIdx + 4;

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal void CreateShadowVertices(Vector2[] points, SVGXRenderShape renderShape, GFX.GradientData gradientData, SVGXStyle style, SVGXMatrix matrix) {
            int start = renderShape.shape.pointRange.start;
            int end = renderShape.shape.pointRange.end;

            int triIdx = triangleIndex;
            float z = renderShape.zIndex;

            int renderData = BitUtil.SetHighLowBits((int) renderShape.shape.type, RenderTypeShadow);

            Color color = style.shadowColor;
            Color shadowTint = style.shadowTint;
            float shadowSoftnessX = style.shadowSoftnessX;
            float shadowSoftnessY = style.shadowSoftnessY;
            float shadowIntensity = style.shadowIntensity;

            switch (renderShape.shape.type) {
                case SVGXShapeType.Unset:
                    break;
                case SVGXShapeType.Rect:
                case SVGXShapeType.Ellipse:
                case SVGXShapeType.Circle:

                    Vector2 pos = points[start + 0];
                    Vector2 wh = points[start + 1];

                    pos.x += style.shadowOffsetX;
                    pos.y += style.shadowOffsetY;
                    pos -= wh * 0.2f;
                    wh += wh * 0.2f; // shader wants a buffer to blur in, user defines a fixed size.
                    // making the size 20% larger fixes expectations of user and shader

                    Vector2 p0 = matrix.Transform(new Vector2(pos.x, pos.y));
                    Vector2 p1 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y));
                    Vector2 p2 = matrix.Transform(new Vector2(pos.x + wh.x, pos.y + wh.y));
                    Vector2 p3 = matrix.Transform(new Vector2(pos.x, pos.y + wh.y));

                    positionList.Add(new Vector3(p0.x, -p0.y, z));
                    positionList.Add(new Vector3(p1.x, -p1.y, z));
                    positionList.Add(new Vector3(p2.x, -p2.y, z));
                    positionList.Add(new Vector3(p3.x, -p3.y, z));

                    trianglesList.Add(triIdx + 0);
                    trianglesList.Add(triIdx + 1);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 2);
                    trianglesList.Add(triIdx + 3);
                    trianglesList.Add(triIdx + 0);

                    uv0List.Add(new Vector4(0, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 1, wh.x, wh.y));
                    uv0List.Add(new Vector4(1, 0, wh.x, wh.y));
                    uv0List.Add(new Vector4(0, 0, wh.x, wh.y));

                    uv1List.Add(new Vector4(renderData, 0, 0, 0));
                    uv1List.Add(new Vector4(renderData, 0, 0, 0));
                    uv1List.Add(new Vector4(renderData, 0, 0, 0));
                    uv1List.Add(new Vector4(renderData, 0, 0, 0));

                    uv2List.Add(new Vector4(shadowSoftnessX, shadowSoftnessY, shadowIntensity, 0));
                    uv2List.Add(new Vector4(shadowSoftnessX, shadowSoftnessY, shadowIntensity, 0));
                    uv2List.Add(new Vector4(shadowSoftnessX, shadowSoftnessY, shadowIntensity, 0));
                    uv2List.Add(new Vector4(shadowSoftnessX, shadowSoftnessY, shadowIntensity, 0));

                    uv3List.Add(shadowTint);
                    uv3List.Add(shadowTint);
                    uv3List.Add(shadowTint);
                    uv3List.Add(shadowTint);

                    uv4List.Add(new Vector4());
                    uv4List.Add(new Vector4());
                    uv4List.Add(new Vector4());
                    uv4List.Add(new Vector4());

                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);
                    colorsList.Add(color);

                    triangleIndex = triIdx + 4;
                    break;
                case SVGXShapeType.RoundedRect:
                    break;
                case SVGXShapeType.Path:
                    break;
                case SVGXShapeType.Text:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Vector2 MidPoint(Vector2 v0, Vector2 v1) {
            return (v0 + v1) * 0.5f;
        }

        public float SignedArea(Vector2 p0, Vector2 p1, Vector2 p2) {
            return (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
        }

        public bool LineIntersection(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, out Vector2 intersection) {
            float a0 = p1.y - p0.y;
            float b0 = p0.x - p1.x;

            float a1 = p3.y - p2.y;
            float b1 = p2.x - p3.x;

            float det = a0 * b1 - a1 * b0;
            if (det > -float.Epsilon && det < float.Epsilon) {
                intersection = Vector2.zero;
                return false;
            }
            else {
                float c0 = a0 * p0.x + b0 * p0.y;
                float c1 = a1 * p2.x + b1 * p2.y;

                float x = (b1 * c0 - b0 * c1) / det;
                float y = (a0 * c1 - a1 * c0) / det;
                intersection = new Vector2(x, y);
                return true;
            }
        }

        // todo -- implement a version that uses a pixel shader instead of geometry
        private void GenerateRoundJoin(Vector2 center, Vector2 p0, Vector2 p1, Vector2 next) {
            float radius = (center - p0).magnitude;

            float angle0 = Mathf.Atan2((p1.y - center.y), (p1.x - center.x));
            float angle1 = Mathf.Atan2((p0.y - center.y), (p0.x - center.x));
            float orgAngle0 = angle0;

            if (angle1 > angle0) {
                if (angle1 - angle0 >= Mathf.PI - float.Epsilon) {
                    angle1 = angle1 - (2f * Mathf.PI);
                }
            }
            else {
                if (angle0 - angle1 >= Mathf.PI - float.Epsilon) {
                    angle0 = angle0 - (2f * Mathf.PI);
                }
            }

            float angleDiff = angle1 - angle0;

            if (Mathf.Abs(angleDiff) >= Mathf.PI - float.Epsilon && Mathf.Abs(angleDiff) <= Mathf.PI + float.Epsilon) {
                Vector2 r1 = center - next;
                if (r1.x == 0) {
                    if (r1.y > 0) {
                        angleDiff = -angleDiff;
                    }
                }
                else if (r1.x >= -float.Epsilon) {
                    angleDiff = -angleDiff;
                }
            }

            // 7 is magic but is a reasonable tessellation threshold
            int segmentCount = (int) ((Mathf.Abs(angleDiff * radius) / 7f) + 1);

            float angleInc = angleDiff / segmentCount;

            for (int i = 0; i < segmentCount; i++) {
                AddVertex(center, Color.yellow, 0);
                // todo can remove a bunch of Sin and Cos calls here by using previous results
                AddVertex(new Vector2(
                    center.x + radius * Mathf.Cos(orgAngle0 + angleInc * i),
                    center.y + radius * Mathf.Sin(orgAngle0 + angleInc * i)
                ), Color.yellow, 0);
                AddVertex(new Vector2(
                    center.x + radius * Mathf.Cos(orgAngle0 + angleInc * (i + 1)),
                    center.y + radius * Mathf.Sin(orgAngle0 + angleInc * (i + 1))
                ), Color.yellow, 0);
                CompleteTriangle();
            }
        }

        // next steps:
        // get culling to work, right now Cull must be off
        // experiment with depth buffer to stop overdraw and behave like html canvas does
        // allow fragment shader to generate round joins
        // implement caps 
        // test miter join
        // only do this for corner segments
        // see if we can stop splitting along midpoint 
        // push all of this to gpu only code

        public void CreateTriangles(Vector2 p0, Vector2 p1, Vector2 p2, float strokeWidth, LineJoin join, int miterLimit) {
            Vector2 t0 = (p1 - p0).Perpendicular();
            Vector2 t2 = (p2 - p1).Perpendicular();

            if (SignedArea(p0, p1, p2) > 0) {
                t0 = t0.Invert();
                t2 = t2.Invert();
            }

            t0 = t0.normalized;
            t2 = t2.normalized;
            t0 *= strokeWidth;
            t2 *= strokeWidth;

            float anchorLength = float.MaxValue;
            Vector2 anchor = new Vector2();
            bool didIntersect = LineIntersection(t0 + p0, t0 + p1, t2 + p2, t2 + p1, out Vector2 pintersect);
            if (didIntersect) {
                anchor = pintersect - p1;
                anchorLength = anchor.magnitude;
//                var ctx = SVGXRoot.CTX;
//                ctx.CircleFromCenter(pintersect, 5f);
//                ctx.SetStroke(Color.black);
//                ctx.Stroke();
            }

            int limit = (int) (anchorLength / strokeWidth);
            Vector2 p0p1 = p0 - p1;
            Vector2 p1p2 = p1 - p2;
            float p0p1Length = p0p1.magnitude;
            float p1p2Length = p1p2.magnitude;
            if (anchorLength > p0p1Length || anchorLength > p1p2Length) {
                Vector2 v4 = p2 + t2;
                Vector2 v5 = p1 - t2;
                Vector2 v6 = p1 + t2;
                Vector2 v7 = p2 - t2;

                AddVertex(v4, Color.red, 0);
                AddVertex(v6, Color.red, 1);
                AddVertex(v7, Color.red, 2);
                AddVertex(v5, Color.red, 3);

                CompleteQuad();

                Vector2 v0 = p0 + t0;
                Vector2 v1 = p0 - t0;
                Vector2 v2 = p1 + t0;
                Vector2 v3 = p1 - t0;

                AddVertex(v0, Color.blue, 0);
                AddVertex(v2, Color.blue, 0);
                AddVertex(v1, Color.blue, 0);
                AddVertex(v3, Color.blue, 0);

                CompleteQuad();

                if (join == LineJoin.Round) {
                    GenerateRoundJoin(p1, p1 + t0, p1 + t2, p2);
                }
                else if (join == LineJoin.Bevel || (join == LineJoin.Miter && limit >= miterLimit)) {
                    AddVertex(p1, Color.yellow, 4);
                    AddVertex(p1 + t0, Color.yellow, 5);
                    AddVertex(p1 + t2, Color.yellow, 6);
                    CompleteTriangle();
                }
                else if (join == LineJoin.Miter && limit < miterLimit && didIntersect) {
                    AddVertex(p1 + t0, Color.yellow, 0);
                    AddVertex(p1, Color.yellow, 1);
                    AddVertex(pintersect, Color.yellow, 2);

                    CompleteTriangle();

                    AddVertex(p1 + t2, Color.yellow, 0);
                    AddVertex(p1, Color.yellow, 1);
                    AddVertex(pintersect, Color.yellow, 2);

                    CompleteTriangle();
                }
            }
            else {
                Vector2 v0 = p0 - t0;
                Vector2 v1 = p1 - anchor;
                Vector2 v2 = p0 + t0;
                Vector2 v3 = p1 + t0;

                AddVertex(v0, Color.blue, 0);
                AddVertex(v1, Color.blue, 1);
                AddVertex(v2, Color.blue, 2);
                AddVertex(v3, Color.blue, 3);

                CompleteQuad();

                Vector2 v4 = p1 - anchor;
                Vector4 v5 = p2 - t2;
                Vector2 v6 = p1 + t2;
                Vector2 v7 = p2 + t2;

                AddVertex(v4, Color.red, 0);
                AddVertex(v5, Color.red, 1);
                AddVertex(v6, Color.red, 2);
                AddVertex(v7, Color.red, 3);

                CompleteQuad();

                if (join == LineJoin.Round) {
                    Vector2 center = p1;
                    Vector2 _p0 = p1 + t0;
                    Vector2 _p1 = p1 + t2;
                    Vector2 _p2 = p1 - anchor;

                    AddVertex(_p0, Color.yellow, 0);
                    AddVertex(center, Color.yellow, 0);
                    AddVertex(_p2, Color.yellow, 0);

                    CompleteTriangle();

                    AddVertex(center, Color.yellow, 0);
                    AddVertex(_p1, Color.yellow, 0);
                    AddVertex(_p2, Color.yellow, 0);

                    CompleteTriangle();

                    GenerateRoundJoin(center, _p0, _p1, _p2);
                }
                else {
                    if (join == LineJoin.Bevel) {
                        AddVertex(p1 + t0, Color.yellow, 0);
                        AddVertex(p1 + t2, Color.yellow, 0);
                        AddVertex(p1 - anchor, Color.yellow, 0);
                        CompleteTriangle();
                    }
                    else if (join == LineJoin.Miter && limit < miterLimit) {
                        AddVertex(p1 + t0, Color.yellow, 0);
                        AddVertex(p1 - anchor, Color.yellow, 0);
                        AddVertex(p1 + t2, Color.yellow, 0);
                        CompleteTriangle();

                        AddVertex(pintersect, Color.yellow, 0);
                        AddVertex(p1 + t0, Color.yellow, 0);
                        AddVertex(p1 + t2, Color.yellow, 0);
                        CompleteTriangle();
                    }
                }
            }
        }

        

        private static bool IsLeft(Vector2 a, Vector2 b, Vector2 c) {
            return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) > 0;
        }

        public static bool LineLineIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
            bool isIntersecting = false;

            float denominator = (p4.y - p3.y) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.y - p1.y);

            //Make sure the denominator is > 0, if so the lines are parallel
            if (Math.Abs(denominator) > float.Epsilon) {
                float u_a = ((p4.x - p3.x) * (p1.y - p3.y) - (p4.y - p3.y) * (p1.x - p3.x)) / denominator;
                float u_b = ((p2.x - p1.x) * (p1.y - p3.y) - (p2.y - p1.y) * (p1.x - p3.x)) / denominator;

                //Is intersecting if u_a and u_b are between 0 and 1
                if (u_a >= 0 && u_a <= 1 && u_b >= 0 && u_b <= 1) {
                    isIntersecting = true;
                }
            }

            return isIntersecting;
        }

        public void GenerateStartCap2() { }

        // joins always work on vertex 1 and 3 from s0 and 0 and 2 from s1
        // this means we need to 'complete' the last segment geometry before creating
        // the join geometry and starting the next one
        // caps should handle the start / end of the first and last segments respectively

        public void GenerateBevelJoin(Segment s0, Segment s1, float strokeWidth) {
            float offset = strokeWidth * 0.5f;

            Vector2 p0 = s0.p0;
            Vector2 p1 = s0.p1;
            Vector2 p2 = s1.p0;
            Vector2 p3 = s1.p1;

            Vector2 toSegment1End = s0.toNextNormalized;
            Vector2 toSegment2End = s1.toNextNormalized;

            Vector2 segment1Perp = new Vector2(-toSegment1End.y, toSegment1End.x);
            Vector2 segment2Perp = new Vector2(-toSegment2End.y, toSegment2End.x);

            Vector2 v1 = p1 - (segment1Perp * offset);
            Vector2 v3 = p1 + (segment1Perp * offset);

            Vector2 v4 = p2 - (segment2Perp * offset);
            Vector2 v6 = p2 - (segment2Perp * offset);

            Vector2 miter = (segment1Perp + segment2Perp).normalized;
            float miterLength = offset / Vector2.Dot(miter, segment2Perp);

            // first we finish the geometry from start segment

            // need to compute the bevel point, first figure out if we are going left or right

            if (IsLeft(p0, p1, p3)) {
                v3 = p1 + (miter * miterLength);
                v6 = v3;

                Vector2 bevel0 = v1;
                Vector2 bevel1 = v3;
                Vector2 bevel2 = v4;

                Vector2 v0 = positionList[positionList.Count - 1];
                Vector2 v2 = positionList[positionList.Count - 2];

                Vector2 iv0 = new Vector2(v0.x, -v0.y);
                Vector2 iv2 = new Vector2(v2.x, -v2.y);

                SVGXRoot.CTX.BeginPath();
                Vector2 toV = (iv2 - iv0).normalized;
                SVGXRoot.CTX.SetStroke(Color.cyan);
                SVGXRoot.CTX.SetStrokeWidth(1f); //Color.cyan);
                SVGXRoot.CTX.MoveTo(iv0);
                SVGXRoot.CTX.LineTo(iv2 + (toV * 200f));
                SVGXRoot.CTX.Stroke();

                if (LineLineIntersect(v1, v3, iv0, iv2)) {
                    v3 = iv2;
                }

                // todo -- if line intersects next end pair need to also relocated bevel center

                AddVertex(v3, Color.blue, 3);
                AddVertex(v1, Color.white, 1);

                CompleteQuad();

                AddVertex(bevel0, Color.yellow, -1);
                AddVertex(bevel1, Color.yellow, -1);
                AddVertex(bevel2, Color.yellow, -1);

                CompleteTriangle();

                AddVertex(v4, Color.red, 0);
                AddVertex(v6, Color.red, 2);
            }
            else {
                // v1 = normal offset position
                // v3 = miter position

                // v4 = normal offset position
                // v6 = v3 (miter position)
                // todo -- figure out this case
                v1 = p1 - (miter * miterLength);
                v4 = v1;

                v6 = p2 + (segment2Perp * offset);

                Vector2 bevel0 = v1;
                Vector2 bevel1 = v3;
                Vector2 bevel2 = v6;

                AddVertex(v3, Color.blue, 1);
                AddVertex(v1, Color.white, 3);
                CompleteQuad();

                AddVertex(bevel0, Color.yellow, -1);
                AddVertex(bevel1, Color.yellow, -1);
                AddVertex(bevel2, Color.yellow, -1);

                CompleteTriangle();

                AddVertex(v4, Color.red, 0);
                AddVertex(v6, Color.red, 2);
            }
        }

        public struct Segment {

            public readonly Vector2 p0;
            public readonly Vector2 p1;
            public readonly Vector2 toNextNormalized;

            public Segment(Vector2 p0, Vector2 p1) {
                this.p0 = p0;
                this.p1 = p1;
                this.toNextNormalized = (p1 - p0).normalized;
            }

        }

       

        private void AddVertex(Vector2 position, Color color, int idx) {
            switch (idx) {
                case 0:
                    uv0List.Add(new Vector4(0, 1, 0, 0));
                    break;
                case 1:
                    uv0List.Add(new Vector4(1, 1, 0, 0));
                    break;
                case 2:
                    uv0List.Add(new Vector4(1, 0, 0, 0));
                    break;
                case 3:
                    uv0List.Add(new Vector4(0, 0, 0, 0));
                    break;
                default:
                    uv0List.Add(new Vector4(0, 0, 0, 0));
                    break;
            }

            positionList.Add(new Vector3(position.x, -position.y, 500)); // todo -- set z
            colorsList.Add(color);
            uv1List.Add(new Vector4());
            uv2List.Add(new Vector4());
            uv3List.Add(new Vector4());
            uv4List.Add(new Vector4());
        }

        private void CompleteQuad() {
            // assume vertex 0 and 2 are added first
            // then vertex 1 and 3

            int triIdx = triangleIndex;

            trianglesList.Add(triIdx + 0);
            trianglesList.Add(triIdx + 1);
            trianglesList.Add(triIdx + 2);
            trianglesList.Add(triIdx + 2);
            trianglesList.Add(triIdx + 1);
            trianglesList.Add(triIdx + 3);

            triangleIndex = triIdx + 4;
        }

        private void CompleteTriangle() {
            int triIdx = triangleIndex;

            trianglesList.Add(triIdx + 0);
            trianglesList.Add(triIdx + 1);
            trianglesList.Add(triIdx + 2);

            triangleIndex = triIdx + 3;
        }

       

    }

}
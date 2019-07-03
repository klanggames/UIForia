using System;
using System.Text;
using SVGX;
using TMPro;
using UIForia.Layout;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Text {

    public class TextInfo {

        public StructList<SpanInfo> spanList;
        public StructList<LineInfo> lineInfoList;
        public StructList<WordInfo> wordInfoList;
        public StructList<CharInfo> charInfoList;
        public StructList<char> characterList;

        // todo -- separate out the position & uv into another array
        // public StructList<TextGeometry> geometryList;
        // struct TextGeometry {
        //     public Vector2 topLeft;
        //     public Vector2 bottomRight;
        //     public Vector2 topLeftUV;
        //     public Vector2 bottomRightUV;
        //     public float scale;
        //}
        
        private Size metrics;
        private bool metricsDirty;
        private bool layoutDirty;
        private float layoutWidth;

        public struct FontAssetData {

            public Texture2D texture;
            public float scaleRatioA;
            public float scaleRatioB;
            public float scaleRatioC;
            public float gradientScale;
            public float textureWidth;
            public float textureHeight;
            public float fontWeightBold;
            public float fontWeightNormal;
            public float scaleX;
            public float scaleY;

        }

        internal FontAssetData fontAssetData;

        public bool LayoutDirty => layoutDirty;

        public float LayoutWidth {
            get => layoutWidth;
            set {
                layoutDirty = true;
                layoutWidth = value;
            }
        }

        public int CharCount => charInfoList.Count;

        public static TMP_FontAsset DefaultFont => TMP_FontAsset.defaultFontAsset;

        public TextInfo(TextSpan span) {
            spanList = new StructList<SpanInfo>(2);
            lineInfoList = new StructList<LineInfo>(2);
            wordInfoList = new StructList<WordInfo>();
            charInfoList = new StructList<CharInfo>();
            characterList = new StructList<char>();
            AppendSpan(span);
            layoutWidth = float.MaxValue;
            layoutDirty = true;
        }

        public TextInfo(params TextSpan[] spans) {
            spanList = new StructList<SpanInfo>(spans.Length);
            lineInfoList = new StructList<LineInfo>(2);
            wordInfoList = new StructList<WordInfo>();
            charInfoList = new StructList<CharInfo>();
            characterList = new StructList<char>();
            for (int i = 0; i < spans.Length; i++) {
                AppendSpan(spans[i]);
            }

            layoutWidth = float.MaxValue;
            layoutDirty = true;
        }

        private static SpanInfo CreateSpanInfo(TextSpan span) {
            SpanInfo spanInfo = new SpanInfo(span.text, span.style);
            if (spanInfo.textStyle.fontAsset == null) {
                spanInfo.textStyle.fontAsset = DefaultFont;
            }

            if (spanInfo.textStyle.fontSize <= 0) {
                spanInfo.textStyle.fontSize = 24;
            }

            Material material = spanInfo.textStyle.fontAsset.material;
//            FontAssetData fontAssetData = new FontAssetData();
//            fontAssetData.texture = spanInfo.textStyle.font.atlas;
//            fontAssetData.textureWidth = fontAssetData.texture.width;
//            fontAssetData.textureWidth = fontAssetData.texture.height;
//            fontAssetData.gradientScale = material.GetFloat("_GradientScale"); 
//            fontAssetData.scaleRatioA = material.GetFloat("_ScaleRatioA"); 
//            fontAssetData.scaleRatioB = material.GetFloat("_ScaleRatioB"); 
//            fontAssetData.scaleRatioC = material.GetFloat("_ScaleRatioC");
//            fontAssetData.scaleX = material.GetFloat("_ScaleX");
//            fontAssetData.scaleX = material.GetFloat("_ScaleY");
//            fontAssetData.fontWeightBold = material.GetFloat("_FontWeightBold");
//            fontAssetData.fontWeightNormal = material.GetFloat("_FontWeightNormal");
         //  span.fontAssetData = fontAssetData;
            // textInfo.SetContent(<Text>'Hello There'</Text><Text style=''/>more text</Text>);
            return spanInfo;
        }

        public void AppendSpan(TextSpan span) {
            int spanIdx = spanList.Count;
            int previousCount = characterList.Count;
            spanList.Add(CreateSpanInfo(span));

            char[] buffer = null;

            // text, transform, or whitespace changed
            int bufferSize = ProcessWrap(span.text, span.CollapseWhiteSpace, span.PreserveNewlines, ref buffer);
            ApplyTextTransform(buffer, bufferSize, span.style.textTransform);

            characterList.EnsureAdditionalCapacity(bufferSize);
            charInfoList.EnsureAdditionalCapacity(bufferSize);
            char[] characters = characterList.Array;

            Array.Copy(buffer, 0, characters, characterList.Count, bufferSize);
            ArrayPool<char>.Release(ref buffer);

            int wordStart = spanList.Count == 1 ? 0 : spanList[spanIdx - 1].wordEnd;

            characterList.Count += bufferSize;
            charInfoList.Count += bufferSize;

            StructList<WordInfo> tempWordList = BreakIntoWords(previousCount, previousCount + bufferSize);

            spanList.Array[spanIdx].charStart = previousCount;
            spanList.Array[spanIdx].charEnd = previousCount + bufferSize;
            spanList.Array[spanIdx].wordStart = wordStart;
            spanList.Array[spanIdx].wordEnd = wordStart + tempWordList.Count;

            wordInfoList.AddRange(tempWordList);
            ComputeCharacterAndWordSizes(spanIdx);

            StructList<WordInfo>.Release(ref tempWordList);
            layoutDirty = true;
            metricsDirty = true;
        }

        public void UpdateSpan(int spanIdx, string text) {
            if (spanIdx >= spanList.Count) {
                AppendSpan(new TextSpan(text));
                return;
            }

            UpdateSpan(spanIdx, new TextSpan(text, spanList[spanIdx].textStyle));
        }

        public SelectionRange AppendToSpan(int spanIndex, char c) {
            UpdateSpan(0, spanList.Array[spanIndex].inputText + c);
            return new SelectionRange();
        }

        public SelectionRange InsertText(SelectionRange selectionRange, char c) {
            return InsertText(selectionRange, c.ToString());
        }

        public SelectionRange InsertText(SelectionRange selectionRange, string c) {
            // todo this can be optimized to not re-compute the whole text metrics
            SpanInfo spanInfo = spanList.Array[0];

            if (spanInfo.inputText.Length == 0) {
                UpdateSpan(0, c);
                return new SelectionRange(0, TextEdge.Right);
            }

            if (selectionRange.HasSelection) {
                selectionRange = DeleteTextForwards(selectionRange);
                spanInfo = spanList.Array[0];
            }

            int cursorIndex = selectionRange.cursorIndex;
            if (selectionRange.cursorIndex == spanInfo.CharCount - 1) {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    string text = spanInfo.inputText.Substring(0, cursorIndex);
                    string endText = spanInfo.inputText.Substring(cursorIndex);
                    string newText = text + c + endText;
                    UpdateSpan(0, newText);
                }
                else {
                    UpdateSpan(0, spanInfo.inputText + c);
                    cursorIndex++;
                }
            }
            else if (selectionRange.cursorIndex == spanInfo.charStart) {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    UpdateSpan(0, c + spanInfo.inputText);
                }
                else {
                    string text = spanInfo.inputText.Substring(0, cursorIndex);
                    string endText = spanInfo.inputText.Substring(cursorIndex);
                    UpdateSpan(0, text + c + endText);
                    cursorIndex++;
                }
            }
            else {
                // todo optimize not to use substring here
                if (selectionRange.cursorEdge == TextEdge.Right) {
                    cursorIndex++;
                }

                string text = spanInfo.inputText.Substring(0, cursorIndex);
                string endText = spanInfo.inputText.Substring(cursorIndex);
                string newText = text + c + endText;
                UpdateSpan(0, newText);
            }

            return new SelectionRange(cursorIndex, TextEdge.Right);
        }

        public SelectionRange DeleteTextForwards(SelectionRange range) {
            int spanIndex = 0;

            // todo -- fix this.
            // todo -- we need to compute span indices across the range and nuke accordingly. 
            // todo -- need to figure out if we leave empty spans or delete them, probably keep them

            if (CharCount == 0) {
                return range;
            }

            int cursorIndex = Mathf.Clamp(range.cursorIndex, 0, CharCount);

            // todo -- fix this for multi-span
            string text = spanList[0].inputText;

            if (range.HasSelection) {
                int min = (range.cursorIndex < range.selectIndex ? range.cursorIndex : range.selectIndex);
                int max = (range.cursorIndex > range.selectIndex ? range.cursorIndex : range.selectIndex);

                if (range.selectEdge == TextEdge.Right) {
                    max++;
                }

                string part0 = text.Substring(0, min);
                string part1 = text.Substring(max);
                UpdateSpan(spanIndex, part0 + part1);

                if (range.selectEdge == TextEdge.Right) {
                    if (min - 1 < 0) {
                        return new SelectionRange(0, TextEdge.Left);
                    }

                    return new SelectionRange(min - 1, TextEdge.Right);
                }

                return new SelectionRange(min, TextEdge.Left);
            }
            else {
                if (cursorIndex == CharCount - 1 && range.cursorEdge == TextEdge.Right) {
                    return range;
                }
                else {
                    if (cursorIndex == CharCount - 1) {
                        UpdateSpan(spanIndex, text.Remove(CharCount - 1));
                        return new SelectionRange(CharCount - 1, TextEdge.Right);
                    }
                    else {
                        string part0 = text.Substring(0, cursorIndex);
                        string part1 = text.Substring(cursorIndex + 1);
                        UpdateSpan(spanIndex, part0 + part1);
                        return new SelectionRange(cursorIndex, TextEdge.Left);
                    }
                }
            }
        }

        public SelectionRange DeleteTextBackwards(SelectionRange range) {
            if (CharCount == 0) {
                return new SelectionRange(0, TextEdge.Left);
            }

            SpanInfo spanInfo = spanList.Array[0];

            int cursorIndex = range.cursorIndex;

            if (range.HasSelection) {
                int min = (range.cursorIndex < range.selectIndex ? range.cursorIndex : range.selectIndex);
                int max = (range.cursorIndex > range.selectIndex ? range.cursorIndex : range.selectIndex);

                if (max - min == CharCount - 1) {
                    UpdateSpan(0, string.Empty);
                    return new SelectionRange(0, TextEdge.Left);
                }

                if (range.selectEdge == TextEdge.Right) {
                    max++;
                }

                string part0 = spanInfo.inputText.Substring(0, min);
                string part1 = spanInfo.inputText.Substring(max);
                UpdateSpan(0, part0 + part1);
                if (min == CharCount) {
                    return new SelectionRange(min - 1, TextEdge.Right);
                }

                return new SelectionRange(min, TextEdge.Left);
            }
            else {
                if (cursorIndex == 0 && range.cursorEdge == TextEdge.Left) {
                    return range;
                }

                // assume same line for the moment
                // todo this might be broken
                if (range.cursorEdge == TextEdge.Left) {
                    cursorIndex--;
                }

                cursorIndex = Mathf.Max(0, cursorIndex);

                if (cursorIndex == spanInfo.charStart) {
                    UpdateSpan(0, spanInfo.inputText.Substring(1));
                    return new SelectionRange(0, TextEdge.Left);
                }
                else if (cursorIndex == CharCount - 1) {
                    UpdateSpan(0, spanInfo.inputText.Substring(0, spanInfo.inputText.Length - 1));
                    return new SelectionRange(range.cursorIndex - 1, TextEdge.Right);
                }
                else {
                    string part0 = spanInfo.inputText.Substring(0, cursorIndex);
                    string part1 = spanInfo.inputText.Substring(cursorIndex + 1);
                    UpdateSpan(0, part0 + part1);

                    return new SelectionRange(cursorIndex - 1, TextEdge.Right);
                }
            }
        }

        public void UpdateSpan(int spanIdx, string text, SVGXTextStyle textStyle) {
            if (spanIdx >= spanList.Count) {
                AppendSpan(new TextSpan(text));
                return;
            }

            UpdateSpan(spanIdx, new TextSpan(text, textStyle));
        }

        public void UpdateSpan(int spanIdx, TextSpan span) {
            if (spanIdx >= spanList.Count) {
                AppendSpan(span);
                return;
            }

            SpanInfo old = spanList.Array[spanIdx];

            spanList.Array[spanIdx] = CreateSpanInfo(span);

            char[] buffer = null;

            int bufferSize = ProcessWrap(span.text, span.CollapseWhiteSpace, span.PreserveNewlines, ref buffer);
            ApplyTextTransform(buffer, bufferSize, span.style.textTransform);

            if (bufferSize > old.CharCount) {
                characterList.ShiftRight(old.charEnd, bufferSize - old.CharCount);
                charInfoList.ShiftRight(old.charEnd, bufferSize - old.CharCount);
                Array.Copy(buffer, 0, characterList.Array, old.charStart, bufferSize);
            }
            else if (bufferSize < old.CharCount) {
                characterList.ShiftLeft(old.charEnd, old.CharCount - bufferSize);
                charInfoList.ShiftLeft(old.charEnd, old.CharCount - bufferSize);
                Array.Copy(buffer, 0, characterList.Array, old.charStart, bufferSize);
            }
            else {
                Array.Copy(buffer, 0, characterList.Array, old.charStart, bufferSize);
            }

            StructList<WordInfo> tempWordList = BreakIntoWords(old.charStart, old.charStart + bufferSize);

            int wordStart = old.wordStart;

            int charDiff = bufferSize - old.CharCount;
            int wordDiff = tempWordList.Count - old.WordCount;
            SpanInfo[] spans = spanList.Array;

            if (wordDiff > 0) {
                wordInfoList.ShiftRight(old.wordEnd, wordDiff);
            }
            else if (wordDiff < 0) {
                wordInfoList.ShiftLeft(old.wordEnd, -wordDiff);
            }

            WordInfo[] wordInfos = wordInfoList.Array;
            Array.Copy(tempWordList.Array, 0, wordInfos, old.wordStart, tempWordList.Count);

            for (int i = spanIdx + 1; i < spanList.Count; i++) {
                spans[i].charStart += charDiff;
                spans[i].charEnd += charDiff;
                spans[i].wordStart += wordDiff;
                spans[i].wordEnd += wordDiff;
                int ws = spans[i].wordStart;
                int we = spans[i].wordEnd;
                for (int j = ws; j < we; j++) {
                    wordInfos[j].startChar += charDiff;
                }
            }

            spans[spanIdx].charStart = old.charStart;
            spans[spanIdx].charEnd = old.charStart + bufferSize;
            spans[spanIdx].wordStart = wordStart;
            spans[spanIdx].wordEnd = wordStart + tempWordList.Count;

            ComputeCharacterAndWordSizes(spanIdx);
            layoutDirty = true;
            metricsDirty = true;
            StructList<WordInfo>.Release(ref tempWordList);
        }

        // for debugging only
        internal string GetWord(int word, WordInfo[] array = null) {
            if (array == null) array = wordInfoList.Array;
            string retn = "";
            WordInfo wordInfo = array[word];
            for (int i = wordInfo.startChar; i < wordInfo.startChar + wordInfo.charCount; i++) {
                retn += charInfoList[i].character;
            }

            return retn;
        }

        public void RemoveSpan(int idx) {
            metricsDirty = true;
            layoutDirty = true;
        }

        private StructList<WordInfo> BreakIntoWords(int characterStart, int characterEnd) {
            StructList<WordInfo> tempWordList = StructList<WordInfo>.Get();

            WordInfo currentWord = new WordInfo();
            currentWord.startChar = characterStart;

            bool inWhiteSpace = false;
            CharInfo[] charInfos = charInfoList.Array;
            char[] buffer = characterList.Array;
            for (int i = characterStart; i < characterEnd; i++) {
                int charCode = buffer[i];
                charInfos[i].character = (char) charCode;

                if ((char) charCode == '\n') {
                    if (currentWord.charCount > 0) {
                        tempWordList.Add(currentWord);
                        currentWord = new WordInfo();
                        currentWord.startChar = i;
                    }

                    currentWord.charCount = 1;
                    currentWord.spaceStart = 0;
                    currentWord.isNewLine = true;
                    tempWordList.Add(currentWord);
                    currentWord = new WordInfo();
                    currentWord.startChar = i + 1;
                }

                if (!char.IsWhiteSpace((char) charCode)) {
                    if (inWhiteSpace) {
                        // new word starts
                        tempWordList.Add(currentWord);
                        currentWord = new WordInfo();
                        currentWord.startChar = i;
                        inWhiteSpace = false;
                    }

                    currentWord.charCount++;
                }
                else {
                    if (!inWhiteSpace) {
                        inWhiteSpace = true;
                        currentWord.spaceStart = currentWord.charCount;
                    }

                    currentWord.charCount++;
                }
            }

            if (!inWhiteSpace) {
                currentWord.spaceStart = currentWord.charCount;
            }

            tempWordList.Add(currentWord);

            return tempWordList;
        }

        public float ComputeWidth() {
            if (spanList.Count == 0) return 0;

            StructList<LineInfo> lineInfos = RunLayout();

            float maxWidth = 0;

            for (int i = 0; i < lineInfos.Count; i++) {
                maxWidth = Mathf.Max(maxWidth, lineInfos[i].width);
            }

            StructList<LineInfo>.Release(ref lineInfos);

            return maxWidth;
        }

        public float ComputeHeight(float width) {
            if (spanList.Count == 0) return 0;
            StructList<LineInfo> lineInfos = RunLayout(width);
            LineInfo lastLine = lineInfos[lineInfos.Count - 1];
            float height = lastLine.position.y + lastLine.height;
            StructList<LineInfo>.Release(ref lineInfos);
            return height;
        }

        public Size ComputeMetrics(float width = -1) {
            if (spanList.Count == 0) return default;
            if (!metricsDirty) return metrics;
            StructList<LineInfo> lineInfos = RunLayout(width);
            LineInfo lastLine = lineInfos[lineInfos.Count - 1];
            float height = lastLine.position.y + lastLine.height;
            float maxWidth = 0;

            for (int i = 0; i < lineInfos.Count; i++) {
                maxWidth = Mathf.Max(maxWidth, lineInfos[i].width);
            }

            StructList<LineInfo>.Release(ref lineInfos);
            metricsDirty = false;
            return new Size(maxWidth, height);
        }

        public Size Layout(Vector2 offset = default, float width = float.MaxValue) {
            
            if (spanList.Count == 0) return default;

            lineInfoList.Clear();

            RunLayout(width, lineInfoList);
            LineInfo lastLine = lineInfoList[lineInfoList.Count - 1];
            float maxWidth = 0;

            LineInfo[] lineInfos = lineInfoList.Array;
            for (int i = 0; i < lineInfoList.Count; i++) {
                lineInfoList.Array[i].position = new Vector2(
                    lineInfos[i].position.x + offset.x,
                    lineInfos[i].position.y + offset.y
                );
                maxWidth = Mathf.Max(maxWidth, lineInfos[i].width);
            }

            metricsDirty = false;
            layoutDirty = false;
            float height = lastLine.position.y + lastLine.height;
            // todo -- handle alignment across multiple spans
            // ApplyTextAlignment(maxWidth, style.TextAlignment);
            ApplyLineAndWordOffsets();
            metrics = new Size(maxWidth, height);
            return metrics;
        }

        private void ApplyLineAndWordOffsets() {
            LineInfo[] lineInfos = lineInfoList.Array;
            WordInfo[] wordInfos = wordInfoList.Array;
            CharInfo[] charInfos = charInfoList.Array;

            for (int lineIdx = 0; lineIdx < lineInfoList.Count; lineIdx++) {
                LineInfo currentLine = lineInfos[lineIdx];
                float lineOffset = currentLine.position.y;
                float wordAdvance = currentLine.position.x;

                for (int w = currentLine.wordStart; w < currentLine.wordStart + currentLine.wordCount; w++) {
                    WordInfo currentWord = wordInfos[w];
                    currentWord.lineIndex = lineIdx;
                    currentWord.position = new Vector2(wordAdvance, currentLine.position.y);

                    for (int i = currentWord.startChar; i < currentWord.startChar + currentWord.charCount; i++) {
                        float x0 = charInfos[i].topLeft.x + wordAdvance;
                        float x1 = charInfos[i].bottomRight.x + wordAdvance;
                        float y0 = charInfos[i].topLeft.y + lineOffset;
                        float y1 = charInfos[i].bottomRight.y + lineOffset;
                        charInfos[i].wordIndex = w;
                        charInfos[i].lineIndex = lineIdx;
                        charInfos[i].layoutTopLeft = new Vector2(x0, y0);
                        charInfos[i].layoutBottomRight = new Vector2(x1, y1);
                    }

                    wordInfos[w] = currentWord;
                    wordAdvance += currentWord.xAdvance;
                }
            }
        }

        private StructList<LineInfo> RunLayout(float width = float.MaxValue, StructList<LineInfo> lineInfos = null) {
            int spanCount = spanList.Count;
            WordInfo[] wordInfos = wordInfoList.Array;
            SpanInfo[] spanInfos = spanList.Array;

            LineInfo currentLine = new LineInfo();

            lineInfos = lineInfos ?? StructList<LineInfo>.Get();

            for (int i = 0; i < spanCount; i++) {
                SpanInfo spanInfo = spanInfos[i];
                TMP_FontAsset asset = spanInfo.textStyle.fontAsset;

                float scale = (spanInfo.textStyle.fontSize / asset.fontInfo.PointSize) * asset.fontInfo.Scale;
                float lh = (asset.fontInfo.Ascender - asset.fontInfo.Descender) * scale;
                float lineOffset = 0;

                currentLine.height = currentLine.height > lh ? currentLine.height : lh;

                for (int w = spanInfo.wordStart; w < spanInfo.wordEnd; w++) {
                    WordInfo currentWord = wordInfos[w];

                    if (currentWord.isNewLine) {
                        lineInfos.Add(currentLine);
                        lineOffset += lh;
                        currentLine = new LineInfo(w + 1, new Vector2(0, lineOffset), lh);
                        continue;
                    }

                    if (currentWord.characterSize > width + 0.01f) {
                        // we had words in this line already
                        // finish the line and start a new one
                        // line offset needs to to be bumped
                        if (currentLine.wordCount > 0) {
                            lineInfos.Add(currentLine);
                            lineOffset += lh;
                        }

                        currentLine = new LineInfo(new RangeInt(w, 1), new Vector2(0, lineOffset), new Size(currentWord.characterSize, lh));
                        lineInfos.Add(currentLine);

                        lineOffset += lh;
                        currentLine = new LineInfo(w + 1, new Vector2(0, lineOffset), lh);
                    }

                    else if (currentLine.width + currentWord.size.x > width + 0.01f) {
                        // characters fit but space does not, strip spaces and start new line w/ next word
                        if (currentLine.width + currentWord.characterSize < width + 0.01f) {
                            currentLine.wordCount++;
                            currentLine.width += currentWord.characterSize;
                            lineInfos.Add(currentLine);

                            lineOffset += lh;

                            currentLine = new LineInfo(new RangeInt(w + 1, 0), new Vector2(0, lineOffset), new Size(0, lh));
                            continue;
                        }

                        lineInfos.Add(currentLine);
                        lineOffset += lh;
                        currentLine = new LineInfo(new RangeInt(w, 1), new Vector2(0, lineOffset), new Size(currentWord.size.x, lh));
                    }

                    else {
                        currentLine.wordCount++;
                        currentLine.width += currentWord.xAdvance;
                    }
                }
            }

            if (currentLine.wordCount > 0) {
                lineInfos.Add(currentLine);
            }

            return lineInfos;
        }

        private static TMP_FontAsset GetFontAssetForWeight(FontStyle fontStyle, TMP_FontAsset font, int fontWeight) {
            bool isItalic = (fontStyle & FontStyle.Italic) != 0;

            int weightIndex = fontWeight / 100;
            TMP_FontWeights weights = font.fontWeights[weightIndex];
            return isItalic ? weights.italicTypeface : weights.regularTypeface;
        }

        private void ComputeCharacterAndWordSizes(int spanIdx) {
            WordInfo[] wordInfos = wordInfoList.Array;
            CharInfo[] charInfos = charInfoList.Array;

            SpanInfo spanInfo = spanList[spanIdx];
            TMP_FontAsset fontAsset = spanInfo.textStyle.fontAsset;
            Material fontAssetMaterial = fontAsset.material;

            bool isUsingAltTypeface = false;
            float boldAdvanceMultiplier = 1;

            if ((spanInfo.textStyle.fontStyle & FontStyle.Bold) != 0) {
                fontAsset = GetFontAssetForWeight(spanInfo.textStyle.fontStyle, spanInfo.textStyle.fontAsset, 700);
                isUsingAltTypeface = true;
                boldAdvanceMultiplier = 1 + fontAsset.boldSpacing * 0.01f;
            }

            float smallCapsMultiplier = (spanInfo.textStyle.fontStyle & FontStyle.SmallCaps) == 0 ? 1.0f : 0.8f;
            float fontScale = spanInfo.textStyle.fontSize * smallCapsMultiplier / fontAsset.fontInfo.PointSize * fontAsset.fontInfo.Scale;

            //float yAdvance = fontAsset.fontInfo.Baseline * fontScale * fontAsset.fontInfo.Scale;
            //float monoAdvance = 0;

            float minWordSize = float.MaxValue;
            float maxWordSize = float.MinValue;

            float padding = ShaderUtilities.GetPadding(fontAsset.material, enableExtraPadding: false, isBold: false);
            float stylePadding = 0;

            if (!isUsingAltTypeface && (spanInfo.textStyle.fontStyle & FontStyle.Bold) == FontStyle.Bold) {
                if (fontAssetMaterial.HasProperty(ShaderUtilities.ID_GradientScale)) {
                    float gradientScale = fontAssetMaterial.GetFloat(ShaderUtilities.ID_GradientScale);
                    stylePadding = fontAsset.boldStyle / 4.0f * gradientScale * fontAssetMaterial.GetFloat(ShaderUtilities.ID_ScaleRatio_A);

                    // Clamp overall padding to Gradient Scale size.
                    if (stylePadding + padding > gradientScale) {
                        padding = gradientScale - stylePadding;
                    }
                }

                boldAdvanceMultiplier = 1 + fontAsset.boldSpacing * 0.01f;
            }
            else if (fontAssetMaterial.HasProperty(ShaderUtilities.ID_GradientScale)) {
                float gradientScale = fontAssetMaterial.GetFloat(ShaderUtilities.ID_GradientScale);
                stylePadding = fontAsset.normalStyle / 4.0f * gradientScale *
                               fontAssetMaterial.GetFloat(ShaderUtilities.ID_ScaleRatio_A);

                // Clamp overall padding to Gradient Scale size.
                if (stylePadding + padding > gradientScale) {
                    padding = gradientScale - stylePadding;
                }
            }

            // todo -- handle tab
            // todo -- handle sprites

            int charCount = charInfoList.Count;

            for (int w = spanInfo.wordStart; w < spanInfo.wordEnd; w++) {
                WordInfo currentWord = wordInfos[w];
                float xAdvance = 0;
                // new lines are their own words (idea: give them an xAdvance of some huge number so they always get their own lines)

                for (int i = currentWord.startChar; i < currentWord.startChar + currentWord.charCount; i++) {
                    int current = charInfos[i].character;

                    TMP_Glyph glyph;
                    TMP_FontAsset fontForGlyph = TMP_FontUtilities.SearchForGlyph(spanInfo.textStyle.fontAsset, charInfos[i].character, out glyph);

                    if (glyph == null) {
                        Debug.Log($"The character {charInfos[i].character} isn't available in font {spanInfo.textStyle.fontAsset.name}.");
                        continue;
                    }

                    KerningPair adjustmentPair;
                    GlyphValueRecord glyphAdjustments = new GlyphValueRecord();

                    // todo -- if we end up doing character wrapping we probably want to ignore prev x kerning for line start
                    if (i != charCount - 1) {
                        int next = charInfos[i + 1].character;
                        fontAsset.kerningDictionary.TryGetValue((next << 16) + current, out adjustmentPair);
                        if (adjustmentPair != null) {
                            glyphAdjustments = adjustmentPair.firstGlyphAdjustments;
                        }
                    }

                    if (i != 0) {
                        int prev = charInfos[i - 1].character;
                        fontAsset.kerningDictionary.TryGetValue((current << 16) + prev, out adjustmentPair);
                        if (adjustmentPair != null) {
                            glyphAdjustments += adjustmentPair.secondGlyphAdjustments;
                        }
                    }

                    float currentElementScale = fontScale * glyph.scale;
                    float topShear = 0;
                    float bottomShear = 0;

                    if (!isUsingAltTypeface && ((spanInfo.textStyle.fontStyle & FontStyle.Italic) != 0)) {
                        float shearValue = fontAsset.italicStyle * 0.01f;
                        topShear = glyph.yOffset * shearValue;
                        bottomShear = (glyph.yOffset - glyph.height) * shearValue;
                    }

                    Vector2 topLeft;
                    Vector2 bottomRight;

                    // idea for auto sizing: multiply scale later on and just save base unscaled vertices
//                        topLeft.x = xAdvance + (glyph.xOffset - padding - stylePadding + glyphAdjustments.xPlacement) * currentElementScale;
//                        topLeft.y = yAdvance + (glyph.yOffset + padding + glyphAdjustments.yPlacement) * currentElementScale;
//                        bottomRight.x = topLeft.x + (glyph.width + padding * 2) * currentElementScale;
//                        bottomRight.y = topLeft.y - (glyph.height + padding * 2 + stylePadding * 2) * currentElementScale;

                    topLeft.x = xAdvance + (glyph.xOffset - padding - stylePadding + glyphAdjustments.xPlacement) * currentElementScale;
                    topLeft.y = ((fontAsset.fontInfo.Ascender) - (glyph.yOffset + padding)) * currentElementScale;
                    bottomRight.x = topLeft.x + (glyph.width + padding * 2) * currentElementScale;
                    bottomRight.y = topLeft.y + (glyph.height + padding * 2 + stylePadding * 2) * currentElementScale;

                    charInfos[i].scale = currentElementScale;
                    
                    if (currentWord.startChar + currentWord.VisibleCharCount >= i) {
                        if (topLeft.y > currentWord.maxCharTop) {
                            currentWord.maxCharTop = topLeft.y;
                        }

                        if (bottomRight.y < currentWord.minCharBottom) {
                            currentWord.minCharBottom = bottomRight.y;
                        }
                    }

                    FaceInfo faceInfo = fontAsset.fontInfo;
                    Vector2 uv0;

                    uv0.x = (glyph.x - padding - stylePadding) / faceInfo.AtlasWidth;
                    uv0.y = 1 - (glyph.y + padding + stylePadding + glyph.height) / faceInfo.AtlasHeight;

                    Vector2 uv1;
                    uv1.x = (glyph.x + padding + stylePadding + glyph.width) / faceInfo.AtlasWidth;
                    uv1.y = 1 - (glyph.y - padding - stylePadding) / faceInfo.AtlasHeight;

                    charInfos[i].topLeft = topLeft;
                    charInfos[i].bottomRight = bottomRight;
                    charInfos[i].shearValues = new Vector2(topShear, bottomShear);

                    charInfos[i].uv0 = uv0;
                    charInfos[i].uv1 = uv1;

                    charInfos[i].uv2 = new Vector2(currentElementScale, 0); // todo -- compute uv2s
                    charInfos[i].uv3 = Vector2.one;

                    float elementAscender = fontAsset.fontInfo.Ascender * currentElementScale / smallCapsMultiplier;
                    float elementDescender = fontAsset.fontInfo.Descender * currentElementScale / smallCapsMultiplier;

                    charInfos[i].ascender = elementAscender;
                    charInfos[i].descender = elementDescender;

                    currentWord.ascender = elementAscender > currentWord.ascender
                        ? elementAscender
                        : currentWord.ascender;
                    currentWord.descender = elementDescender < currentWord.descender
                        ? elementDescender
                        : currentWord.descender;

                    if ((spanInfo.textStyle.fontStyle & (FontStyle.Superscript | FontStyle.Subscript)) != 0) {
                        float baseAscender = elementAscender / fontAsset.fontInfo.SubSize;
                        float baseDescender = elementDescender / fontAsset.fontInfo.SubSize;

                        currentWord.ascender = baseAscender > currentWord.ascender
                            ? baseAscender
                            : currentWord.ascender;
                        currentWord.descender = baseDescender < currentWord.descender
                            ? baseDescender
                            : currentWord.descender;
                    }

                    if (i < currentWord.startChar + currentWord.spaceStart) {
                        currentWord.characterSize = charInfos[i].bottomRight.x;
                    }

                    xAdvance += (glyph.xAdvance
                                 * boldAdvanceMultiplier
                                 + fontAsset.normalSpacingOffset
                                 + glyphAdjustments.xAdvance) * currentElementScale;
                }

                currentWord.xAdvance = xAdvance;
                currentWord.size = new Vector2(xAdvance, currentWord.ascender); // was ascender - descender
                minWordSize = Mathf.Min(minWordSize, currentWord.size.x);
                maxWordSize = Mathf.Max(maxWordSize, currentWord.size.x);
                wordInfos[w] = currentWord;
            }
        }

        public void SetSpanStyle(int index, SVGXTextStyle svgxTextStyle) {
            spanList.Array[index].textStyle = svgxTextStyle;
            if (spanList.Array[index].textStyle.fontAsset == null) {
                spanList.Array[index].textStyle.fontAsset = DefaultFont;
            }

            if (spanList.Array[index].textStyle.fontSize <= 0) {
                spanList.Array[index].textStyle.fontSize = 24;
            }

            ComputeCharacterAndWordSizes(index);
            metricsDirty = true;
        }

        public void UpdateSpanStyle(int spanIdx, SVGXTextStyle spanStyle) {
            SVGXTextStyle oldStyle = spanList.Array[spanIdx].textStyle;

            bool whitespaceChanged = oldStyle.whitespaceMode != spanStyle.whitespaceMode;
            bool transformChanged = oldStyle.textTransform != spanStyle.textTransform;
            bool fontStyleChanged = oldStyle.fontStyle != spanStyle.fontStyle;
            bool needsGlyphUpdate = fontStyleChanged || oldStyle.fontAsset != spanStyle.fontAsset || oldStyle.fontSize != spanStyle.fontSize || transformChanged;


            if (whitespaceChanged || transformChanged) {
                UpdateSpan(spanIdx, new TextSpan(spanList.Array[spanIdx].inputText, spanStyle));
                return;
            }

            if (spanStyle.fontAsset == null) {
                spanStyle.fontAsset = DefaultFont;
            }

            if (spanStyle.fontSize <= 0) {
                spanStyle.fontSize = 18;
            }

            spanList.Array[spanIdx].textStyle = spanStyle;

            if (needsGlyphUpdate) {
                ComputeCharacterAndWordSizes(spanIdx);
                layoutDirty = true;
                metricsDirty = true;
            }
        }

        private TextEdge FindCursorEdge(int charIndex, Vector2 point) {
            if (charInfoList.Count == 0 || charIndex >= charInfoList.Count) {
                return TextEdge.Left;
            }

            Vector2 topLeft = charInfoList.Array[charIndex].layoutTopLeft;
            Vector2 bottomRight = charInfoList.Array[charIndex].layoutBottomRight;
            float width = bottomRight.x - topLeft.x;

            if (point.x > topLeft.x + (width * 0.5f)) {
                return TextEdge.Right;
            }

            return TextEdge.Left;
        }

        private int FindNearestCharacterIndex(Vector2 point) {
            int nearestLine = FindNearestLine(point);
            int nearestWord = FindNearestWord(nearestLine, point);
            return FindNearestCharacterIndex(nearestWord, point);
        }

        private int FindNearestWord(int lineIndex, Vector2 point) {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            LineInfo line = lineInfoList.Array[lineIndex];
            WordInfo[] wordInfos = wordInfoList.Array;
            for (int i = line.wordStart; i < line.wordStart + line.wordCount; i++) {
                WordInfo word = wordInfos[i];
                float x1 = word.position.x;
                float x2 = word.position.x + word.xAdvance;
                if (point.x >= x1 && point.x <= x2) {
                    return i;
                }

                float distToX1 = Mathf.Abs(point.x - x1);
                float distToX2 = Mathf.Abs(point.x - x2);
                if (distToX1 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToX1;
                }

                if (distToX2 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToX2;
                }
            }

            return closestIndex;
        }

        private int FindNearestCharacterIndex(int wordIndex, Vector2 point) {
            WordInfo wordInfo = wordInfoList.Array[wordIndex];
            int closestIndex = wordInfo.startChar;
            float closestDistance = float.MaxValue;
            CharInfo[] charInfos = charInfoList.Array;

            int start = wordInfo.startChar;
            int end = start + wordInfo.charCount;

            for (int i = start; i < end; i++) {
                float x1 = charInfos[i].layoutTopLeft.x;
                float x2 = charInfos[i].layoutBottomRight.x;

                if (point.x >= x1 && point.x <= x2) {
                    return i;
                }

                float distToY1 = Mathf.Abs(point.x - x1);
                float distToY2 = Mathf.Abs(point.x - x2);
                if (distToY1 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToY1;
                }

                if (distToY2 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToY2;
                }
            }

            return closestIndex;
        }

        private int FindNearestLine(Vector2 point) {
            int lineCount = lineInfoList.Count;
            LineInfo[] lineInfos = lineInfoList.Array;

            if (point.y <= lineInfos[0].position.y) {
                return 0;
            }

            if (point.y >= lineInfos[lineCount - 1].position.y) {
                return lineCount - 1;
            }

            float closestDistance = float.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < lineCount; i++) {
                LineInfo line = lineInfos[i];
                float y1 = line.position.y;
                float y2 = y1 + line.height;

                if (point.y >= y1 && point.y <= y2) {
                    return i;
                }

                float distToY1 = Mathf.Abs(point.y - y1);
                float distToY2 = Mathf.Abs(point.y - y2);
                if (distToY1 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToY1;
                }

                if (distToY2 < closestDistance) {
                    closestIndex = i;
                    closestDistance = distToY2;
                }
            }

            return closestIndex;
        }

        public SelectionRange SelectToPoint(SelectionRange range, Vector2 point) {
            SelectionRange selectEnd = GetSelectionAtPoint(point);
            return new SelectionRange(range.cursorIndex, range.cursorEdge, selectEnd.cursorIndex, selectEnd.cursorEdge);
        }

        // todo -- verify this for multi line
        public RangeInt GetLineRange(SelectionRange selectionRange) {
            if (lineInfoList.Count == 0) return new RangeInt();
            int start = 0;
            int end = 0;
            int min = Mathf.Min(selectionRange.cursorIndex, selectionRange.selectIndex);
            int max = Mathf.Max(selectionRange.cursorIndex, selectionRange.selectIndex);
            LineInfo[] lines = lineInfoList.Array;
            WordInfo[] words = wordInfoList.Array;
            int i = 0;
            for (i = 0; i < lineInfoList.Count; i++) {
                WordInfo w = words[lines[i].wordStart];
                int charStart = w.startChar;
                if (charStart >= min) {
                    start = i;
                    break;
                }
            }

            while (i < lineInfoList.Count) {
                WordInfo w = words[lines[i].wordStart + lines[i].wordCount];
                int charStart = w.startChar;
                if (charStart >= max) {
                    end = i;
                    break;
                }

                i++;
            }

            // todo fix for multi line
            return new RangeInt(start, 1);
        }

        public Rect GetLineRect(int i) {
            if (i < 0 || i >= lineInfoList.Count) {
                return default;
            }

            LineInfo lineInfo = lineInfoList.Array[i];
            return new Rect(lineInfo.position.x, lineInfo.position.y, lineInfo.width, lineInfo.height);
        }

        public SelectionRange GetSelectionAtPoint(Vector2 point) {
            if (charInfoList.Count == 0) return new SelectionRange(0, TextEdge.Left);
            int charIndex = FindNearestCharacterIndex(point);
            SelectionRange range = new SelectionRange(charIndex, FindCursorEdge(charIndex, point));
            if (IsLastOnLine(charIndex)) {
                return range;
            }

            return range.NormalizeLeft();
        }

        private bool IsFirstOnLine(int idx) {
            int lineIndex = charInfoList.Array[idx].lineIndex;
            int wordIndex = lineInfoList.Array[lineIndex].wordStart;
            int startChar = wordInfoList.Array[wordIndex].startChar;
            return idx == startChar;
        }

        private bool IsLastOnLine(int idx) {
            int lineIndex = charInfoList.Array[idx].lineIndex;
            int wordIndex = lineInfoList.Array[lineIndex].LastWordIndex;
            int endChar = wordInfoList.Array[wordIndex].LastCharacterIndex;
            return idx == endChar;
        }

        public Vector2 GetCursorPosition(SelectionRange selectionRange) {
            if (CharCount == 0) {
                return new Vector2(2, 0); // 2 is temp
            }
            else if (CharCount == 1) {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    return new Vector2(2, 0); // using the left edge of character makes caret hard to see if border is present
                }
                else {
                    return new Vector2(charInfoList.Array[selectionRange.cursorIndex].layoutBottomRight.x, 0);
                }
            }

            else if (selectionRange.cursorIndex < 0 || selectionRange.cursorIndex >= CharCount) {
                return new Vector2();
            }

            CharInfo charInfo = charInfoList.Array[selectionRange.cursorIndex];
            LineInfo lineInfo = lineInfoList.Array[charInfo.lineIndex];

            if (selectionRange.cursorEdge == TextEdge.Left && IsFirstOnLine(selectionRange.cursorIndex)) {
                return new Vector2(charInfo.layoutTopLeft.x, lineInfo.position.y);
            }
            else if (selectionRange.cursorEdge == TextEdge.Right && IsLastOnLine(selectionRange.cursorIndex)) {
                return new Vector2(charInfo.layoutBottomRight.x, lineInfo.position.y);
            }
            else {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    // use average of previous right and current left
                    CharInfo prev = charInfoList.Array[selectionRange.cursorIndex - 1];
                    float prevX = prev.layoutBottomRight.x;
                    float x = charInfo.layoutTopLeft.x;
                    float half = (prevX + x) * 0.5f;
                    return new Vector2(half, lineInfo.position.y);
                }
                else {
                    // use average of previous right and current left
                    CharInfo next = charInfoList.Array[selectionRange.cursorIndex + 1];
                    float nextX = next.layoutTopLeft.x;
                    float x = charInfo.layoutBottomRight.x;
                    float half = (nextX + x) * 0.5f;
                    return new Vector2(half, lineInfo.position.y);
                }
            }
        }

        public Vector2 GetSelectionPosition(SelectionRange selectionRange) {
            return GetCursorPosition(new SelectionRange(selectionRange.selectIndex, selectionRange.selectEdge));
        }

        public SelectionRange MoveCursorLeft(SelectionRange range, bool maintainSelection) {
            int selectionIndex = range.selectIndex;
            TextEdge selectionEdge = range.selectEdge;

            if (!maintainSelection && range.HasSelection) {
                return new SelectionRange(range.cursorIndex, range.cursorEdge).NormalizeLeft();
            }
            else if (!maintainSelection) {
                selectionIndex = -1;
            }
            else if (!range.HasSelection) {
                selectionIndex = range.cursorIndex;
                selectionEdge = range.cursorEdge;
            }

            int cursorIndex = range.cursorIndex - 1;

            if (cursorIndex < 0) cursorIndex = 0;

            if (range.cursorEdge == TextEdge.Right) {
                return new SelectionRange(cursorIndex + 1, TextEdge.Left, selectionIndex, selectionEdge);
            }

            return new SelectionRange(cursorIndex, TextEdge.Left, selectionIndex, selectionEdge);
        }

        public SelectionRange MoveCursorRight(SelectionRange range, bool maintainSelection) {
            int selectionIndex = range.selectIndex;
            TextEdge selectionEdge = range.selectEdge;

            if (!maintainSelection && range.HasSelection) {
                return new SelectionRange(range.cursorIndex, range.cursorEdge);
            }
            else if (!maintainSelection) {
                selectionIndex = -1;
            }
            else if (!range.HasSelection) {
                selectionIndex = range.cursorIndex;
                selectionEdge = range.cursorEdge;
            }

            int cursorIndex = range.cursorIndex + 1;

            if (cursorIndex >= CharCount) {
                return new SelectionRange(CharCount - 1, TextEdge.Right, selectionIndex, selectionEdge);
            }

            if (range.cursorEdge == TextEdge.Right) {
                return new SelectionRange(cursorIndex + 1, TextEdge.Left, selectionIndex, selectionEdge);
            }

            return new SelectionRange(cursorIndex, TextEdge.Left, selectionIndex, selectionEdge);
        }

        public SelectionRange SelectWordAtPoint(Vector2 point) {
            int line = FindNearestLine(point);
            int word = FindNearestWord(line, point);
            WordInfo wordInfo = wordInfoList.Array[word];
            if (wordInfo.spaceStart == wordInfo.charCount) {
                return new SelectionRange(wordInfo.startChar, TextEdge.Left, wordInfo.startChar + wordInfo.spaceStart - 1, TextEdge.Right);
            }

            return new SelectionRange(wordInfo.startChar, TextEdge.Left, wordInfo.startChar + wordInfo.spaceStart, TextEdge.Left).Invert();
        }

        public SelectionRange SelectLineAtPoint(Vector2 point) {
            int line = FindNearestLine(point);

            int wordStart = lineInfoList.Array[line].wordStart;
            int wordEnd = wordStart + lineInfoList.Array[line].wordCount - 1;
            WordInfo startWord = wordInfoList.Array[wordStart];
            WordInfo endWord = wordInfoList.Array[wordEnd];
            return new SelectionRange(startWord.startChar, TextEdge.Left, endWord.startChar + endWord.charCount - 1, TextEdge.Right).Invert();
        }

        // todo -- support multiple spans
        public string GetSelectedString(SelectionRange selectionRange) {
            int start = Mathf.Min(selectionRange.cursorIndex, selectionRange.selectIndex);
            int end = Mathf.Max(selectionRange.cursorIndex, selectionRange.selectIndex);

            if (selectionRange.selectEdge == TextEdge.Right) {
                end++;
            }

            // assume a single span for now

            return spanList.Array[0].inputText.Substring(start, end - start);
        }

        public SelectionRange SelectAll() {
            return new SelectionRange(0, TextEdge.Left, CharCount - 1, TextEdge.Right);
        }

        public SelectionRange MoveToStartOfLine(SelectionRange selectionRange, bool maintainSelection) {
            int selectionIndex = selectionRange.selectIndex;
            TextEdge selectionEdge = selectionRange.selectEdge;

            if (!maintainSelection && selectionRange.HasSelection) {
                return new SelectionRange(selectionRange.cursorIndex, selectionRange.cursorEdge).NormalizeLeft();
            }
            else if (!maintainSelection) {
                selectionIndex = -1;
            }
            else if (!selectionRange.HasSelection) {
                selectionIndex = selectionRange.cursorIndex;
                selectionEdge = selectionRange.cursorEdge;
            }
            
            CharInfo charInfo = charInfoList.Array[selectionRange.cursorIndex];
            int lineIdx = charInfo.lineIndex;
            int i = selectionRange.cursorIndex;
            while (i > 0) {
                if (charInfoList.Array[i].lineIndex != lineIdx) {
                    break;
                }

                i--;
            }

            return new SelectionRange(i, TextEdge.Left, selectionIndex, selectionEdge);
        }

        public SelectionRange MoveToEndOfText() {
            return new SelectionRange(CharCount - 1, TextEdge.Right);
        }

        public SelectionRange MoveToEndOfLine(SelectionRange selectionRange, bool maintainSelection) {
            int selectionIndex;
            TextEdge selectionEdge = TextEdge.Right;
            
            if (!maintainSelection) {
                selectionIndex = -1;
                selectionEdge = TextEdge.Left;
            }
            else if (!selectionRange.HasSelection) {
                selectionIndex = selectionRange.cursorIndex;
            } else {
                selectionIndex = selectionRange.selectIndex;
            }      
            
            CharInfo charInfo = charInfoList.Array[selectionRange.cursorIndex];
            int lineIdx = charInfo.lineIndex;
            int i = selectionRange.cursorIndex;
            while (i < CharCount - 1) {
                if (charInfoList.Array[i].lineIndex != lineIdx) {
                    break;
                }

                i++;
            }
            
            return new SelectionRange(i, TextEdge.Right, selectionIndex, selectionEdge);
        }

        private static readonly StringBuilder s_Builder = new StringBuilder(128);

        public string GetAllText() {
            if (spanList.Count == 1) {
                return spanList[0].inputText;
            }
            else {
                s_Builder.Clear();
                string retn = string.Empty;
                for (int i = 0; i < spanList.Count; i++) {
                    s_Builder.Append(spanList.Array[i].inputText);
                }

                return retn;
            }
        }

        public float GetCharacterWidthAtPoint(SelectionRange selectionRange) {
            CharInfo charInfo = charInfoList.Array[selectionRange.cursorIndex];
            return charInfo.layoutBottomRight.x - charInfo.layoutTopLeft.x;
        }

        public SVGXTextStyle GetSpanStyle(int i) {
            if (i < 0 || i >= spanList.Count) {
                return default;
            }

            return spanList.Array[i].textStyle;
        }


        private static int ProcessWrap(string input, bool collapseSpaceAndTab, bool preserveNewLine, ref char[] buffer) {
            bool collapsing = collapseSpaceAndTab;

            input = input ?? string.Empty;

            if (buffer == null) {
                buffer = ArrayPool<char>.GetMinSize(input.Length);
            }

            if (buffer.Length < input.Length) {
                ArrayPool<char>.Resize(ref buffer, input.Length);
            }

            int writeIndex = 0;

            for (int i = 0; i < input.Length; i++) {
                char current = input[i];

                if (current == '\n' && !preserveNewLine) {
                    continue;
                }

                bool isWhiteSpace = current == '\t' || current == ' ';

                if (collapsing) {
                    if (!isWhiteSpace) {
                        buffer[writeIndex++] = current;
                        collapsing = false;
                    }
                }
                else if (isWhiteSpace) {
                    collapsing = collapseSpaceAndTab;
                    buffer[writeIndex++] = ' ';
                }
                else {
                    buffer[writeIndex++] = current;
                }
            }

            return writeIndex;
        }

        private static void ApplyTextTransform(char[] buffer, int count, TextTransform transform) {
            switch (transform) {
                case TextTransform.UpperCase:
                case TextTransform.SmallCaps:
                    for (int i = 0; i < count; i++) {
                        buffer[i] = char.ToUpper(buffer[i]);
                    }

                    break;
                case TextTransform.LowerCase:
                    for (int i = 0; i < count; i++) {
                        buffer[i] = char.ToLower(buffer[i]);
                    }

                    break;
                case TextTransform.TitleCase:
                    for (int i = 0; i < count - 1; i++) {
                        if (char.IsLetter(buffer[i]) && char.IsWhiteSpace(buffer[i - 1])) {
                            buffer[i] = char.ToUpper(buffer[i]);
                        }
                    }

                    break;
            }
        }

    }

}
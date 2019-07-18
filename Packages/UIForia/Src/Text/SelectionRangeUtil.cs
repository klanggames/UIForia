using System;
using UnityEngine;

namespace UIForia.Text {

    public static class SelectionRangeUtil {

        public static string InsertText(string source, ref SelectionRange selectionRange, string characters) {
            string retn = null;

            if (string.IsNullOrEmpty(characters)) {
                return source;
            }

            if (string.IsNullOrEmpty(source)) {
                selectionRange = new SelectionRange(characters.Length - 1, TextEdge.Right);
                return characters;
            }

            if (selectionRange.HasSelection) {
                source = DeleteTextForwards(source, ref selectionRange);
            }

            int cursorIndex = Mathf.Clamp(selectionRange.cursorIndex, 0, source.Length - 1);
            TextEdge edge = selectionRange.cursorEdge;
            if (selectionRange.cursorIndex == source.Length - 1) {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    string text = source.Substring(0, cursorIndex);
                    string endText = source.Substring(cursorIndex);
                    retn = text + characters + endText;
                }
                else {
                    retn = source + characters;
                    cursorIndex += characters.Length;
                }
            }
            else if (selectionRange.cursorIndex == 0) {
                if (selectionRange.cursorEdge == TextEdge.Left) {
                    retn = characters + source;
                    cursorIndex = characters.Length;
                    edge = TextEdge.Left;
                }
                else {
                    string text = source.Substring(0, cursorIndex);
                    string endText = source.Substring(cursorIndex);
                    retn = text + characters + endText;
                    cursorIndex += characters.Length;
                }
            }
            else {
                // todo optimize not to use substring here
                if (selectionRange.cursorEdge == TextEdge.Right) {
                    cursorIndex += characters.Length;
                }

                string text = source.Substring(0, cursorIndex);
                string endText = source.Substring(cursorIndex);
                retn = text + characters + endText;
                cursorIndex += characters.Length;
            }

            selectionRange = new SelectionRange(cursorIndex, edge);
            return retn;
        }

        public static string DeleteTextForwards(string source, ref SelectionRange selectionRange) {
            string retn = null;

            if (source.Length == 0) {
                return String.Empty;
            }

            int cursorIndex = Mathf.Clamp(selectionRange.cursorIndex, 0, source.Length - 1);

            if (selectionRange.HasSelection) {
                int min = Mathf.Clamp((selectionRange.cursorIndex < selectionRange.selectIndex ? selectionRange.cursorIndex : selectionRange.selectIndex), 0, source.Length - 1);
                int max = (selectionRange.cursorIndex > selectionRange.selectIndex ? selectionRange.cursorIndex : selectionRange.selectIndex);

                if (selectionRange.selectEdge == TextEdge.Right) {
                    max++;
                }

                string part0 = source.Substring(0, min);
                string part1 = source.Substring(max);
                retn = part0 + part1;

                if (selectionRange.selectEdge == TextEdge.Right) {
                    if (min - 1 < 0) {
                        selectionRange = new SelectionRange(0, TextEdge.Left);
                    }
                    else {
                        selectionRange = new SelectionRange(min - 1, TextEdge.Right);
                    }
                }
                else {
                    selectionRange = new SelectionRange(min, TextEdge.Left);
                    return retn;
                }
            }
            else {
                if (cursorIndex == source.Length - 1 && selectionRange.cursorEdge == TextEdge.Right) {
                    return source;
                }
                else {
                    if (cursorIndex == source.Length - 1) {
                        retn = source.Remove(source.Length - 1);
                        selectionRange = new SelectionRange(retn.Length - 1, TextEdge.Right);
                    }
                    else {
                        string part0 = source.Substring(0, cursorIndex);
                        string part1 = source.Substring(cursorIndex + 1);
                        retn = part0 + part1;
                        selectionRange = new SelectionRange(cursorIndex, TextEdge.Left);
                    }
                }
            }

            return retn;
        }

        public static string DeleteTextBackwards(string source, ref SelectionRange range) {
            if (string.IsNullOrEmpty(source)) {
                range = new SelectionRange(0, TextEdge.Left);
                return string.Empty;
            }

            int cursorIndex = range.cursorIndex;

            if (range.HasSelection) {
                int min = (range.cursorIndex < range.selectIndex ? range.cursorIndex : range.selectIndex);
                int max = (range.cursorIndex > range.selectIndex ? range.cursorIndex : range.selectIndex);

                if (max - min == source.Length - 1) {
                    range = new SelectionRange(0, TextEdge.Left);
                    return string.Empty;
                }

                if (range.selectEdge == TextEdge.Right) {
                    max++;
                }

                if (max == source.Length) {
                    range = new SelectionRange(min - 1, TextEdge.Right);
                    return source.Substring(0, min);    
                }

                if (min == 0) {
                    range = new SelectionRange(0, TextEdge.Left);
                    return source.Substring(max);
                }
                
                string part0 = source.Substring(0, min);
                string part1 = source.Substring(max);
                range = new SelectionRange(min, TextEdge.Left);
                return part0 + part1;
            }
            else {
                if (cursorIndex == 0 && range.cursorEdge == TextEdge.Left) {
                    return source;
                }

                if (range.cursorEdge == TextEdge.Left) {
                    cursorIndex--;
                }

                cursorIndex = Mathf.Max(0, cursorIndex);

                if (cursorIndex == 0) {
                    range = new SelectionRange(0, TextEdge.Left);
                    return source.Substring(1);
                }
                else if (cursorIndex == source.Length - 1) {
                    range = new SelectionRange(range.cursorIndex - 1, TextEdge.Right);
                    return source.Substring(0, source.Length - 1);
                }
                else {
                    string part0 = source.Substring(0, cursorIndex);
                    string part1 = source.Substring(cursorIndex + 1);
                    range = new SelectionRange(cursorIndex, TextEdge.Left);
                    return part0 + part1;
                }
            }
        }

    }

}
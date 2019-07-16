using System;
using System.Collections.Generic;
using UIForia.Compilers;
using UIForia.Compilers.ExpressionResolvers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Expressions;
using UIForia.Rendering;
using UIForia.Systems;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Animation {

    public class VariableResolver<T> : ExpressionAliasResolver {

        public T value;

        public VariableResolver(string aliasName, T value) : base(aliasName) {
            this.value = value;
        }

        public override Expression CompileAsValueExpression(CompilerContext context) {
            return new ConstantExpression<T>(value);
        }

    }

    public class StyleKeyFrameAnimation : StyleAnimation {

        private readonly LightList<ProcessedKeyFrameGroup> processedFrameGroups;
        private static readonly ExpressionCompiler expressionCompiler;

        static StyleKeyFrameAnimation() {
            expressionCompiler = new ExpressionCompiler(true);
        }

        public StyleKeyFrameAnimation(UIElement target, AnimationData data) : base(target, data) {
            processedFrameGroups = new LightList<ProcessedKeyFrameGroup>();
            ProcessKeyFrames(data.frames);
        }

        public void ProcessKeyFrames(IList<AnimationKeyFrame> frames) {
            // todo -- ensure we release lists where we need to
            // todo -- dont use a list each processed frame group, use a single list sorted sensibly
            processedFrameGroups.QuickClear();
            for (int i = 0; i < frames.Count; i++) {
                AnimationKeyFrame f = frames[i];
                StyleKeyFrameValue[] properties = f.properties.Array;
                for (int j = 0; j < f.properties.Count; j++) {
                    AddKeyFrame(f.key, properties[j]);
                }
            }

            for (int i = 0; i < processedFrameGroups.Count; i++) {
                StylePropertyId property = processedFrameGroups[i].propertyId;
                LightList<ProcessedKeyFrame> processedKeyFrames = processedFrameGroups[i].frames;
                processedKeyFrames.Sort(KeyFrameSorter);
                if (processedKeyFrames[0].time != 0f) {
                    processedKeyFrames.Insert(0, new ProcessedKeyFrame(0f, target.style.GetComputedStyleProperty(property)));
                }
            }
        }

        private void AddKeyFrame(float time, StyleKeyFrameValue property) {
            if (property.IsCalculated) {
                //StyleTokenizer.Tokenize();
                //AnimationParser.Parse();
                switch (property.propertyId) {
                    case StylePropertyId.TransformPivotX:
                    case StylePropertyId.TransformPositionX:
                    case StylePropertyId.PaddingLeft:
                    case StylePropertyId.PaddingRight:
                    case StylePropertyId.BorderLeft:
                    case StylePropertyId.BorderRight:
                    case StylePropertyId.MarginLeft:
                    case StylePropertyId.MarginRight: {
                        UIFixedLength val = target.style.GetComputedStyleProperty(property.propertyId).AsUIFixedLength;
                        expressionCompiler.SetAliasResolver(new VariableResolver<UIFixedLength>("startVal", ResolveFixedWidth(target, target.View.Viewport, val)));
                        break;
                    }

                    case StylePropertyId.TransformPivotY:
                    case StylePropertyId.TransformPositionY:
                    case StylePropertyId.PaddingTop:
                    case StylePropertyId.PaddingBottom:
                    case StylePropertyId.BorderTop:
                    case StylePropertyId.BorderBottom:
                    case StylePropertyId.MarginTop:
                    case StylePropertyId.MarginBottom: {
                        UIFixedLength val = target.style.GetComputedStyleProperty(property.propertyId).AsUIFixedLength;
                        expressionCompiler.SetAliasResolver(new VariableResolver<UIFixedLength>("startVal", ResolveFixedHeight(target, target.View.Viewport, val)));
                        break;
                    }

                    case StylePropertyId.PreferredWidth:
                    case StylePropertyId.MinWidth:
                    case StylePropertyId.MaxWidth: {
                        UIMeasurement val = target.style.GetComputedStyleProperty(property.propertyId).AsUIMeasurement;
                        expressionCompiler.SetAliasResolver(new VariableResolver<UIMeasurement>("startVal", ResolveWidthMeasurement(target, target.View.Viewport, val)));
                        break;
                    }

                    case StylePropertyId.PreferredHeight:
                    case StylePropertyId.MinHeight:
                    case StylePropertyId.MaxHeight: {
                        UIMeasurement val = target.style.GetComputedStyleProperty(property.propertyId).AsUIMeasurement;
                        expressionCompiler.SetAliasResolver(new VariableResolver<UIMeasurement>("startVal", ResolveHeightMeasurement(target, target.View.Viewport, val)));
                        break;
                    }

                    case StylePropertyId.Opacity:
                    case StylePropertyId.TransformScaleX:
                    case StylePropertyId.TransformScaleY:
                    case StylePropertyId.TransformRotation:
                    case StylePropertyId.GridLayoutColGap:
                    case StylePropertyId.GridLayoutRowGap: {
                        expressionCompiler.SetAliasResolver(new VariableResolver<float>("startVal", target.style.GetComputedStyleProperty(property.propertyId).AsFloat));
                        break;
                    }

                    case StylePropertyId.BorderColor:
                    case StylePropertyId.BackgroundColor:
                    case StylePropertyId.TextColor: {
                        throw new NotImplementedException();
                    }

                    default:
                        if (StyleUtil.CanAnimate(property.propertyId)) {
                            throw new NotImplementedException(property.propertyId + " can be animated but is not implemented");
                        }

                        throw new InvalidArgumentException(property.propertyId + " is not a supported animation property");
                }
            }

            for (int i = 0; i < processedFrameGroups.Count; i++) {
                if (processedFrameGroups[i].propertyId == property.propertyId) {
                    processedFrameGroups[i].frames.Add(new ProcessedKeyFrame(time, property));
                    return;
                }
            }

            LightList<ProcessedKeyFrame> list = LightList<ProcessedKeyFrame>.Get();
            list.Add(new ProcessedKeyFrame(time, property));
            processedFrameGroups.Add(new ProcessedKeyFrameGroup(property.propertyId, list));
        }

        public override UITaskResult Run(float deltaTime) {
            status.elapsedTotalTime += deltaTime;
            status.elapsedIterationTime += deltaTime;

            Rect viewport = target.View.Viewport;

            AnimationOptions options = data.options;

            float duration = options.duration.Value * 0.001f;
            float iterationTime = duration;
            if (options.iterations.Value > 0) {
                iterationTime = iterationTime / options.iterations.Value;
            }

            float progress = Mathf.Clamp01(status.elapsedIterationTime / duration);

            status.iterationProgress = progress;
            status.frameCount++;
            
            ProcessedKeyFrameGroup[] groups = processedFrameGroups.Array;
            for (int i = 0; i < processedFrameGroups.Count; i++) {
                StylePropertyId propertyId = groups[i].propertyId;
                ProcessedKeyFrame[] frames = groups[i].frames.Array;
                int frameCount = groups[i].frames.Count;

                ProcessedKeyFrame prev = frames[0];
                ProcessedKeyFrame next = frames[frameCount - 1];

                for (int j = 1; j < frameCount; j++) {
                    if (frames[j].time > progress) {
                        prev = frames[j - 1];
                        next = frames[j];
                        break;
                    }
                }

                float t = Mathf.Clamp01(Easing.Interpolate(MathUtil.PercentOfRange(progress, prev.time, next.time), options.timingFunction.Value));

                switch (propertyId) {
                    case StylePropertyId.AnchorLeft:
                    case StylePropertyId.AnchorRight:
                    case StylePropertyId.TransformPivotX:
                    case StylePropertyId.TransformPositionX:
                    case StylePropertyId.PaddingLeft:
                    case StylePropertyId.PaddingRight:
                    case StylePropertyId.BorderLeft:
                    case StylePropertyId.BorderRight:
                    case StylePropertyId.MarginLeft:
                    case StylePropertyId.MarginRight: {

                        float v0 = ResolveFixedWidth(target, viewport, prev.value.IsCalculated
                            ? prev.value.Evaluate<UIFixedLength>(null)
                            : prev.value.styleProperty.AsUIFixedLength
                        );

                        float v1 = ResolveFixedWidth(target, viewport, next.value.IsCalculated
                            ? next.value.Evaluate<UIFixedLength>(null)
                            : next.value.styleProperty.AsUIFixedLength
                        );
                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, new UIFixedLength(Mathf.Lerp(v0, v1, t))));
                        break;
                    }

                    case StylePropertyId.AnchorTop:
                    case StylePropertyId.AnchorBottom:
                    case StylePropertyId.TransformPivotY:
                    case StylePropertyId.TransformPositionY:
                    case StylePropertyId.PaddingTop:
                    case StylePropertyId.PaddingBottom:
                    case StylePropertyId.BorderTop:
                    case StylePropertyId.BorderBottom:
                    case StylePropertyId.MarginTop:
                    case StylePropertyId.MarginBottom: {
                        float v0 = ResolveFixedHeight(target, viewport, prev.value.IsCalculated
                            ? prev.value.Evaluate<UIFixedLength>(null)
                            : prev.value.styleProperty.AsUIFixedLength
                        );

                        float v1 = ResolveFixedHeight(target, viewport, next.value.IsCalculated
                            ? next.value.Evaluate<UIFixedLength>(null)
                            : next.value.styleProperty.AsUIFixedLength
                        );
                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, new UIFixedLength(Mathf.Lerp(v0, v1, t))));
                        break;
                    }

                    case StylePropertyId.PreferredWidth:
                    case StylePropertyId.MinWidth:
                    case StylePropertyId.MaxWidth: { 

                        float v0 = ResolveWidthMeasurement(target, viewport, prev.value.IsCalculated
                            ? prev.value.Evaluate<UIMeasurement>(null)
                            : prev.value.styleProperty.AsUIMeasurement
                        );

                        float v1 = ResolveWidthMeasurement(target, viewport, next.value.IsCalculated
                            ? next.value.Evaluate<UIMeasurement>(null)
                            : next.value.styleProperty.AsUIMeasurement
                        );

                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, new UIMeasurement(Mathf.Lerp(v0, v1, t))));
                        break;
                    }

                    case StylePropertyId.PreferredHeight:
                    case StylePropertyId.MinHeight:
                    case StylePropertyId.MaxHeight: {
                        float v0 = ResolveHeightMeasurement(target, viewport, prev.value.IsCalculated
                            ? prev.value.Evaluate<UIMeasurement>(null)
                            : prev.value.styleProperty.AsUIMeasurement
                        );

                        float v1 = ResolveHeightMeasurement(target, viewport, next.value.IsCalculated
                            ? next.value.Evaluate<UIMeasurement>(null)
                            : next.value.styleProperty.AsUIMeasurement
                        );

                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, new UIMeasurement(Mathf.Lerp(v0, v1, t))));
                        break;
                    }

                    case StylePropertyId.Opacity:
                    case StylePropertyId.TransformScaleX:
                    case StylePropertyId.TransformScaleY:
                    case StylePropertyId.TransformRotation:
                    case StylePropertyId.GridLayoutColGap:
                    case StylePropertyId.GridLayoutRowGap: {

                        float v0 = prev.value.IsCalculated
                            ? prev.value.Evaluate<float>(null)
                            : prev.value.styleProperty.AsFloat;

                        float v1 = next.value.IsCalculated
                            ? next.value.Evaluate<float>(null)
                            : next.value.styleProperty.AsFloat;

                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, Mathf.Lerp(v0, v1, t)));
                        break;
                    }

                    case StylePropertyId.BorderColor:
                    case StylePropertyId.BackgroundColor:
                    case StylePropertyId.TextColor: {

//                        float v0 = ResolveHeightMeasurement(target, viewport, prev.value.IsCalculated
//                            ? prev.value.Evaluate<UIMeasurement>(null)
//                            : prev.value.styleProperty.AsUIMeasurement
//                        );
//
//                        float v1 = ResolveHeightMeasurement(target, viewport, next.value.IsCalculated
//                            ? next.value.Evaluate<UIMeasurement>(null)
//                            : next.value.styleProperty.AsUIMeasurement
//                        );
//
//                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, new UIMeasurement(Mathf.Lerp(v0, v1, t))));

                        Color c0 = prev.value.IsCalculated
                            ? prev.value.Evaluate<Color>(null)
                            : prev.value.styleProperty.AsColor;

                        Color c1 = next.value.IsCalculated
                            ? next.value.Evaluate<Color>(null)
                            : next.value.styleProperty.AsColor;
                        target.style.SetAnimatedProperty(new StyleProperty(propertyId, Color.Lerp(c0, c1, t)));
                        break;
                    }

                    default:
                        if (StyleUtil.CanAnimate(propertyId)) {
                            throw new NotImplementedException(propertyId + " can be animated but is not implemented");
                        }

                        throw new InvalidArgumentException(propertyId + " is not a supported animation property");
                }
            }

            return progress == 1f ? UITaskResult.Completed : UITaskResult.Running;
        }

    }

}
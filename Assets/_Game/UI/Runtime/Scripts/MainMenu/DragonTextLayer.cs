using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// Decorative background layer for the main menu.
    /// Words drift apart when the dragon body passes through them.
    /// Implemented fully in UI Toolkit so it can live inside the existing UIDocument menu.
    /// </summary>
    public sealed class DragonTextLayer : VisualElement
    {
        private sealed class WordData
        {
            public string Text = string.Empty;
            public Label Label = null!;
            public int LineIndex;
            public float Width;
            public float Height;
            public float BaseX;
            public float BaseY;
            public float CurrentX;
            public float TargetX;
        }

        private sealed class LineData
        {
            public float Y;
            public float Height;
            public float MinX;
            public float MaxX;
            public readonly List<WordData> Words = new();
        }

        private struct DragonNode
        {
            public Vector2 Position;
            public float Radius;
        }

        private struct Interval
        {
            public float Min;
            public float Max;

            public Interval(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }

        private readonly List<WordData> _words = new();
        private readonly List<LineData> _lines = new();
        private readonly VisualElement _wordHost;

        private DragonNode[] _dragon = Array.Empty<DragonNode>();
        private VisualElement _exclusionElement;
        private Rect _cachedExclusionLocalRect;
        private bool _hasExclusionRect;
        private bool _isBuilt;

        private string _sourceText =
            "SEASONAL BASTION ENDURES THROUGH AUTUMN AND WINTER. MOVE THE CURSOR THROUGH THE FIELD AND WATCH THE WORDS PART AROUND THE DRAGON.";

        // Layout
        private const float HorizontalPadding = 64f;
        private const float VerticalPadding = 48f;
        private const float WordSpacing = 16f;
        private const float LineSpacing = 22f;
        private const float FontSize = 22f;
        private const float ApproxCharWidth = 13.25f;
        private const float LineHeight = 30f;

        // Motion
        private const float WordLerpSpeed = 11f;
        private const float HeadFollowSpeed = 20f;
        private const float BodyFollowSharpness = 24f;
        private const float FollowDistance = 18f;
        private const float RelaxGap = 10f;
        private const float PushPadding = 8f;
        private const float BaseBias = 0.08f;
        private const float IdleAmplitude = 7f;
        private const float IdleFrequency = 1.9f;
        private const float IdleDelay = 0.15f;

        // Shape
        private const int SegmentCount = 68;
        private const float RadiusHead = 38f;
        private const float RadiusBody = 22f;
        private const float RadiusTail = 8f;
        private const float LineInfluencePadding = 8f;
        private const float ExclusionPadding = 26f;

        private Vector2 _mouseLocal;
        private Vector2 _lastMouseLocal;
        private float _lastMouseMoveTime;

        public DragonTextLayer()
        {
            name = "DragonTextLayerRuntime";
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            pickingMode = PickingMode.Ignore;

            _wordHost = new VisualElement
            {
                name = "DragonWordHost",
                pickingMode = PickingMode.Ignore
            };
            _wordHost.style.position = Position.Absolute;
            _wordHost.style.left = 0;
            _wordHost.style.top = 0;
            _wordHost.style.right = 0;
            _wordHost.style.bottom = 0;
            hierarchy.Add(_wordHost);

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<GeometryChangedEvent>(_ => Rebuild());
        }

        public void SetText(string text)
        {
            _sourceText = string.IsNullOrWhiteSpace(text) ? _sourceText : text;
            Rebuild();
        }

        public void SetExclusionElement(VisualElement element)
        {
            _exclusionElement = element;
            UpdateExclusionRect();
            MarkDirtyRepaint();
        }

        public void Tick(float deltaTime)
        {
            if (!_isBuilt || panel == null)
                return;

            UpdateExclusionRect();
            UpdateMouseLocal();
            UpdateDragon(deltaTime);
            UpdateWordTargets();
            UpdateWords(deltaTime);
            MarkDirtyRepaint();
        }

        public void Rebuild()
        {
            _wordHost.Clear();
            _words.Clear();
            _lines.Clear();
            _dragon = Array.Empty<DragonNode>();
            _isBuilt = false;

            if (resolvedStyle.width < 16f || resolvedStyle.height < 16f)
                return;

            BuildWords();
            BuildDragon();
            UpdateExclusionRect();
            _isBuilt = true;
            MarkDirtyRepaint();
        }

        private void BuildWords()
        {
            List<string> tokens = Tokenize(_sourceText);
            float usableWidth = Mathf.Max(120f, resolvedStyle.width - HorizontalPadding * 2f);
            float leftX = HorizontalPadding;
            float topY = VerticalPadding;

            float cursorX = leftX;
            float cursorY = topY;

            LineData currentLine = new()
            {
                Y = cursorY,
                Height = LineHeight,
                MinX = leftX,
                MaxX = leftX + usableWidth
            };

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                float tokenWidth = EstimateWordWidth(token);

                bool needsWrap = currentLine.Words.Count > 0 && (cursorX - leftX + tokenWidth) > usableWidth;
                if (needsWrap)
                {
                    _lines.Add(currentLine);
                    cursorY += LineHeight + LineSpacing;
                    cursorX = leftX;
                    currentLine = new LineData
                    {
                        Y = cursorY,
                        Height = LineHeight,
                        MinX = leftX,
                        MaxX = leftX + usableWidth
                    };
                }

                Label label = new(token)
                {
                    pickingMode = PickingMode.Ignore
                };
                label.style.position = Position.Absolute;
                label.style.left = cursorX;
                label.style.top = cursorY;
                label.style.fontSize = FontSize;
                label.style.color = new Color(0.94f, 0.97f, 1f, 0.10f);
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                _wordHost.Add(label);

                WordData word = new()
                {
                    Text = token,
                    Label = label,
                    LineIndex = _lines.Count,
                    Width = tokenWidth,
                    Height = LineHeight,
                    BaseX = cursorX,
                    BaseY = cursorY,
                    CurrentX = cursorX,
                    TargetX = cursorX
                };

                currentLine.Words.Add(word);
                _words.Add(word);
                cursorX += tokenWidth + WordSpacing;
            }

            _lines.Add(currentLine);
        }

        private void BuildDragon()
        {
            _dragon = new DragonNode[SegmentCount];
            Vector2 start = new(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
            _mouseLocal = start;
            _lastMouseLocal = start;
            _lastMouseMoveTime = Time.unscaledTime;

            for (int i = 0; i < _dragon.Length; i++)
            {
                float t = i / (float)(_dragon.Length - 1);
                _dragon[i] = new DragonNode
                {
                    Position = start + Vector2.left * i * FollowDistance,
                    Radius = EvaluateRadius(t)
                };
            }
        }

        private static float EvaluateRadius(float t)
        {
            if (t < 0.14f)
                return Mathf.Lerp(RadiusHead, RadiusBody, t / 0.14f);

            if (t < 0.78f)
                return RadiusBody;

            float tailT = Mathf.InverseLerp(0.78f, 1f, t);
            return Mathf.Lerp(RadiusBody, RadiusTail, tailT);
        }

        private void UpdateExclusionRect()
        {
            if (_exclusionElement == null || panel == null)
            {
                _hasExclusionRect = false;
                return;
            }

            Rect world = _exclusionElement.worldBound;

            Vector2 localMin = this.WorldToLocal(world.min);
            Vector2 localMax = this.WorldToLocal(world.max);

            _cachedExclusionLocalRect = Rect.MinMaxRect(
                Mathf.Min(localMin.x, localMax.x) - ExclusionPadding,
                Mathf.Min(localMin.y, localMax.y) - ExclusionPadding,
                Mathf.Max(localMin.x, localMax.x) + ExclusionPadding,
                Mathf.Max(localMin.y, localMax.y) + ExclusionPadding);

            _hasExclusionRect = true;
        }

        private void UpdateMouseLocal()
        {
            Vector2 screen;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
                screen = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            else
#endif
                screen = Input.mousePosition;

            if (panel == null)
                return;

            Vector2 panelMouse = RuntimePanelUtils.ScreenToPanel(panel, screen);
            Vector2 worldMouse = panel.visualTree.LocalToWorld(panelMouse);
            _mouseLocal = this.WorldToLocal(worldMouse);

            if ((_mouseLocal - _lastMouseLocal).sqrMagnitude > 0.5f)
            {
                _lastMouseMoveTime = Time.unscaledTime;
                _lastMouseLocal = _mouseLocal;
            }
        }

        private void UpdateDragon(float deltaTime)
        {
            if (_dragon.Length == 0)
                return;

            Vector2 targetHead = _mouseLocal;
            float idleTime = Time.unscaledTime - _lastMouseMoveTime;
            if (idleTime > IdleDelay)
            {
                targetHead += new Vector2(0f, Mathf.Sin(Time.unscaledTime * IdleFrequency) * IdleAmplitude);
            }

            float headBlend = 1f - Mathf.Exp(-HeadFollowSpeed * deltaTime);
            _dragon[0].Position = Vector2.Lerp(_dragon[0].Position, targetHead, headBlend);

            if (_hasExclusionRect && _cachedExclusionLocalRect.Contains(_dragon[0].Position))
            {
                _dragon[0].Position = ClosestPointOnRectEdge(_cachedExclusionLocalRect, _dragon[0].Position);
            }

            for (int i = 1; i < _dragon.Length; i++)
            {
                Vector2 prev = _dragon[i - 1].Position;
                Vector2 current = _dragon[i].Position;
                Vector2 dir = current - prev;
                float len = dir.magnitude;
                if (len < 0.0001f)
                {
                    dir = Vector2.left;
                    len = 1f;
                }

                Vector2 desired = prev + dir / len * FollowDistance;
                float bodyBlend = 1f - Mathf.Exp(-BodyFollowSharpness * deltaTime);
                Vector2 next = Vector2.Lerp(current, desired, bodyBlend);

                if (_hasExclusionRect && _cachedExclusionLocalRect.Contains(next))
                    next = ClosestPointOnRectEdge(_cachedExclusionLocalRect, next);

                _dragon[i].Position = next;
            }
        }

        private void UpdateWordTargets()
        {
            foreach (WordData word in _words)
                word.TargetX = word.BaseX;

            foreach (LineData line in _lines)
            {
                List<Interval> intervals = BuildMergedIntervals(line);
                if (_hasExclusionRect)
                {
                    float lineCenterY = line.Y + line.Height * 0.5f;
                    if (lineCenterY >= _cachedExclusionLocalRect.yMin && lineCenterY <= _cachedExclusionLocalRect.yMax)
                    {
                        intervals.Add(new Interval(_cachedExclusionLocalRect.xMin, _cachedExclusionLocalRect.xMax));
                    }
                }

                if (intervals.Count == 0)
                {
                    RelaxLine(line);
                    continue;
                }

                intervals.Sort((a, b) => a.Min.CompareTo(b.Min));
                List<Interval> merged = MergeIntervals(intervals);
                ApplyIntervalsToLine(line, merged);
                RelaxLine(line);
                ClampLineToBounds(line);
            }
        }

        private List<Interval> BuildMergedIntervals(LineData line)
        {
            List<Interval> raw = new();
            float lineCenterY = line.Y + line.Height * 0.5f;

            for (int i = 0; i < _dragon.Length; i++)
            {
                DragonNode node = _dragon[i];
                float effectiveRadius = node.Radius + line.Height * 0.5f + LineInfluencePadding;
                float dy = Mathf.Abs(node.Position.y - lineCenterY);
                if (dy >= effectiveRadius)
                    continue;

                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, effectiveRadius * effectiveRadius - dy * dy));
                raw.Add(new Interval(node.Position.x - halfWidth, node.Position.x + halfWidth));
            }

            return raw.Count == 0 ? raw : MergeIntervals(raw);
        }

        private static List<Interval> MergeIntervals(List<Interval> intervals)
        {
            if (intervals.Count <= 1)
                return intervals;

            intervals.Sort((a, b) => a.Min.CompareTo(b.Min));
            List<Interval> merged = new();
            Interval current = intervals[0];

            for (int i = 1; i < intervals.Count; i++)
            {
                if (intervals[i].Min <= current.Max)
                {
                    current.Max = Mathf.Max(current.Max, intervals[i].Max);
                }
                else
                {
                    merged.Add(current);
                    current = intervals[i];
                }
            }

            merged.Add(current);
            return merged;
        }

        private static void ApplyIntervalsToLine(LineData line, List<Interval> intervals)
        {
            for (int i = 0; i < intervals.Count; i++)
            {
                Interval interval = intervals[i];

                float leftCursor = line.MinX;
                float rightCursor = interval.Max + PushPadding;

                for (int w = 0; w < line.Words.Count; w++)
                {
                    WordData word = line.Words[w];
                    float baseCenter = word.BaseX + word.Width * 0.5f;

                    if (baseCenter < interval.Min)
                    {
                        word.TargetX = leftCursor;
                        leftCursor += word.Width + WordSpacing;
                        continue;
                    }

                    if (baseCenter > interval.Max)
                    {
                        word.TargetX = Mathf.Max(word.TargetX, rightCursor);
                        rightCursor = word.TargetX + word.Width + WordSpacing;
                        continue;
                    }

                    // Từ nằm trong vùng bị cắt: tách theo nửa trái / phải
                    if (baseCenter <= (interval.Min + interval.Max) * 0.5f)
                    {
                        word.TargetX = leftCursor;
                        leftCursor += word.Width + WordSpacing;
                    }
                    else
                    {
                        word.TargetX = Mathf.Max(word.TargetX, rightCursor);
                        rightCursor = word.TargetX + word.Width + WordSpacing;
                    }
                }
            }
        }

        private static void RelaxLine(LineData line)
        {
            if (line.Words.Count == 0)
                return;

            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 1; i < line.Words.Count; i++)
                {
                    WordData prev = line.Words[i - 1];
                    WordData cur = line.Words[i];
                    float minDist = prev.Width + WordSpacing;

                    if (cur.TargetX < prev.TargetX + minDist)
                        cur.TargetX = prev.TargetX + minDist;
                }

                for (int i = line.Words.Count - 2; i >= 0; i--)
                {
                    WordData cur = line.Words[i];
                    WordData next = line.Words[i + 1];
                    float minDist = cur.Width + WordSpacing;

                    if (cur.TargetX + cur.Width > next.TargetX)
                        cur.TargetX = next.TargetX - minDist;
                }
            }

            for (int i = 0; i < line.Words.Count; i++)
            {
                WordData word = line.Words[i];
                word.TargetX = Mathf.Lerp(word.TargetX, word.BaseX, BaseBias);
            }
        }

        private static void ClampLineToBounds(LineData line)
        {
            if (line.Words.Count == 0)
                return;

            float minExtent = float.MaxValue;
            float maxExtent = float.MinValue;

            for (int i = 0; i < line.Words.Count; i++)
            {
                WordData word = line.Words[i];
                minExtent = Mathf.Min(minExtent, word.TargetX);
                maxExtent = Mathf.Max(maxExtent, word.TargetX + word.Width);
            }

            float shift = 0f;
            if (minExtent < line.MinX)
                shift += line.MinX - minExtent;
            if (maxExtent > line.MaxX)
                shift -= maxExtent - line.MaxX;

            if (Mathf.Abs(shift) > 0.001f)
            {
                for (int i = 0; i < line.Words.Count; i++)
                    line.Words[i].TargetX += shift;
            }
        }

        private void UpdateWords(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-WordLerpSpeed * deltaTime);
            foreach (WordData word in _words)
            {
                word.CurrentX = Mathf.Lerp(word.CurrentX, word.TargetX, blend);
                word.Label.style.left = word.CurrentX;

                float offset = Mathf.Abs(word.CurrentX - word.BaseX);
                float alpha = Mathf.Lerp(0.10f, 0.24f, Mathf.InverseLerp(0f, 52f, offset));
                word.Label.style.color = new Color(0.94f, 0.97f, 1f, alpha);
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (!_isBuilt || _dragon.Length == 0)
                return;

            Painter2D painter = mgc.painter2D;
            for (int i = _dragon.Length - 1; i >= 0; i--)
            {
                float t = i / (float)(_dragon.Length - 1);
                float alpha = Mathf.Lerp(0.30f, 0.02f, t);

                Color fill = Color.Lerp(
                    new Color(0.40f, 0.66f, 1f, alpha),
                    new Color(0.60f, 0.83f, 1f, alpha * 0.7f),
                    0.35f
                );

                painter.fillColor = fill;
                painter.BeginPath();
                painter.Arc(_dragon[i].Position, _dragon[i].Radius, 0f, Mathf.PI * 2f);
                painter.Fill();
            }
        }

        private static float EstimateWordWidth(string token)
        {
            if (string.IsNullOrEmpty(token))
                return 0f;

            float width = 8f;

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];

                if ("WM@#%&QGOD".IndexOf(c) >= 0) width += 17f;
                else if ("mw".IndexOf(c) >= 0) width += 15f;
                else if ("Iil1|!'".IndexOf(c) >= 0) width += 7f;
                else if (".,:;".IndexOf(c) >= 0) width += 6f;
                else width += 12f;
            }

            return width;
        }

        private static List<string> Tokenize(string text)
        {
            List<string> result = new();
            MatchCollection matches = Regex.Matches(text ?? string.Empty, @"\S+");
            foreach (Match match in matches)
                result.Add(match.Value);
            return result;
        }

        private static Vector2 ClosestPointOnRectEdge(Rect rect, Vector2 point)
        {
            float left = Mathf.Abs(point.x - rect.xMin);
            float right = Mathf.Abs(rect.xMax - point.x);
            float top = Mathf.Abs(point.y - rect.yMin);
            float bottom = Mathf.Abs(rect.yMax - point.y);

            float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            if (min == left) return new Vector2(rect.xMin, Mathf.Clamp(point.y, rect.yMin, rect.yMax));
            if (min == right) return new Vector2(rect.xMax, Mathf.Clamp(point.y, rect.yMin, rect.yMax));
            if (min == top) return new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), rect.yMin);
            return new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax), rect.yMax);
        }
    }
}

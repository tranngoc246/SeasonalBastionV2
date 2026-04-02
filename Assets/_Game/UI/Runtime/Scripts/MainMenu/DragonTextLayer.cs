using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// Decorative background layer for the main menu.
    /// Individual characters drift apart when the dragon body passes through them.
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
            public float VelocityX;
            public bool IsWhitespace;
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

        private readonly List<WordData> _words = new();
        private readonly List<LineData> _lines = new();
        private readonly VisualElement _wordHost;
        private readonly VisualElement _dragonVisualHost;
        private readonly List<VisualElement> _dragonSegments = new();
        private readonly List<VisualElement> _dragonConnectors = new();

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
        private const float GlyphSpacing = 1.25f;
        private const float LineSpacing = 22f;
        private const float FontSize = 22f;
        private const float LineHeight = 30f;

        // Motion
        private const float FollowDistance = 12f;
        private const float BaseBias = 0.12f;
        private const float IdleAmplitude = 2.5f;
        private const float IdleFrequency = 1.9f;
        private const float IdleDelay = 0.15f;

        // Character reaction
        private const float PushPadding = 16f;
        private const float MaxWordPush = 128f;
        private const float LocalInfluenceFalloff = 90f;
        private const float LineInfluencePadding = 6f;
        private const float ExclusionPadding = 26f;
        private const float SpacePushMultiplier = 0.45f;

        // Shape
        private const int SegmentCount = 32;
        private const float RadiusHead = 30f;
        private const float RadiusBody = 18f;
        private const float RadiusTail = 7f;
        private const float ConnectorThickness = 0.96f;

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

            _dragonVisualHost = new VisualElement
            {
                name = "DragonVisualHost",
                pickingMode = PickingMode.Ignore
            };
            _dragonVisualHost.style.position = Position.Absolute;
            _dragonVisualHost.style.left = 0;
            _dragonVisualHost.style.top = 0;
            _dragonVisualHost.style.right = 0;
            _dragonVisualHost.style.bottom = 0;
            hierarchy.Add(_dragonVisualHost);

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
            UpdateDragonVisuals();
            UpdateWordTargets();
            UpdateWords(deltaTime);
        }

        public void Rebuild()
        {
            if (_dragonVisualHost == null || _wordHost == null)
            {
                Debug.LogError("[DragonTextLayer] Visual hosts were not initialized.");
                return;
            }

            _dragonVisualHost.Clear();
            _dragonSegments.Clear();
            _dragonConnectors.Clear();

            _wordHost.Clear();
            _words.Clear();
            _lines.Clear();
            _dragon = Array.Empty<DragonNode>();
            _isBuilt = false;

            if (resolvedStyle.width < 16f || resolvedStyle.height < 16f)
                return;

            BuildWords();
            BuildDragon();
            BuildDragonVisuals();
            UpdateExclusionRect();
            _isBuilt = true;
            MarkDirtyRepaint();
        }

        private void BuildDragonVisuals()
        {
            _dragonVisualHost.Clear();
            _dragonSegments.Clear();
            _dragonConnectors.Clear();

            for (int i = 0; i < Mathf.Max(0, _dragon.Length - 1); i++)
            {
                VisualElement connector = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };

                connector.style.position = Position.Absolute;
                connector.style.backgroundColor = new Color(0.50f, 0.78f, 1f, 0.52f);
                connector.style.transformOrigin = new TransformOrigin(0f, 50f, 0f);
                connector.style.borderTopLeftRadius = 999f;
                connector.style.borderTopRightRadius = 999f;
                connector.style.borderBottomLeftRadius = 999f;
                connector.style.borderBottomRightRadius = 999f;

                _dragonVisualHost.Add(connector);
                _dragonConnectors.Add(connector);
            }

            for (int i = 0; i < _dragon.Length; i++)
            {
                VisualElement seg = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };

                seg.style.position = Position.Absolute;
                seg.style.backgroundColor = new Color(0.50f, 0.78f, 1f, 0.72f);
                seg.style.borderTopLeftRadius = 999f;
                seg.style.borderTopRightRadius = 999f;
                seg.style.borderBottomLeftRadius = 999f;
                seg.style.borderBottomRightRadius = 999f;

                _dragonVisualHost.Add(seg);
                _dragonSegments.Add(seg);
            }
        }

        private void UpdateDragonVisuals()
        {
            if (_dragonSegments.Count != _dragon.Length)
                return;

            for (int i = 0; i < _dragonConnectors.Count; i++)
            {
                DragonNode a = _dragon[i];
                DragonNode b = _dragon[i + 1];
                VisualElement connector = _dragonConnectors[i];

                Vector2 delta = b.Position - a.Position;
                float length = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                float thickness = Mathf.Max(2f, Mathf.Min(a.Radius, b.Radius) * 2f * ConnectorThickness);
                float t = i / (float)Mathf.Max(1, _dragonConnectors.Count - 1);
                float alpha = Mathf.Lerp(0.52f, 0.08f, t);

                connector.style.left = a.Position.x;
                connector.style.top = a.Position.y - thickness * 0.5f;
                connector.style.width = length + Mathf.Max(a.Radius, b.Radius) * 0.25f;
                connector.style.height = thickness;
                connector.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                connector.style.opacity = alpha;
                connector.style.backgroundColor = Color.Lerp(
                    new Color(0.30f, 0.60f, 1f, alpha),
                    new Color(0.72f, 0.88f, 1f, alpha),
                    0.35f
                );
            }

            for (int i = 0; i < _dragon.Length; i++)
            {
                DragonNode node = _dragon[i];
                VisualElement seg = _dragonSegments[i];

                float diameter = node.Radius * 2f;
                float t = i / (float)Mathf.Max(1, _dragon.Length - 1);

                float alpha = Mathf.Lerp(0.86f, 0.14f, t);
                Color c = Color.Lerp(
                    new Color(0.35f, 0.65f, 1f, alpha),
                    new Color(0.80f, 0.93f, 1f, alpha),
                    0.35f
                );

                seg.style.width = diameter;
                seg.style.height = diameter;
                seg.style.left = node.Position.x - node.Radius;
                seg.style.top = node.Position.y - node.Radius;
                seg.style.backgroundColor = c;
                seg.style.opacity = alpha;
            }
        }

        private void BuildWords()
        {
            float usableWidth = Mathf.Max(120f, resolvedStyle.width - HorizontalPadding * 2f);
            float leftX = HorizontalPadding;
            float topY = VerticalPadding;

            float cursorX = leftX;
            float cursorY = topY;

            LineData currentLine = CreateLine(cursorY, leftX, usableWidth);
            bool lastWasSpace = false;

            foreach (char c in _sourceText)
            {
                if (c == '\r')
                    continue;

                if (c == '\n')
                {
                    _lines.Add(currentLine);
                    cursorY += LineHeight + LineSpacing;
                    cursorX = leftX;
                    currentLine = CreateLine(cursorY, leftX, usableWidth);
                    lastWasSpace = false;
                    continue;
                }

                bool isWhitespace = c == ' ' || c == '\t';
                float glyphWidth = EstimateGlyphWidth(c);

                bool needsWrap = currentLine.Words.Count > 0 && (cursorX - leftX + glyphWidth) > usableWidth;
                if (needsWrap)
                {
                    // tránh xuống dòng với khoảng trắng mở đầu dòng mới
                    if (isWhitespace)
                    {
                        _lines.Add(currentLine);
                        cursorY += LineHeight + LineSpacing;
                        cursorX = leftX;
                        currentLine = CreateLine(cursorY, leftX, usableWidth);
                        lastWasSpace = true;
                        continue;
                    }

                    _lines.Add(currentLine);
                    cursorY += LineHeight + LineSpacing;
                    cursorX = leftX;
                    currentLine = CreateLine(cursorY, leftX, usableWidth);
                }

                if (isWhitespace && currentLine.Words.Count == 0)
                {
                    lastWasSpace = true;
                    continue;
                }

                if (isWhitespace && lastWasSpace)
                    continue;

                Label label = new(c == ' ' ? "\u00A0" : c.ToString())
                {
                    pickingMode = PickingMode.Ignore
                };
                label.style.position = Position.Absolute;
                label.style.left = cursorX;
                label.style.top = cursorY;
                label.style.fontSize = FontSize;
                label.style.color = isWhitespace
                    ? new Color(0.94f, 0.97f, 1f, 0.02f)
                    : new Color(0.94f, 0.97f, 1f, 0.10f);
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                _wordHost.Add(label);

                WordData word = new()
                {
                    Text = c.ToString(),
                    Label = label,
                    LineIndex = _lines.Count,
                    Width = glyphWidth,
                    Height = LineHeight,
                    BaseX = cursorX,
                    BaseY = cursorY,
                    CurrentX = cursorX,
                    TargetX = cursorX,
                    IsWhitespace = isWhitespace
                };

                currentLine.Words.Add(word);
                _words.Add(word);
                cursorX += glyphWidth + GlyphSpacing;
                lastWasSpace = isWhitespace;
            }

            if (currentLine.Words.Count > 0 || _lines.Count == 0)
                _lines.Add(currentLine);
        }

        private static LineData CreateLine(float y, float leftX, float usableWidth)
        {
            return new LineData
            {
                Y = y,
                Height = LineHeight,
                MinX = leftX,
                MaxX = leftX + usableWidth
            };
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

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screen);
            float panelHeight = panel.visualTree.worldBound.height;
            if (panelHeight <= 0f)
                panelHeight = resolvedStyle.height;

            _mouseLocal = new Vector2(panelPos.x, panelHeight - panelPos.y);

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
                targetHead += new Vector2(0f, Mathf.Sin(Time.unscaledTime * IdleFrequency) * IdleAmplitude);

            float headBlend = 1f - Mathf.Exp(-34f * deltaTime);
            _dragon[0].Position = Vector2.Lerp(_dragon[0].Position, targetHead, headBlend);

            if (_hasExclusionRect && _cachedExclusionLocalRect.Contains(_dragon[0].Position))
                _dragon[0].Position = ClosestPointOnRectEdge(_cachedExclusionLocalRect, _dragon[0].Position);

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
                float bodyBlend = 1f - Mathf.Exp(-30f * deltaTime);
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
                if (line.Words.Count == 0)
                    continue;

                float lineCenterY = line.Y + line.Height * 0.5f;

                for (int w = 0; w < line.Words.Count; w++)
                {
                    WordData word = line.Words[w];
                    float baseCenterX = word.BaseX + word.Width * 0.5f;
                    float totalPush = 0f;

                    for (int i = 0; i < _dragon.Length; i++)
                    {
                        DragonNode node = _dragon[i];
                        float effectiveRadius = node.Radius + line.Height * 0.5f + LineInfluencePadding;
                        float dy = Mathf.Abs(node.Position.y - lineCenterY);
                        if (dy >= effectiveRadius)
                            continue;

                        float halfWidth = Mathf.Sqrt(Mathf.Max(0f, effectiveRadius * effectiveRadius - dy * dy));
                        float dx = baseCenterX - node.Position.x;
                        float absDx = Mathf.Abs(dx);
                        if (absDx > halfWidth + LocalInfluenceFalloff)
                            continue;

                        float edgeDistance = Mathf.Max(0f, absDx - halfWidth);
                        float influence = 1f - Mathf.Clamp01(edgeDistance / LocalInfluenceFalloff);
                        if (influence <= 0f)
                            continue;

                        float direction = dx >= 0f ? 1f : -1f;
                        float pushAmount = PushPadding + node.Radius * 0.90f;
                        if (word.IsWhitespace)
                            pushAmount *= SpacePushMultiplier;

                        totalPush += direction * influence * pushAmount;
                    }

                    totalPush = Mathf.Clamp(totalPush, -MaxWordPush, MaxWordPush);
                    totalPush *= word.IsWhitespace ? 0.45f : 0.82f;
                    word.TargetX = word.BaseX + totalPush;
                }

                ApplyExclusionPush(line);
                RelaxLine(line);
                ClampLineToBounds(line);
            }
        }

        private void ApplyExclusionPush(LineData line)
        {
            if (!_hasExclusionRect)
                return;

            float lineCenterY = line.Y + line.Height * 0.5f;
            if (lineCenterY < _cachedExclusionLocalRect.yMin || lineCenterY > _cachedExclusionLocalRect.yMax)
                return;

            float exclusionCenter = (_cachedExclusionLocalRect.xMin + _cachedExclusionLocalRect.xMax) * 0.5f;

            for (int i = 0; i < line.Words.Count; i++)
            {
                WordData word = line.Words[i];
                float baseCenterX = word.BaseX + word.Width * 0.5f;
                float dx = baseCenterX - exclusionCenter;
                float distanceToRect = 0f;

                if (baseCenterX < _cachedExclusionLocalRect.xMin)
                    distanceToRect = _cachedExclusionLocalRect.xMin - baseCenterX;
                else if (baseCenterX > _cachedExclusionLocalRect.xMax)
                    distanceToRect = baseCenterX - _cachedExclusionLocalRect.xMax;

                if (distanceToRect > LocalInfluenceFalloff)
                    continue;

                float influence = 1f - Mathf.Clamp01(distanceToRect / LocalInfluenceFalloff);
                float direction = dx >= 0f ? 1f : -1f;
                float push = direction * influence * (word.IsWhitespace ? 12f : 26f);
                word.TargetX += push;
            }
        }

        private static void RelaxLine(LineData line)
        {
            if (line.Words.Count == 0)
                return;

            for (int pass = 0; pass < 7; pass++)
            {
                for (int i = 1; i < line.Words.Count; i++)
                {
                    WordData prev = line.Words[i - 1];
                    WordData cur = line.Words[i];
                    float minX = prev.TargetX + prev.Width + GlyphSpacing;
                    if (cur.TargetX < minX)
                        cur.TargetX = minX;
                }

                for (int i = line.Words.Count - 2; i >= 0; i--)
                {
                    WordData cur = line.Words[i];
                    WordData next = line.Words[i + 1];
                    float maxX = next.TargetX - cur.Width - GlyphSpacing;
                    if (cur.TargetX > maxX)
                        cur.TargetX = maxX;
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
            const float smoothTime = 0.11f;
            const float maxSpeed = 1600f;

            foreach (WordData word in _words)
            {
                word.CurrentX = Mathf.SmoothDamp(
                    word.CurrentX,
                    word.TargetX,
                    ref word.VelocityX,
                    smoothTime,
                    maxSpeed,
                    deltaTime
                );

                word.Label.style.left = word.CurrentX;

                if (word.IsWhitespace)
                {
                    word.Label.style.opacity = 0.001f;
                    continue;
                }

                float offset = Mathf.Abs(word.CurrentX - word.BaseX);
                float alpha = Mathf.Lerp(0.10f, 0.26f, Mathf.InverseLerp(0f, 80f, offset));
                word.Label.style.color = new Color(0.94f, 0.97f, 1f, alpha);
            }
        }

        private static float EstimateGlyphWidth(char c)
        {
            if (c == ' ' || c == '\t')
                return 9f;

            if ("WM@#%&QGOD".IndexOf(c) >= 0) return 17f;
            if ("mw".IndexOf(c) >= 0) return 15f;
            if ("Iil1|!'".IndexOf(c) >= 0) return 7f;
            if (".,:;".IndexOf(c) >= 0) return 6f;
            return 12f;
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

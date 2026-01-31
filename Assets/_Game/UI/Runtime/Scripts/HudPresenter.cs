using System;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class HudPresenter
    {
        private readonly GameServices _s;
        private readonly Label _lblTime;
        private readonly Label _lblPhase;

        private readonly Button _btn1;
        private readonly Button _btn2;
        private readonly Button _btn3;

        private int _yearIndex = 1;

        // store delegates so Unbind works (không dùng lambda inline để remove)
        private Action _onBtn1;
        private Action _onBtn2;
        private Action _onBtn3;

        public HudPresenter(VisualElement root, GameServices s)
        {
            _s = s;

            _lblTime = root.Q<Label>("LblTime");
            _lblPhase = root.Q<Label>("LblPhase");

            _btn1 = root.Q<Button>("BtnSpeed1");
            _btn2 = root.Q<Button>("BtnSpeed2");
            _btn3 = root.Q<Button>("BtnSpeed3");
        }

        public void Bind()
        {
            // 1) Subscribe event bus (nếu có) để lắng nghe những event publish chuẩn hệ thống
            if (_s?.EventBus != null)
            {
                _s.EventBus.Subscribe<DayStartedEvent>(OnDayStarted);
                _s.EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChangedEvt);
                _s.EventBus.Subscribe<TimeScaleChangedEvent>(OnTimeScaleChangedEvt);
            }

            // 2) Bind button click
            _onBtn1 = () => TrySetSpeed(1f);
            _onBtn2 = () => TrySetSpeed(2f);
            _onBtn3 = () => TrySetSpeed(3f);

            if (_btn1 != null) _btn1.clicked += _onBtn1;
            if (_btn2 != null) _btn2.clicked += _onBtn2;
            if (_btn3 != null) _btn3.clicked += _onBtn3;

            // 3) Paint initial state ngay lập tức (không phụ thuộc DayStartedEvent)
            var rc = _s?.RunClock;
            if (rc != null)
            {
                _yearIndex = ReadYearIndexBestEffort(rc, fallback: 1);
                PaintTime(rc.CurrentSeason, rc.DayIndex, _yearIndex);
                PaintPhase(rc.CurrentPhase);
                PaintSpeed(rc.TimeScale);
                PaintSpeedAvailability(rc.CurrentPhase);
            }
            else
            {
                PaintTime(Season.Spring, 1, 1);
                PaintPhase(Phase.Build);
                PaintSpeed(1f);
                PaintSpeedAvailability(Phase.Build);
            }
        }

        public void Unbind()
        {
            if (_s?.EventBus != null)
            {
                _s.EventBus.Unsubscribe<DayStartedEvent>(OnDayStarted);
                _s.EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChangedEvt);
                _s.EventBus.Unsubscribe<TimeScaleChangedEvent>(OnTimeScaleChangedEvt);
            }

            if (_btn1 != null && _onBtn1 != null) _btn1.clicked -= _onBtn1;
            if (_btn2 != null && _onBtn2 != null) _btn2.clicked -= _onBtn2;
            if (_btn3 != null && _onBtn3 != null) _btn3.clicked -= _onBtn3;

            _onBtn1 = null; _onBtn2 = null; _onBtn3 = null;
        }

        private void TrySetSpeed(float s)
        {
            var rc = _s?.RunClock;
            if (rc == null) return;

            // Defend: nếu chưa unlock thì clamp về x1 (RunClockService cũng clamp, đây chỉ để UI predictable)
            if (rc.CurrentPhase == Phase.Defend && !rc.DefendSpeedUnlocked && s > 1f)
                s = 1f;

            rc.SetTimeScale(s);

            // Safety paint ngay (trường hợp event bus chưa tới UI vì setup)
            PaintSpeed(rc.TimeScale);

            // Debug hữu ích: nếu bấm mà không thấy thay đổi thì biết click đã vào TrySetSpeed hay chưa
            Debug.Log($"[UI] SetTimeScale request={s} applied={rc.TimeScale} phase={rc.CurrentPhase} defendUnlocked={rc.DefendSpeedUnlocked}");
        }

        private void OnDayStarted(DayStartedEvent ev)
        {
            _yearIndex = ev.YearIndex > 0 ? ev.YearIndex : _yearIndex;
            PaintTime(ev.Season, ev.DayIndex, _yearIndex);
            PaintPhase(ev.Phase);
            PaintSpeedAvailability(ev.Phase);
        }

        private void OnPhaseChangedEvt(PhaseChangedEvent ev)
        {
            PaintPhase(ev.To);
            PaintSpeedAvailability(ev.To);

            var rc = _s?.RunClock;
            if (rc != null) PaintSpeed(rc.TimeScale);
        }

        private void OnTimeScaleChangedEvt(TimeScaleChangedEvent ev)
        {
            PaintSpeed(ev.To);
        }

        private void PaintTime(Season season, int dayIndex, int yearIndex)
        {
            if (_lblTime == null) return;
            _lblTime.text = $"Year {yearIndex} • {season} D{dayIndex}";
        }

        private void PaintPhase(Phase p)
        {
            if (_lblPhase == null) return;

            _lblPhase.text = p.ToString();

            _lblPhase.RemoveFromClassList("is-build");
            _lblPhase.RemoveFromClassList("is-defend");
            if (p == Phase.Build) _lblPhase.AddToClassList("is-build");
            else _lblPhase.AddToClassList("is-defend");
        }

        private void PaintSpeed(float s)
        {
            SetActive(_btn1, s <= 1.01f);
            SetActive(_btn2, s > 1.01f && s < 2.51f);
            SetActive(_btn3, s >= 2.51f);
        }

        private void PaintSpeedAvailability(Phase phase)
        {
            var rc = _s?.RunClock;
            if (rc == null) return;

            bool allowFast = phase != Phase.Defend || rc.DefendSpeedUnlocked;
            if (_btn2 != null) _btn2.SetEnabled(allowFast);
            if (_btn3 != null) _btn3.SetEnabled(allowFast);
        }

        private static void SetActive(Button b, bool active)
        {
            if (b == null) return;
            if (active) b.AddToClassList("is-active");
            else b.RemoveFromClassList("is-active");
        }

        /// <summary>
        /// RunClock interface không expose YearIndex; project bạn có concrete RunClockService.YearIndex.
        /// Đọc bằng reflection để UI không bị "cứng" vào concrete, nhưng vẫn hiển thị Year ngay lập tức.
        /// </summary>
        private static int ReadYearIndexBestEffort(IRunClock rc, int fallback)
        {
            try
            {
                var t = rc.GetType();
                var p = t.GetProperty("YearIndex", BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(int))
                {
                    var v = (int)p.GetValue(rc);
                    if (v > 0) return v;
                }
            }
            catch { /* ignore */ }
            return fallback;
        }
    }
}

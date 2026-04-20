using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Day41 - Tutorial hints (anti-spam, shipable).
    /// Active only in first 10 minutes (sim time).
    /// Triggers:
    /// - unassigned NPC
    /// - producer full
    /// - out of ammo (during defend + enemies exist)
    /// - wave incoming (phase change to defend)
    /// </summary>
    public sealed class TutorialHintsService : ITickable, IResettable
    {
        private readonly GameServices _s;

        // Only show hints in first 10 minutes
        private const float ActiveWindowSeconds = 10f * 60f;

        // scan cadence
        private const float ScanNpcInterval = 6f;
        private const float ScanProducerInterval = 6f;
        private const float ScanAmmoInterval = 3f;

        private float _runAge;
        private float _npcAcc;
        private float _prodAcc;
        private float _ammoAcc;

        private bool _hadUnassignedNpc;
        private bool _hadProducerFull;
        private bool _hadTowerOutOfAmmo;

        // anti-repeat per season defend hint
        private int _lastWaveIncomingSeasonKey = -1;

        private int _currentYear = 1;

        // debug
        public int HintNpcUnassignedCount { get; private set; }
        public int HintProducerFullCount { get; private set; }
        public int HintOutOfAmmoCount { get; private set; }
        public int HintWaveIncomingCount { get; private set; }
        public float LastHintRealtime { get; private set; }
        public float RunAge => _runAge;

        public TutorialHintsService(GameServices s)
        {
            _s = s;

            // event-based: wave incoming
            _s?.EventBus?.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
            _s?.EventBus?.Subscribe<DayStartedEvent>(OnDayStarted);
        }

        public void Reset()
        {
            _runAge = 0f;
            _npcAcc = 0f;
            _prodAcc = 0f;
            _ammoAcc = 0f;

            _hadUnassignedNpc = false;
            _hadProducerFull = false;
            _hadTowerOutOfAmmo = false;

            _lastWaveIncomingSeasonKey = -1;
            _currentYear = 1;

            HintNpcUnassignedCount = 0;
            HintProducerFullCount = 0;
            HintOutOfAmmoCount = 0;
            HintWaveIncomingCount = 0;
            LastHintRealtime = 0f;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_s == null || _s.NotificationService == null || _s.WorldState == null || _s.RunClock == null) return;

            _runAge += dt;
            if (_runAge > ActiveWindowSeconds) return; // stop after 10 minutes

            // scan conditions
            _npcAcc += dt;
            if (_npcAcc >= ScanNpcInterval)
            {
                _npcAcc -= ScanNpcInterval;
                TryHintUnassignedNpc();
            }

            _prodAcc += dt;
            if (_prodAcc >= ScanProducerInterval)
            {
                _prodAcc -= ScanProducerInterval;
                TryHintProducerFull();
            }

            _ammoAcc += dt;
            if (_ammoAcc >= ScanAmmoInterval)
            {
                _ammoAcc -= ScanAmmoInterval;
                TryHintOutOfAmmo();
            }
        }

        private void OnDayStarted(DayStartedEvent ev)
        {
            _currentYear = ev.YearIndex;

            // If new run forces back to Spring Day1 Year1, allow wave incoming hint again later.
            if (ev.YearIndex == 1 && ev.Season == Season.Spring && ev.DayIndex == 1)
                _lastWaveIncomingSeasonKey = -1;            
        }

        private void OnPhaseChanged(PhaseChangedEvent ev)
        {
            if (_s == null || _s.NotificationService == null || _s.RunClock == null) return;
            if (_runAge > ActiveWindowSeconds) return;

            if (ev.To == Phase.Defend)
            {
                int seasonKey = (_currentYear * 10) + (int)_s.RunClock.CurrentSeason;
                if (_lastWaveIncomingSeasonKey == seasonKey) return;

                _lastWaveIncomingSeasonKey = seasonKey;

                PushHint(
                    key: "hint.wave.incoming",
                    title: "Hint",
                    body: "Wave incoming! Hãy đặt tower, kiểm tra ammo và assign NPC trước khi vào phòng thủ.",
                    cooldown: 45f);

                HintWaveIncomingCount++;
            }
        }

        private void TryHintUnassignedNpc()
        {
            var w = _s.WorldState;
            if (w?.Npcs == null) return;

            // avoid showing immediately at t=0 and give player time to set up first jobs
            if (_runAge < 45f) return;

            int unassigned = 0;
            foreach (var id in w.Npcs.Ids)
            {
                var st = w.Npcs.Get(id);
                if (st.Workplace.Value == 0) unassigned++;
            }

            if (unassigned <= 0)
            {
                _hadUnassignedNpc = false;
                return;
            }

            if (_hadUnassignedNpc) return;
            _hadUnassignedNpc = true;

            PushHint(
                key: "hint.npc.unassigned",
                title: "Gợi ý",
                body: $"Bạn còn {unassigned} NPC chưa được giao việc. Hãy assign họ vào workplace phù hợp để tăng sản xuất và tiếp tế.",
                cooldown: 90f);

            HintNpcUnassignedCount++;
        }

        private void TryHintProducerFull()
        {
            var w = _s.WorldState;
            var data = _s.DataRegistry;
            var storage = _s.StorageService;
            if (w == null || data == null || storage == null || w.Buildings == null) return;

            // Only in Build phase to reduce noise
            if (_s.RunClock.CurrentPhase == Phase.Defend) return;

            bool anyFull = false;

            foreach (var bid in w.Buildings.Ids)
            {
                var bs = w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (!data.TryGetBuilding(bs.DefId, out var bdef) || bdef == null || !bdef.IsProducer)
                    continue;

                if (!TryGetProducedResource(bs.DefId, out var rt)) continue;

                int cap = storage.GetCap(bid, rt);
                if (cap <= 0) continue;

                int cur = storage.GetAmount(bid, rt);
                if (cur >= cap)
                {
                    anyFull = true;
                    break;
                }
            }

            if (!anyFull)
            {
                _hadProducerFull = false;
                return;
            }

            if (_hadProducerFull) return;
            _hadProducerFull = true;

            PushHint(
                key: "hint.producer.full",
                title: "Gợi ý",
                body: "Một số công trình sản xuất đang đầy kho. Hãy haul tài nguyên đi hoặc mở rộng storage để tránh đứng sản xuất.",
                cooldown: 60f);

            HintProducerFullCount++;
        }

        private void TryHintOutOfAmmo()
        {
            // Only meaningful during Defend when enemies exist
            if (_s.RunClock.CurrentPhase != Phase.Defend) return;

            var w = _s.WorldState;
            if (w == null || w.Buildings == null || w.Enemies == null) return;
            if (w.Enemies.Count <= 0) return;

            bool anyTowerEmpty = false;

            foreach (var bid in w.Buildings.Ids)
            {
                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;

                if (!_s.DataRegistry.TryGetBuilding(b.DefId, out var def) || def == null || !def.IsTower)
                    continue;

                if (b.Ammo <= 0)
                {
                    anyTowerEmpty = true;
                    break;
                }
            }

            if (!anyTowerEmpty)
            {
                _hadTowerOutOfAmmo = false;
                return;
            }

            if (_hadTowerOutOfAmmo) return;
            _hadTowerOutOfAmmo = true;

            PushHint(
                key: "hint.tower.out_of_ammo",
                title: "Cảnh báo",
                body: "Có tower đang hết ammo khi đang phòng thủ. Hãy craft hoặc tiếp tế ammo từ Armory ngay.",
                cooldown: 45f);

            HintOutOfAmmoCount++;
        }

        private void PushHint(string key, string title, string body, float cooldown)
        {
            _s.NotificationService?.Push(
                key: key,
                title: title,
                body: body,
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: cooldown,
                dedupeByKey: true
            );

            LastHintRealtime = Time.realtimeSinceStartup;
        }

        private static bool TryGetProducedResource(string defId, out ResourceType rt)
        {
            // match Day36 subset
            if (string.Equals(defId, "bld_farmhouse", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Food; return true; }
            if (string.Equals(defId, "bld_lumbercamp", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Wood; return true; }
            rt = default;
            return false;
        }
    }
}

# PART 20 — AUDIO & FEEDBACK DESIGN (SFX CUES + MIX RULES + ACCESSIBILITY AUDIO) — SPEC v0.1

> Mục tiêu: làm game “có lực” và dễ hiểu trạng thái qua âm thanh:
- SFX cues cho core loop: build, harvest, haul, craft, resupply, tower fire, enemy hit, wave start/end, reward pick
- Mixer groups + volume settings
- Spatial audio tối giản (2D/3D hybrid)
- Audio accessibility: notification sounds toggle, dynamic range, avoid spam
- Implementation pattern data-driven (AudioEventId)

---

## 1) Audio philosophy for this game
- Âm thanh phải “tóm tắt trạng thái” khi người chơi không nhìn UI:
  - tower out of ammo, storage full, objective complete
- Tránh “ồn ào” do NPC/job spam:
  - throttle per event key
- Ưu tiên:
  1) Combat readability
  2) Reward/achievement feedback
  3) Economy ambience

---

## 2) Mixer structure (Unity AudioMixer)

### 2.1 Mixer groups
- Master
  - Music
  - SFX
    - UI
    - Economy
    - Combat
    - Notifications
  - Ambience

### 2.2 Settings mapping
- MasterVolume (0–1)
- MusicVolume
- SFXVolume
- UIVolume
- NotificationVolume
- AmbienceVolume

All stored in settings.json.

---

## 3) AudioEvent system (data-driven)

### 3.1 AudioEventId
```csharp
public enum AudioEventId
{
    UI_Click,
    UI_OpenPanel,
    UI_ClosePanel,

    Build_PlaceConfirm,
    Build_Complete,
    Build_Block,

    NPC_Assign,
    NPC_Unassign,

    Resource_HarvestTick,     // optional, likely too spammy
    Resource_HarvestComplete,
    Resource_HaulDeliver,

    Ammo_CraftComplete,
    Ammo_ResupplyDeliver,
    Ammo_TowerLow,            // notification cue
    Ammo_TowerEmpty,

    Combat_WaveStart,
    Combat_WaveEnd,
    Combat_TowerShot,
    Combat_EnemyHit,
    Combat_EnemyDeath,
    Combat_HQHit,
    Combat_Defeat,
    Combat_Victory,

    Reward_Show,
    Reward_Pick,

    Objective_Complete,
    Notification_Info,
    Notification_Warn,
    Notification_Error
}
```

### 3.2 AudioEventDef
```csharp
[CreateAssetMenu(menuName="Game/Audio/AudioEventDef")]
public sealed class AudioEventDef : ScriptableObject
{
    public AudioEventId Id;
    public UnityEngine.AudioClip[] Clips;  // pick random
    public UnityEngine.Audio.AudioMixerGroup MixerGroup;

    public float Volume = 1f;
    public float PitchMin = 1f;
    public float PitchMax = 1f;

    public bool Spatial;   // play at world position
    public float MinDistance = 3f;
    public float MaxDistance = 20f;

    public float CooldownSeconds = 0.0f; // spam control
}
```

### 3.3 AudioRegistry
- Loaded by DataRegistry (Part 1)
- Map AudioEventId → AudioEventDef

---

## 4) AudioService (runtime)

### 4.1 Responsibilities
- Play SFX by event id
- Apply cooldown (per id + optional per target)
- Support 2 modes:
  - UI/non-spatial
  - spatial at world position

### 4.2 API
```csharp
public interface IAudioService
{
    void Play(AudioEventId id);
    void PlayAt(AudioEventId id, UnityEngine.Vector3 worldPos);
}
```

### 4.3 Cooldown strategy
Use dictionary:
- key: (AudioEventId, optional targetId) → lastTime

v0.1 recommended:
- global cooldown per id (good enough)
- plus special per tower for Ammo_TowerLow/Empty (reuse Notification keys)

---

## 5) Cue mapping (what plays when)

### 5.1 UI
- Button clicks: UI_Click (subtle)
- Open/close panels: UI_OpenPanel/UI_ClosePanel
- Confirm dialogs: small confirm tone

### 5.2 Build loop
- Placement confirm: Build_PlaceConfirm
- Placement blocked: Build_Block (soft “thud”)
- Build complete: Build_Complete (pleasant, short)
- Objective complete: Objective_Complete (distinct)

### 5.3 Economy loop
- Harvest complete: Resource_HarvestComplete (only on completion, not ticks)
- Haul deliver: Resource_HaulDeliver (quiet)
- NPC assign: NPC_Assign

### 5.4 Ammo pipeline
- Craft complete: Ammo_CraftComplete
- Resupply deliver to tower: Ammo_ResupplyDeliver
- Tower low/empty: Ammo_TowerLow / Ammo_TowerEmpty (notification group, throttled)

### 5.5 Combat
- Wave start/end: Combat_WaveStart / Combat_WaveEnd
- Tower shot: Combat_TowerShot (spatial, short)
- Enemy hit/death: Combat_EnemyHit / Combat_EnemyDeath (spatial)
- HQ hit: Combat_HQHit (warning tone; throttle 1–2s)
- Defeat/Victory: big stingers

### 5.6 Reward flow
- Reward modal show: Reward_Show
- Pick: Reward_Pick

### 5.7 Notifications
- Info/Warn/Error cues:
  - Notification_Info (light)
  - Notification_Warn (mid)
  - Notification_Error (strong)
But careful not to double-play if you already play specific cues (ammo low).
Rule:
- If notification has dedicated cue, skip generic.

---

## 6) Spatial audio model (simple)

### 6.1 World emitters
- Use `OneShotAudioEmitterPool`:
  - pool of AudioSource objects
  - Play clip then return to pool
No instantiate during play.

### 6.2 2D vs 3D decisions
- UI and notifications: 2D
- Combat hits/shots: 3D
- Economy (harvest/haul): 2D or 3D low volume (choose 2D for clarity)

---

## 7) Music & ambience (minimal but effective)

### 7.1 Music layers (optional)
- Build phase track
- Defend phase track
Crossfade on season change.

### 7.2 Ambience
- Light wind/forest ambience based on map biome (later)
- Keep volume low, non-intrusive.

---

## 8) Audio accessibility

### 8.1 Settings
- Notification sound toggle
- Dynamic range:
  - “Night mode” (compress loud peaks)
- Reduce repetitive cues:
  - enable cooldowns
- Subtitle for key events (optional):
  - show “Wave bắt đầu” in text (already)

### 8.2 Spam prevention
Hard rules:
- Tower shot SFX:
  - cap max shots per second globally (e.g. 8) by skipping extra
- Enemy hit:
  - cap similarly
- HQ hit:
  - throttle 1.5s

---

## 9) Implementation steps (fast)

1) Create AudioMixer groups + expose params
2) Create AudioEventDefs for core events (20–30)
3) Implement AudioRegistry + AudioService
4) Hook into:
   - UI click (Screen binders)
   - Build pipeline events (site created/complete/blocked)
   - Job executor events (harvest complete, haul deliver)
   - Forge/Armory events (craft/resupply)
   - Combat events (wave start/end, tower shot, enemy death, HQ hit)
   - Reward flow
5) Add pooling for spatial sources
6) Add spam caps

---

## 10) QA Checklist (Part 20)
- [ ] All critical states have a cue (ammo empty, wave start, build complete)
- [ ] No audio spam with many towers/enemies (caps work)
- [ ] Mixer sliders persist & update live
- [ ] Defend phase default 1x still readable (sounds not too fast)
- [ ] Notification sounds can be disabled
- [ ] Spatial cues match world positions

---

## 11) Next Part (Part 21 đề xuất)
**Art/FX direction minimal**: VFX cues for build/repair, ammo resupply, tower shots, damage states; performance-friendly.


# PART 23 — ECONOMY/COMBAT TUNING PLAYBOOK (METRICS + TEST SEEDS + TARGET RUN LENGTH) — SPEC v0.1

> Mục tiêu: cân bằng game theo hướng “public fun” thay vì cảm tính:
- Xác định target run length + difficulty curve
- Bộ metrics bắt buộc để đo (economy + ammo + combat)
- Test seeds chuẩn hoá để so sánh thay đổi
- Quy trình tuning: change nhỏ → chạy test → so sánh → quyết định
- Anti-patterns: đừng “fix” bằng cách buff vô tội vạ

---

## 1) Target experience (v0.1 baseline)

### 1.1 Target run length (premium roguelite)
Chọn 1 mục tiêu rõ:
- **Short run**: 20–30 phút (dễ replay)
- **Standard run**: 35–60 phút (chiến thuật sâu)
- **Long run**: 60–90 phút (hardcore)

v0.1 recommended: **35–60 phút** (đủ xây + đủ defend + đủ reward choices).

### 1.2 Difficulty curve (feel)
- Early: “học + ổn định” (không chết vì thiếu ammo)
- Mid: “áp lực” (towers phải mở rộng + ammo pipeline quan trọng)
- Late: “bùng nổ” (boss wave, buộc tối ưu)

---

## 2) Golden metrics (must track)

### 2.1 Economy flow
- `res_produced[type]` per day
- `res_delivered_to_warehouse[type]` per day
- `producer_local_full_time[type]` (% time local full)
- `builder_waiting_for_resource_time` (seconds)
- `build_orders_completed_per_day`

### 2.2 Workforce / jobs
- `npc_idle_time_ratio` (%)
- `job_fail_counts[reason]`
- `avg_job_queue_len[workplace]`
- `avg_time_to_assign_job` (seconds)

### 2.3 Ammo pipeline
- `ammo_crafted_per_day`
- `ammo_in_armory_avg`
- `tower_low_ammo_time` (% time <=25%)
- `tower_empty_time` (% time ==0)
- `resupply_latency` (time from request -> delivered)
- `forge_starved_time` (% time no inputs)
- `armory_empty_time` (% time while requests exist)

### 2.4 Combat
- `enemies_spawned`
- `enemies_killed`
- `hq_damage_taken`
- `buildings_destroyed_count`
- `tower_shots_fired`
- `tower_hit_rate` (if you have projectiles; else approximate)
- `time_in_defend` (seconds)

### 2.5 Outcome
- `days_survived`
- `waves_cleared`
- `run_score` (Part 12)
- `run_outcome` (victory/defeat)

---

## 3) Instrumentation (how to collect)

### 3.1 Minimal telemetry logger (local)
- Write JSON lines to:
  - `Logs/run_metrics_<timestamp>.jsonl`
- Each “tick” event:
  - end-of-day snapshot
  - end-of-wave snapshot
  - end-of-run summary

### 3.2 Debug HUD (dev)
- Show key metrics live:
  - ammo low %, idle %, local full %
- Helps tune without opening file.

---

## 4) Standard test seeds (repeatability)

### 4.1 Why seeds
- Same map/placements/lane patterns → compare changes fairly
- Avoid “hên xui” hiding issues

### 4.2 Seed set (suggested)
- `SEED_A_EASY`: open map, 2 lanes
- `SEED_B_STANDARD`: normal clutter, 3 lanes
- `SEED_C_HARD`: tight roads, 3 lanes, longer distances
- `SEED_D_EDGE`: weird corner case placements

### 4.3 Test protocol
For each change:
- Run 3 seeds:
  - B, C, D (A optional)
- Run each seed 1 time for quick check, 3 times for serious tuning.

---

## 5) Tuning knobs (what to adjust)

### 5.1 Economy knobs
- Producer output per cycle
- Harvest work seconds
- Local storage caps
- Haul capacity per trip (if modeled)
- NPC move speed (road bonus)
- Warehouse cap

### 5.2 Build knobs
- Build costs
- Build seconds
- Upgrade costs/seconds
- Build order limits (queue cap)

### 5.3 Ammo knobs (most sensitive)
- Recipe input amounts
- Craft time
- Output ammo amount
- Forge local ammo cap
- Armory ammo cap
- Resupply chunk
- Tower ammo max
- Ammo per shot
- Low ammo threshold (25%)

### 5.4 Combat knobs
- Enemy HP, speed, damage
- Spawn counts, spawn interval
- Wave frequency per defend day
- Tower damage/range/fire interval

### 5.5 Rewards knobs
- rarity weights by day
- category weights by day
- effect magnitudes

---

## 6) The Tuning Loop (step-by-step)

### 6.1 Choose one problem statement
Examples:
- “Towers thường hết đạn ở day 3”
- “Builder đứng chờ tài nguyên quá nhiều”
- “Combat quá dễ, HQ không mất máu”

### 6.2 Identify metric threshold
Define target numbers (initial guess):
- tower_empty_time < 5% per defend day (early)
- resupply_latency < 20s average
- builder_waiting_for_resource_time < 15% of build phase
- npc_idle_time_ratio between 10–30% (not 0, not 80)

### 6.3 Change minimal
- Adjust 1–2 numbers only
- Keep a changelog line:
  - “Forge craft time -10%”
  - “Resupply chunk +5”

### 6.4 Run tests
- Run seeds B/C/D
- Collect end-of-day metrics

### 6.5 Compare deltas
- % improvement, check side effects:
  - if ammo fixed but economy breaks, revert
- Only keep change if improves target without major regression.

---

## 7) Common tuning patterns (practical recipes)

### 7.1 Fix tower ammo starvation (early)
If `tower_empty_time` high AND `forge_starved_time` low:
- Increase ammo output per craft OR reduce ammo per shot OR increase armory resupply chunk
If `forge_starved_time` high:
- Reduce recipe input requirements OR increase harvest output OR add extra transporter
If `resupply_latency` high:
- Increase ArmoryRunner count OR reduce distances (unlock placement) OR prioritize urgent requests

### 7.2 Fix warehouse bottleneck
If `producer_local_full_time` high:
- Increase transporter availability
- Increase warehouse capacity
- Increase road speed bonus

### 7.3 Fix builder stuck
If `builder_waiting_for_resource_time` high:
- Lower build costs slightly
- Or increase production/haul rate
- Or allow builder to take from producer local (already in rules)

### 7.4 Fix combat too easy
- Increase spawn count gradually (5–10%)
- Increase enemy HP slightly
- Decrease tower damage slightly
Avoid buffing enemy damage too early (feels unfair).

### 7.5 Fix combat too hard
- Reduce spawn count/HP
- Increase tower ammo max or reduce ammo per shot
- Add early reward that boosts ammo pipeline

---

## 8) Reward tuning guidelines
- Rewards should “solve problems”, not only “make bigger numbers”.
- Ensure at least 1 of 3 choices is relevant to player’s current state:
  - if ammo issues, offer ammo/craft/resupply buff sometimes
- Avoid runaway scaling:
  - cap stacks or reduce magnitudes at high rarity.

---

## 9) Anti-patterns (avoid)
- Fix everything by increasing production output (kills strategy)
- Make enemies hit too hard early (feels random/cheap)
- Too many rewards too often (decision fatigue)
- Making towers never run out of ammo (ammo pipeline becomes pointless)
- Changing 10 numbers at once (no idea what worked)

---

## 10) Release balancing cadence
- Early Access:
  - weekly balance patch
  - collect feedback: “run too long/short”, “ammo frustrating”
- Use metrics + player feedback; never only one.

---

## 11) Deliverable format (for you)
Maintain:
- `BalanceChangelog.md`:
  - date, change, reason, result metrics
- `Seeds.md`:
  - seed list + notes
- `RunMetrics/` folder for jsonl logs

---

## 12) QA Checklist (Part 23)
- [ ] With standard seeds, early defend day rarely ends due to ammo empty
- [ ] Mid-game ammo pipeline becomes necessary but manageable
- [ ] Run length roughly matches target bracket
- [ ] Changes are traceable (changelog)
- [ ] No single reward breaks difficulty curve

---

## 13) Next Part (Part 24 đề xuất)
**Monetization & product strategy (premium target 1 tỷ)**: pricing, content scope, EA roadmap, DLC plan, marketing beats.


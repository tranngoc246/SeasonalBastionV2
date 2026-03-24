# SEASONAL BASTION — VERTICAL SLICE ROADMAP v1.0 (VN)

> Mục đích: Biến bộ GDD/spec hiện tại thành kế hoạch thực thi có thứ tự, có milestone, có tiêu chí hoàn thành.
> Phạm vi: Từ vertical slice đầu tiên đến trạng thái base game có 1 run hoàn chỉnh.
> Tài liệu nền:
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`

---

## 1. Mục tiêu roadmap

Roadmap này nhằm đạt 3 mốc rõ ràng:
1. **Vertical Slice Playable** — chứng minh core loop có thật và “vào”.
2. **Year 1 Complete** — chứng minh nửa đầu game có progression, pressure và payoff.
3. **Base Run Complete** — chứng minh game có thể chơi từ menu tới win/lose/sumary như một sản phẩm hoàn chỉnh.

---

## 2. Nguyên tắc thực thi

1. **Loop trước, content sau** — chỉ thêm content khi loop hiện tại đã đọc được và vui.
2. **Player-facing risk first** — ưu tiên các vấn đề người chơi cảm nhận trực tiếp.
3. **Không mở scope song song** — mỗi milestone chỉ giải quyết một tầng vấn đề chính.
4. **Mỗi milestone phải playable** — không tích nợ quá nhiều phần “để sau mới ghép”.
5. **Mọi thay đổi lớn phải quay lại GDD/spec** — tránh drift thiết kế.

---

## 3. Định nghĩa các milestone

### M0 — Design & Scope Lock Lite
Mục tiêu:
- chốt tài liệu nền
- chốt scope gần
- dừng việc viết thêm tài liệu lớn

Deliverables:
- GDD complete
- UI spec
- UX/Onboarding spec
- Content scope spec
- Roadmap này

Exit criteria:
- có 1 bộ tài liệu đủ để làm việc tiếp mà không cần tranh luận lại product direction mỗi ngày

---

### M1 — Vertical Slice Playable
Mục tiêu:
- chứng minh Seasonal Bastion “là game gì” trong 1 build chơi được
- xác nhận road/placement + labor + logistics + defend thực sự dính vào nhau

### M2 — Year 1 Complete
Mục tiêu:
- chứng minh Year 1 có progression, seasonal pacing, bottleneck và payoff đủ rõ

### M3 — Base Run Complete
Mục tiêu:
- có thể chơi trọn 1 run từ menu đến win/lose/summary

---

## 4. Milestone M1 — Vertical Slice Playable

## 4.1 Trải nghiệm mục tiêu
Người chơi phải chơi được một phiên bản ngắn mà sau 15–30 phút có thể nói:
- mình hiểu assignment có ý nghĩa
- mình hiểu road/entry là luật thật
- mình hiểu resource không tự tăng
- mình hiểu tower cần logistics / ammo
- mình cảm thấy mùa defend là bài kiểm tra thật

## 4.2 Hệ thống bắt buộc trong M1

### A. Run start / map baseline
- New Run hoạt động
- Start map 64x64 hoặc tương đương baseline
- HQ + 2 House + Farm + Lumber + Arrow Tower + road seed
- Start NPC set hợp lệ

### B. Core loop cơ bản
- season/day clock tối thiểu
- Build/Defend phase chuyển đổi được
- speed controls cơ bản

### C. Placement
- build placement bằng road/entry hợp lệ
- ghost valid/invalid rõ
- fail reason đủ đọc

### D. Workforce / assignment
- NPC unassigned/assigned flow
- workplace slots
- assignment panel tối thiểu dùng được

### E. Economy / logistics tối thiểu
- harvest
- local storage cơ bản
- haul basic tối thiểu
- build/repair dùng resource thật

### F. Ammo pipeline tối thiểu
- Forge craft ammo
- Armory dispatch ammo
- Tower low ammo request
- tower out-of-ammo thì ngừng bắn

### G. Defend pressure tối thiểu
- 1 defend phase playable
- 1–2 loại enemy đủ để tạo threat thật
- lose state cơ bản khi HQ sập

### H. UI/UX tối thiểu
- HUD
- notifications
- inspect panel tối thiểu
- end-of-session state cơ bản (lose/retry hoặc pseudo-summary)

## 4.3 Không làm trong M1
- nhiều tower types đầy đủ
- full Year 2 content
- full summary polish
- meta layer
- endless mode
- procedural generation phức tạp

## 4.4 Exit criteria cho M1
M1 được coi là xong khi:
- New Run vào game ổn định
- placement không gây mù mờ
- player assign được NPC và thấy settlement đổi hành vi
- resource flow tăng bằng lao động thật
- tower có thể fail vì thiếu ammo, và player đọc được điều đó
- defend phase tạo pressure đủ thật
- một người mới nhìn vào có thể hiểu fantasy cốt lõi trong 20 phút

---

## 5. Milestone M2 — Year 1 Complete

## 5.1 Trải nghiệm mục tiêu
Người chơi có thể chơi trọn Year 1 và cảm nhận được:
- growth
- greed
- bottleneck
- payoff
- Winter như một climax mini-run

## 5.2 Nội dung mở rộng trong M2

### A. Year 1 pacing hoàn chỉnh
- Spring / Summer / Autumn / Winter rõ vai trò
- end-of-season summary sơ bộ
- defend transition rõ ràng

### B. Building/content đủ cho Y1
- Quarry
- Iron source
- Warehouse hoàn chỉnh hơn
- Builder Hut có vai trò rõ hơn
- Forge / Armory ổn định hơn
- thêm ít nhất 1 tower type ngoài Arrow nếu cần cho decision variety

### C. Enemy/wave content đủ cho Year 1
- roster basic đủ readable
- wave escalation hợp lý Autumn → Winter
- 1 boss cho Winter Y1 hoặc một climax equivalent

### D. UX hoàn thiện hơn
- warning priorities đúng
- first-run onboarding khá ổn
- inspect panel nói rõ bottleneck tốt hơn
- end-of-season summary đủ hữu ích

## 5.3 Exit criteria cho M2
M2 được coi là xong khi:
- Year 1 chơi trọn từ start đến Winter climax không gãy loop
- người chơi hiểu rõ vì sao mình mạnh/yếu ở cuối năm
- ít nhất 2–3 bottleneck chiến lược xuất hiện tự nhiên trong run
- seasonal pacing bắt đầu tạo cảm xúc rõ

---

## 6. Milestone M3 — Base Run Complete

## 6.1 Trải nghiệm mục tiêu
Có thể chơi từ Main Menu → New Run → 2 năm → Win/Lose → Summary → Retry / Menu.

## 6.2 Nội dung mở rộng trong M3

### A. Year 2 content
- escalation cho roster hiện có
- elite variants vừa đủ
- late-game pressure tăng chủ yếu bằng interaction của systems, không chỉ tăng số
- boss/climax Winter Y2

### B. Full run UX
- win screen
- lose screen
- end-of-run summary hoàn chỉnh
- retry flow sạch
- save/load đủ đáng tin cho base game

### C. Content complete cho base
- đủ towers có role riêng
- đủ enemy types để tạo decision variety
- đủ wave content cho 2 năm
- run pacing ổn định từ early → late

## 6.3 Exit criteria cho M3
M3 được coi là xong khi:
- 1 run hoàn chỉnh chơi được như sản phẩm thực sự
- có thể thắng/thua công bằng
- summary hữu ích
- save/load không phá settlement state
- Year 2 không chỉ là Year 1 kéo dài thêm

---

## 7. Thứ tự ưu tiên implementation

## 7.1 Ưu tiên cao nhất — Player-facing risks
1. Placement clarity
2. Assignment clarity
3. Resource flow readability
4. Ammo pipeline readability
5. Defend transition clarity
6. Failure clarity

## 7.2 Ưu tiên thứ hai — Structural completeness
1. New Run / Retry flow
2. Season/day loop
3. Summary screens
4. Save/load hygiene

## 7.3 Ưu tiên thứ ba — Content expansion
1. thêm building
2. thêm tower
3. thêm enemy
4. thêm variety / mutators / polish

---

## 8. Workstream gợi ý theo nhóm việc

## 8.1 Gameplay Core
- run clock / season flow
- placement / road / entry
- build / repair / upgrade flow
- combat loop

## 8.2 Economy / Jobs
- assignment
- job generation / cleanup
- harvest / haul / local cap
- ammo chain

## 8.3 UI / UX
- HUD
- build menu
- assignment panel
- inspect panel
- notifications
- summary screens

## 8.4 Content / Balancing
- building defs
- enemy defs
- wave defs
- tuning tables
- onboarding timing

## 8.5 Save / Stability
- save/load
- runtime rebuild rules
- regression tests
- smoke tests per milestone

---

## 9. Suggested Sprint Breakdown (practical)

## Sprint 1 — M1 backbone
- New Run
- start map
- HUD cơ bản
- placement clarity
- assignment panel tối thiểu
- harvest + haul basic tối thiểu

## Sprint 2 — M1 defend proof
- ammo chain tối thiểu
- tower low ammo flow
- defend phase đầu tiên
- enemy pressure cơ bản
- lose flow sơ bộ

## Sprint 3 — M1 polish / validate
- inspect panel tối thiểu hữu dụng
- notification priorities
- first onboarding pass
- vertical slice playtest
- cắt bớt thứ không cần nếu loop còn rối

## Sprint 4 — M2 Year 1 expansion
- Quarry / Iron / better logistics
- seasonal summaries
- Y1 wave escalation
- Winter Y1 climax

## Sprint 5 — M2 readability pass
- bottleneck clarity
- assignment clarity pass 2
- summary usefulness
- pace tuning for Y1

## Sprint 6–7 — M3 full run
- Year 2 escalation
- final bosses/climax
- win/lose/summary complete
- retry loop hoàn chỉnh
- save/load stabilization

---

## 10. Playtest Gates

## Gate A — sau M1
Câu hỏi cần trả lời:
- Người mới có hiểu game “là gì” trong 20 phút không?
- Placement có thú vị hay chỉ bực?
- Assignment có ý nghĩa hay giống việc hành chính?
- Tower thiếu ammo có đọc được không?

## Gate B — sau M2
Câu hỏi cần trả lời:
- Year 1 có tạo đủ progression và payoff không?
- Người chơi có học được từ summary không?
- Winter Y1 có đủ là một climax không?

## Gate C — sau M3
Câu hỏi cần trả lời:
- 1 run hoàn chỉnh có đáng chơi lại không?
- Year 2 có đủ khác Year 1 không?
- Summary và outcome có tạo closure tốt không?

---

## 11. Anti-Scope-Creep Rules

Không thêm feature mới giữa milestone nếu chưa trả lời được:
1. Nó phục vụ fantasy nào?
2. Nó giải quyết pain point nào của player?
3. Nó thay đổi decision space ra sao?
4. Nó có làm onboarding hoặc readability tệ hơn không?
5. Nếu thêm nó, milestone hiện tại có bị chậm không?

Nếu không trả lời rõ, đưa về backlog sau.

---

## 12. Definition of Success

Roadmap được coi là đi đúng khi:
- mỗi milestone đều cho ra build playable
- mỗi milestone đều trả lời một câu hỏi thiết kế lớn
- team không bị drown trong content quá sớm
- GDD/spec vẫn là source of truth, không bị drift
- người chơi mới ngày càng hiểu game nhanh hơn qua mỗi vòng test

---

## 13. Việc nên làm tiếp ngay sau roadmap này

1. Tạo backlog milestone M1 theo task nhỏ.
2. Mapping task vào gameplay / UI / UX / content / save-stability.
3. Chốt definition of done cho từng nhóm task.
4. Chạy vertical slice theo thứ tự ưu tiên player-facing risk trước.

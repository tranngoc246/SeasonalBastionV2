# SEASONAL BASTION — BACKLOG M3: BASE RUN COMPLETE v1.0 (VN)

> Mục đích: Tách milestone M3 trong roadmap thành backlog task-by-task để hoàn thiện base game từ một vertical slice/Year 1 proof thành một run hoàn chỉnh có thể ship nội bộ.
> Mục tiêu M3: Có thể chơi từ Main Menu → New Run → 2 năm → Win/Lose → Summary → Retry / Back to Menu như một sản phẩm hoàn chỉnh.
> Tài liệu nền:
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_BACKLOG_M2_YEAR1_COMPLETE_v1.0_VN.md`

---

## 1. Cách đọc backlog này

### Priority labels
- **P0**: chặn milestone, thiếu là chưa thể gọi là base run complete.
- **P1**: rất quan trọng để run hoàn chỉnh có chất lượng sản phẩm.
- **P2**: polish, nice-to-have, hoặc tăng độ chắc tay nếu còn thời gian.

### Status labels (gợi ý)
- TODO
- IN PROGRESS
- BLOCKED
- DONE
- CUT FOR M3

### Definition of Done chung cho task M3
Một task chỉ tính DONE nếu:
- hoạt động được trong full-run flow
- có player-facing behavior rõ ràng
- không phá regression lớn của M1/M2
- có thể verify bằng smoke test / run-through / playtest / regression phù hợp

---

## 2. Exit criteria của M3

M3 xong khi:
- có thể chơi trọn một run từ Main Menu đến Win/Lose/Retry/Menu
- Year 2 không chỉ là Year 1 kéo dài thêm, mà có escalation thật
- Save/load đủ đáng tin để không phá settlement state
- Summary cuối run đủ tốt để tạo closure và replay motivation
- Người chơi có thể thắng/thua công bằng và hiểu tương đối vì sao

---

## 3. Backlog M3 theo cụm

# A. FULL RUN FLOW / PRODUCT LOOP

## M3-A1 — Main Menu hoàn chỉnh cho base run
**Priority:** P0  
**Mục tiêu:** Flow từ menu phải đủ như sản phẩm thật.

### Kết quả mong muốn
- New Run
- Continue (nếu dùng save)
- Settings cơ bản
- Quit

### Verify
- vào/ra run nhiều lần không lỗi flow hoặc state lẫn nhau

---

## M3-A2 — Retry / Return to Menu flow sạch
**Priority:** P0  
**Mục tiêu:** Sau win/lose, player phải quay vòng lại được mà không rác state.

### Kết quả mong muốn
- Retry tạo run mới sạch
- Back to Menu quay lại ổn
- không carry state runtime cũ ngoài ý muốn

### Verify
- lose → retry nhiều lần liên tiếp
- win → menu → new run vẫn sạch

---

## M3-A3 — Full run progression từ Year 1 sang Year 2
**Priority:** P0  
**Mục tiêu:** Chuyển năm phải rõ và có trọng lượng.

### Kết quả mong muốn
- Year 2 bắt đầu đúng
- state run chuyển tiếp sạch
- player cảm nhận được đang bước sang nửa sau của run

### Verify
- end of Y1 → start of Y2 không đứt loop, không reset sai state

---

# B. YEAR 2 ESCALATION

## M3-B1 — Chốt pacing Year 2
**Priority:** P0  
**Mục tiêu:** Year 2 phải có nhịp tăng áp lực đúng, không đuối hoặc loãng.

### Kết quả mong muốn
- lịch Year 2 hợp lý
- thời lượng không kéo quá dài so với payoff
- escalation rõ nhưng không quá đột ngột

### Verify
- full-run playthrough notes cho thấy Year 2 vẫn giữ được tension

---

## M3-B2 — Elite variants hoặc equivalent escalation
**Priority:** P1  
**Mục tiêu:** Year 2 cần đe dọa khác chất chứ không chỉ khác số.

### Kết quả mong muốn
- có ít nhất một lớp escalation mới: elite variant, harder composition, hoặc pressure pattern khác
- player phải điều chỉnh defense / logistics tương ứng

### Verify
- player cảm thấy Year 2 yêu cầu adaptation chứ không chỉ grind lâu hơn

---

## M3-B3 — Year 2 late-game pressure do systems interaction
**Priority:** P0  
**Mục tiêu:** Áp lực Year 2 đến từ interaction của systems, không chỉ từ tăng damage/số lượng thô.

### Kết quả mong muốn
- ammo logistics, repair, staffing, expansion greed và threat bắt đầu cắn nhau mạnh hơn
- bottleneck xuất hiện đa tầng hơn nhưng vẫn đọc được

### Verify
- một run fail ở Y2 phải cho thấy ít nhất 2 subsystem thật sự xung đột

---

## M3-B4 — Winter Y2 climax / final boss
**Priority:** P0  
**Mục tiêu:** Run phải kết thúc bằng một bài kiểm tra đáng nhớ.

### Kết quả mong muốn
- có boss/climax wave rõ ràng cho Winter Y2
- kiểm tra được mastery của base game loop
- chiến thắng tạo cảm giác kết thúc hành trình, không chỉ “hết content”

### Verify
- thắng trận cuối cho cảm giác closure rõ

---

# C. BASE CONTENT COMPLETE

## M3-C1 — Hoàn tất roster tower của base game
**Priority:** P0  
**Mục tiêu:** Base game phải có đủ tower roles đã chốt.

### Kết quả mong muốn
- Arrow
- Cannon
- Frost
- Fire
- Sniper
- mỗi tower có role chiến lược đủ khác nhau

### Verify
- player có decision thật giữa các tower thay vì chỉ chọn tower số to hơn

---

## M3-C2 — Hoàn tất roster enemy của base game
**Priority:** P0  
**Mục tiêu:** Roster enemy đủ tạo variety cho 2 năm.

### Kết quả mong muốn
- light pressure
- bruiser/tank pressure
- ranged / disruption pressure phù hợp
- elite/later variation đủ cho Year 2

### Verify
- wave compositions đa dạng vừa đủ, không trùng cảm giác quá nhiều

---

## M3-C3 — Wave content đủ cho full run
**Priority:** P0  
**Mục tiêu:** Có wave progression đầy đủ cho 2 năm.

### Kết quả mong muốn
- authored hoặc data-driven wave sets đủ cho Autumn/Winter Y1/Y2
- không có lỗ hổng ngày defend bị rỗng hoặc curve nhảy kỳ quặc

### Verify
- validator / smoke run xác nhận có đủ defend content cho cả run

---

## M3-C4 — Resource/building progression hoàn chỉnh cho base
**Priority:** P1  
**Mục tiêu:** Buildings và resources của base game tạo thành progression đầy đủ, không đứt tầng.

### Kết quả mong muốn
- Wood / Stone / Food / Iron / Ammo đều có vai trò xuyên suốt run
- buildings chủ chốt đều có milestone value rõ

### Verify
- full run không có building/resource nào trở thành “thừa nhưng phải tồn tại” quá rõ ràng

---

# D. FULL RUN UX / UI COMPLETION

## M3-D1 — Win screen hoàn chỉnh
**Priority:** P0  
**Mục tiêu:** Chiến thắng phải có closure rõ.

### Kết quả mong muốn
- thông điệp chiến thắng rõ
- chuyển sang summary hợp lý
- actions tiếp theo rõ ràng

---

## M3-D2 — Lose screen hoàn chỉnh
**Priority:** P0  
**Mục tiêu:** Thua phải rõ, không cụt.

### Kết quả mong muốn
- lý do thua đủ rõ ở mức tối thiểu
- player có Retry / Back to Menu
- flow không làm người chơi bối rối

---

## M3-D3 — End-of-run summary hoàn chỉnh
**Priority:** P0  
**Mục tiêu:** Summary cuối run phải thực sự hữu ích và có cảm xúc.

### Nội dung tối thiểu
- days survived
- peak population
- total resources gathered
- ammo crafted / delivered
- towers built
- buildings lost
- bosses defeated

### Verify
- player có thể nhìn summary và kể lại run của mình ở mức khái quát

---

## M3-D4 — Summary readability / pacing pass
**Priority:** P1  
**Mục tiêu:** Summary không quá dài, không quá mơ hồ.

### Kết quả mong muốn
- đọc nhanh
- thông tin có thứ tự ưu tiên tốt
- đủ để học nhưng không thành data dump

---

## M3-D5 — Full HUD / panel consistency pass
**Priority:** P1  
**Mục tiêu:** Sau khi content complete, UI vẫn đọc được.

### Kết quả mong muốn
- HUD không quá tải
- inspect panel không biến thành bảng thông số vô hồn
- notifications vẫn có thứ tự ưu tiên đúng
- tower/building/NPC states vẫn đọc ra được trong late game

---

# E. SAVE / LOAD / RUNTIME HYGIENE

## M3-E1 — Save/Load đủ đáng tin cho base run
**Priority:** P0  
**Mục tiêu:** Người chơi có thể save/load mà không phá run.

### Kết quả mong muốn
- load lại được state chính xác ở mức gameplay cần thiết
- không tạo duplicate orders/jobs/bad runtime refs dễ thấy

### Verify
- save/load ở nhiều phase khác nhau trong run

---

## M3-E2 — Runtime rebuild rules hoàn chỉnh hơn
**Priority:** P0  
**Mục tiêu:** Những state derive từ runtime phải được rebuild/sanitize đúng sau load.

### Kết quả mong muốn
- stale job refs, transient claim refs, caches, tracked runtime states được xử lý sạch
- combat/build/runstart/save systems không phục hồi nửa vời

### Verify
- regression / smoke tests cho các case save/load then chốt

---

## M3-E3 — Retry/reset hygiene
**Priority:** P0  
**Mục tiêu:** Retry không được tương đương với “load state rác”.

### Kết quả mong muốn
- retry = reset sạch từ product perspective
- không carry config/runtime state ngoài ý muốn

### Verify
- multiple retry loops không làm state drift

---

# F. BALANCING / READABILITY / FAIRNESS

## M3-F1 — Full-run difficulty curve pass
**Priority:** P0  
**Mục tiêu:** 2-year run phải khó dần nhưng công bằng.

### Kết quả mong muốn
- early game đọc được
- mid game tạo trade-off thật
- late game tạo mastery pressure
- final climax khó nhưng không vô lý

### Verify
- internal runs ghi lại chết ở đâu, vì sao, có học được không

---

## M3-F2 — Fair failure pass
**Priority:** P0  
**Mục tiêu:** Player thua nhưng hiểu tương đối vì sao.

### Kết quả mong muốn
- major failure sources đọc được: ammo, staffing, repair, layout, greed, storage/logistics
- game không tạo cảm giác “thua vì hệ thống bí mật”

---

## M3-F3 — Replay motivation pass
**Priority:** P1  
**Mục tiêu:** Sau một run, player muốn thử run khác.

### Kết quả mong muốn
- summary, outcome và memory of bottlenecks đủ mạnh để tạo “lần sau mình sẽ…”
- run không kết thúc theo kiểu mệt mỏi, vô hồn

---

# G. POLISH / PRODUCTIZATION

## M3-G1 — Settings tối thiểu usable
**Priority:** P1  
**Mục tiêu:** Product loop cơ bản phải có settings khả dụng.

### Kết quả mong muốn
- audio / basic options đủ dùng
- không cần quá nhiều option, nhưng không nên hoàn toàn trống

---

## M3-G2 — Menu / flow polish cơ bản
**Priority:** P2  
**Mục tiêu:** Product cảm giác nhất quán hơn, đỡ prototype.

### Kết quả mong muốn
- transitions đủ mượt
- text/button wording nhất quán
- không có chỗ “dead end” rõ rệt

---

## M3-G3 — Presentation pass cho climax / season transitions
**Priority:** P2  
**Mục tiêu:** Tăng cảm giác premium mà không phá scope.

### Kết quả mong muốn
- season transitions có nhịp tốt hơn
- boss/climax presentation đủ memorable

---

# H. PLAYTEST / VALIDATION / RELEASE READINESS

## M3-H1 — Full-run smoke checklist
**Priority:** P0  
**Mục tiêu:** Có checklist test nhanh toàn bộ hành trình sản phẩm.

### Smoke cases bắt buộc
- menu → new run
- full Year 1
- transition sang Year 2
- Year 2 defend content
- win flow
- lose flow
- retry / back to menu
- save/load giữa run

---

## M3-H2 — Guided full-run playtest questions
**Priority:** P1  
**Mục tiêu:** Đo chất lượng trải nghiệm như một sản phẩm thật.

### Câu hỏi nên dùng
- Bạn có hiểu mình thua/thắng vì đâu không?
- Year 2 có đủ khác Year 1 không?
- Summary cuối run có giúp bạn hiểu run của mình không?
- Bạn có muốn chơi lại không? Vì sao?
- Hệ nào bạn thấy hay nhất? Hệ nào mệt nhất?

---

## M3-H3 — Cut list cho base ship
**Priority:** P0  
**Mục tiêu:** Chốt rõ cái gì không cố nhét vào base nữa.

### Cắt trước nếu quá tải
- extra polish presentation sâu
- tower/enemy variants phụ không đổi decision space nhiều
- advanced meta hooks
- optional map variety lớn
- advanced settings beyond practical minimum

### Không được cắt cho M3
- win/lose/retry/menu loop
- Year 2 escalation thật
- end-of-run summary usable
- save/load đáng tin ở mức base
- content đủ cho 2-year run

---

## 4. Thứ tự đề xuất để làm M3

### Wave 1 — Product loop complete
1. M3-A1 Main Menu hoàn chỉnh cho base run
2. M3-A2 Retry / Return to Menu flow sạch
3. M3-A3 Full run progression từ Year 1 sang Year 2
4. M3-D1 Win screen hoàn chỉnh
5. M3-D2 Lose screen hoàn chỉnh

### Wave 2 — Year 2 content & escalation
6. M3-B1 Chốt pacing Year 2
7. M3-B2 Elite variants hoặc equivalent escalation
8. M3-B3 Year 2 late-game pressure do systems interaction
9. M3-B4 Winter Y2 climax / final boss

### Wave 3 — Base content complete
10. M3-C1 Hoàn tất roster tower của base game
11. M3-C2 Hoàn tất roster enemy của base game
12. M3-C3 Wave content đủ cho full run
13. M3-C4 Resource/building progression hoàn chỉnh cho base

### Wave 4 — Save/load & fairness
14. M3-E1 Save/Load đủ đáng tin cho base run
15. M3-E2 Runtime rebuild rules hoàn chỉnh hơn
16. M3-E3 Retry/reset hygiene
17. M3-F1 Full-run difficulty curve pass
18. M3-F2 Fair failure pass

### Wave 5 — Summary / productization / validation
19. M3-D3 End-of-run summary hoàn chỉnh
20. M3-D4 Summary readability / pacing pass
21. M3-D5 Full HUD / panel consistency pass
22. M3-F3 Replay motivation pass
23. M3-H1 Full-run smoke checklist
24. M3-H2 Guided full-run playtest questions
25. M3-H3 Cut list cho base ship

---

## 5. M3 done means what?

M3 chỉ tính DONE khi build full run trả lời được 3 câu:
1. Đây đã là một sản phẩm hoàn chỉnh từ đầu tới cuối chưa?
2. Người chơi có thể thắng/thua công bằng và hiểu tương đối vì sao chưa?
3. Sau khi kết thúc một run, người chơi có đủ closure và động lực để chơi lại chưa?

Nếu chưa trả lời được 3 câu đó, M3 chưa xong dù content đã “đủ số lượng”.

# SEASONAL BASTION — BACKLOG M2: YEAR 1 COMPLETE v1.0 (VN)

> Mục đích: Tách milestone M2 trong roadmap thành backlog task-by-task đủ chi tiết để triển khai sau khi M1 đã chứng minh vertical slice playable.
> Mục tiêu M2: Chứng minh Year 1 có progression, seasonal pacing, bottleneck và payoff đủ rõ.
> Tài liệu nền:
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`

---

## 1. Cách đọc backlog này

### Priority labels
- **P0**: chặn milestone, phải có để M2 đạt nghĩa “Year 1 complete”.
- **P1**: rất nên có để Year 1 đủ payoff và readability.
- **P2**: polish hoặc tăng độ chắc tay, làm sau cùng nếu còn thời gian.

### Status labels (gợi ý)
- TODO
- IN PROGRESS
- BLOCKED
- DONE
- CUT FOR M2

### Definition of Done chung cho task M2
Một task chỉ tính DONE nếu:
- hoạt động trong build playable của Year 1
- player-facing behavior đọc được, không chỉ đúng logic nội bộ
- không làm M1 regress nặng
- có cách verify bằng playtest / smoke / regression / manual walkthrough

---

## 2. Exit criteria của M2

M2 xong khi:
- Year 1 chơi trọn từ start đến Winter climax không gãy loop
- Người chơi hiểu vì sao mình mạnh/yếu ở cuối năm
- Có ít nhất 2–3 bottleneck chiến lược xuất hiện tự nhiên trong run
- Seasonal pacing tạo cảm xúc rõ: grow → prepare → pressure → climax
- Summary cuối mùa và cuối Year 1 đủ giúp người chơi học và muốn thử lại

---

## 3. Backlog M2 theo cụm

# A. YEAR 1 PACING / SEASON FLOW COMPLETE

## M2-A1 — Chốt lịch Year 1 playable
**Priority:** P0  
**Mục tiêu:** Year 1 phải có timeline ổn định và đủ nhịp.

### Kết quả mong muốn
- số ngày Spring / Summer / Autumn / Winter hợp lý cho Year 1
- phase transition đúng và testable
- có đủ thời gian để build-up trước Autumn/Winter nhưng không quá dài gây loãng

### Verify
- chạy ít nhất 1–2 full Year 1 simulation với time scale nhanh
- không có mùa nào dài/vô nghĩa rõ rệt

---

## M2-A2 — Seasonal identity rõ hơn
**Priority:** P1  
**Mục tiêu:** Mỗi mùa Year 1 phải có vai trò cảm xúc và gameplay khác nhau.

### Kết quả mong muốn
- Spring = ổn định và hồi nhịp
- Summer = greed / optimize / prepare
- Autumn = pressure bắt đầu
- Winter = climax / survival test

### Verify
- playtesters có thể mô tả sự khác nhau giữa các mùa mà không cần đọc tài liệu

---

## M2-A3 — Defend transition rõ và có trọng lượng
**Priority:** P0  
**Mục tiêu:** Khi bước vào Autumn/Winter, game phải tạo cảm giác “phase change” thật.

### Kết quả mong muốn
- transition UI/HUD/notification rõ
- speed behavior đúng rule
- player thấy áp lực tăng ngay, không mơ hồ

### Verify
- observe first defend transition trong Year 1 và ghi nhận player reaction

---

## M2-A4 — End-of-season cadence có ích
**Priority:** P1  
**Mục tiêu:** Summary giữa các mùa phải trở thành nhịp học, không chỉ là màn hình trang trí.

### Kết quả mong muốn
- hiển thị được tiến độ / thiếu hụt / threat outlook
- đủ ngắn để không làm đứt nhịp
- đủ hữu ích để người chơi điều chỉnh kế hoạch

### Verify
- player có thể nói sau summary rằng mình sẽ thay đổi gì ở mùa sau

---

# B. BUILDING / INFRASTRUCTURE EXPANSION FOR YEAR 1

## M2-B1 — Quarry được đưa vào loop Year 1
**Priority:** P0  
**Mục tiêu:** Stone phải trở thành một resource chiến lược có giá trị thật trong Year 1.

### Kết quả mong muốn
- có building/resource flow cho stone
- stone được dùng cho một số cấu phần expansion/defense meaningful

### Verify
- player có tình huống thiếu stone thật, không chỉ là resource dư thừa tượng trưng

---

## M2-B2 — Iron source được đưa vào loop Year 1
**Priority:** P0  
**Mục tiêu:** Iron phải là cầu nối hợp lý sang ammo economy mạnh hơn.

### Kết quả mong muốn
- iron production readable
- iron trở thành bottleneck hợp lý cho ammo/defense progression

### Verify
- player hiểu được vì sao thiếu iron làm pressure ở mid/late Y1 tăng

---

## M2-B3 — Warehouse có vai trò rõ hơn trong Year 1
**Priority:** P0  
**Mục tiêu:** Warehouse không chỉ tồn tại trên giấy; nó phải giải được bottleneck thật.

### Kết quả mong muốn
- Warehouse cải thiện storage / throughput / haul smoothing rõ ràng
- player cảm nhận được khác biệt khi có hoặc không có Warehouse

### Verify
- compare một run có Warehouse sớm và một run không có Warehouse sớm

---

## M2-B4 — Builder Hut có vai trò rõ hơn
**Priority:** P0  
**Mục tiêu:** Builder Hut phải trở thành cấu phần chiến lược thật, không bị HQ worker nuốt mất ý nghĩa.

### Kết quả mong muốn
- build / repair throughput hoặc reliability tăng khi có Builder Hut
- HQ worker chỉ còn là fallback early game, không phải lời giải mãi mãi

### Verify
- player thấy Builder Hut là milestone chứ không phải công trình “có cũng được” 

---

## M2-B5 — Forge / Armory ổn định hơn ở scale Year 1
**Priority:** P0  
**Mục tiêu:** Ammo pipeline phải chạy ở mức pressure đủ thật cho hết Year 1.

### Kết quả mong muốn
- Forge/Armory không chỉ chạy trong slice nhỏ, mà đứng được trong progression dài hơn
- ammo bottleneck xuất hiện đúng lúc, không quá sớm cũng không quá muộn

### Verify
- survive Year 1 depend thực sự vào việc pipeline này có được đầu tư đúng hay không

---

## M2-B6 — Thêm ít nhất 1 tower type ngoài Arrow (nếu cần)
**Priority:** P1  
**Mục tiêu:** Tăng decision space cho defense trong Year 1.

### Kết quả mong muốn
- tower mới có role chiến lược rõ
- không chỉ là damage number khác màu

### Verify
- player có lý do thật để cân nhắc tower A vs B

---

# C. YEAR 1 ECONOMY / LOGISTICS DEPTH

## M2-C1 — Resource triangle đầu game → mid game rõ hơn
**Priority:** P0  
**Mục tiêu:** Wood / Stone / Food / Iron phải tạo thành bài toán Year 1 thực sự.

### Kết quả mong muốn
- không có resource nào hoàn toàn thừa hoặc vô hình trong Year 1
- expansion buộc player trade off giữa growth, logistics và defense

### Verify
- Year 1 có ít nhất 2 decision moments quanh việc ưu tiên resource nào trước

---

## M2-C2 — Local cap / storage pressure readable hơn
**Priority:** P1  
**Mục tiêu:** Storage pressure phải hiện ra như bottleneck chiến lược rõ, không như bug mơ hồ.

### Kết quả mong muốn
- local cap near full được đọc ra được
- Warehouse hoặc haul capacity thực sự giải được pressure đó

### Verify
- player có thể nói “mình cần thêm storage/haul” thay vì “tự nhiên game chậm lại”

---

## M2-C3 — Build / repair / supply cạnh tranh tài nguyên rõ hơn
**Priority:** P0  
**Mục tiêu:** Year 1 phải cho thấy cùng một pool tài nguyên bị kéo bởi nhiều nhu cầu.

### Kết quả mong muốn
- build greed có thể làm ammo hoặc repair bị thiếu
- repair / defense pressure có thể trì hoãn expansion

### Verify
- cuối Year 1 player cảm nhận được trade-off thật, không chỉ linear build order

---

# D. YEAR 1 DEFENSE / ENEMY / WAVE CONTENT

## M2-D1 — Roster basic đủ readable cho Year 1
**Priority:** P0  
**Mục tiêu:** Enemy roster Year 1 phải đa dạng vừa đủ để defense không đơn điệu.

### Kết quả mong muốn
- basic pressure unit
- tankier / bruiser pressure
- ranged hoặc nuisance pressure nếu phù hợp
- mỗi loại tạo ra một nhu cầu defense khác nhau ở mức vừa phải

### Verify
- playtest cho thấy player có thể nói “wave này khác wave trước ở chỗ nào”

---

## M2-D2 — Autumn → Winter escalation hợp lý
**Priority:** P0  
**Mục tiêu:** Threat curve Year 1 phải leo dần, không giật cục và không bằng phẳng.

### Kết quả mong muốn
- Autumn là bài test đầu tiên
- Winter là climax thực sự mạnh hơn Autumn

### Verify
- pressure curve nhìn lại trong playtest graph / notes hợp lý, không “đầu độc” quá sớm

---

## M2-D3 — Winter Y1 climax / boss equivalent
**Priority:** P0  
**Mục tiêu:** Year 1 phải kết thúc bằng một bài kiểm tra đáng nhớ.

### Kết quả mong muốn
- có boss hoặc climax wave rõ ràng
- kiểm tra được ammo, layout, repair, throughput

### Verify
- player nhớ Winter Y1 như một cột mốc, không phải chỉ là thêm một wave dài

---

## M2-D4 — Tower role readability ở Year 1
**Priority:** P1  
**Mục tiêu:** Nếu có >1 tower type trong M2, role của chúng phải đọc được.

### Kết quả mong muốn
- mỗi tower có role đủ rõ trong defense plan
- player không bị “nhiều tower nhưng giống nhau”

---

# E. UX / ONBOARDING PASS 2

## M2-E1 — First-run onboarding pass hoàn chỉnh hơn
**Priority:** P0  
**Mục tiêu:** Year 1 phải đủ “dẫn” để người mới không rơi rụng quá sớm.

### Kết quả mong muốn
- assignment intro tốt hơn
- placement intro rõ hơn
- defend warning đúng lúc
- ammo/logistics intro không đến quá sớm hoặc quá muộn

### Verify
- user mới qua được early game với ít bối rối hơn M1

---

## M2-E2 — Warning priority tuning
**Priority:** P1  
**Mục tiêu:** Notification phải phục vụ hành động, không spam.

### Kết quả mong muốn
- warning quan trọng lên đúng lúc
- low-value spam bị hạ bớt
- critical warnings thật sự nổi bật

### Verify
- log hoặc observation cho thấy player không bỏ lỡ warnings quan trọng

---

## M2-E3 — Failure clarity pass cho Year 1
**Priority:** P0  
**Mục tiêu:** Khi thua hoặc hụt hơi ở Year 1, player phải hiểu nguyên nhân chính.

### Kết quả mong muốn
- thiếu ammo nhìn ra được
- thiếu worker nhìn ra được
- thiếu storage/haul nhìn ra được
- layout yếu hoặc greed quá mức nhìn ra được phần nào qua state/summaries

### Verify
- sau fail, player trả lời được “vì sao mình thua” mà không đoán mò

---

## M2-E4 — Inspect panel bottleneck readability pass
**Priority:** P1  
**Mục tiêu:** Inspect panel phải thực sự là nơi đọc sự thật của system.

### Kết quả mong muốn
- building/tower/site/NPC states đọc được hơn M1
- ammo, worker, blocked state, storage state rõ hơn

---

# F. SUMMARY / FEEDBACK / LEARNING LOOP

## M2-F1 — End-of-season summary usable
**Priority:** P0  
**Mục tiêu:** Summary giữa mùa phải giúp player học và chỉnh chiến lược.

### Nội dung tối thiểu
- resources gained/spent
- buildings built/upgraded
- damage / repair
- population trend
- next season threat hint

### Verify
- player dùng summary để ra ít nhất 1 quyết định mùa sau

---

## M2-F2 — End-of-Year-1 recap hoặc equivalent
**Priority:** P1  
**Mục tiêu:** Năm đầu phải có cảm giác khép lại một chương.

### Kết quả mong muốn
- sau Winter Y1 có recap / milestone feel rõ
- player thấy mình đã “qua được năm đầu” chứ không chỉ sang season mới một cách vô hồn

---

## M2-F3 — Reward cadence tuning
**Priority:** P1  
**Mục tiêu:** Người chơi phải thường xuyên thấy payoff nhỏ và payoff vừa.

### Kết quả mong muốn
- công trình mới tạo khác biệt rõ
- Warehouse/Builder Hut/Forge/Armory là milestone có cảm giác “đáng xây”
- survive Autumn/Winter cho cảm giác thành tựu thật

---

# G. PLAYTEST / BALANCING / VALIDATION

## M2-G1 — Year 1 smoke checklist
**Priority:** P0  
**Mục tiêu:** Có checklist chạy nhanh cho toàn bộ Year 1.

### Smoke cases bắt buộc
- start package đúng
- assignment flow vẫn ổn
- placement vẫn rõ
- 4 resource loops vẫn vận hành
- Warehouse/Builder Hut tạo khác biệt thật
- ammo pipeline không vỡ vô lý
- Autumn/Winter escalation có áp lực
- climax Y1 chạy được

---

## M2-G2 — Guided Year 1 playtest script
**Priority:** P1  
**Mục tiêu:** Có script test để đo hiểu biết và cảm nhận người chơi.

### Câu hỏi nên dùng
- Bạn có biết mình nên xây gì trong Summer không?
- Bạn có hiểu vì sao mình thiếu đạn / thiếu defense không?
- Bạn có thấy Builder Hut / Warehouse đáng giá không?
- Autumn và Winter có khác nhau rõ không?
- Sau Winter Y1, bạn có muốn chơi tiếp Year 2 không?

---

## M2-G3 — Cut list nếu M2 phình scope
**Priority:** P0  
**Mục tiêu:** Biết cái gì cắt trước để bảo vệ cốt lõi Year 1.

### Cắt trước nếu quá tải
- tower variety phụ ngoài 1 tower bổ sung có vai trò rõ
- extra enemy flavor ngoài roster đủ dùng
- visual polish sâu
- recap presentation quá cầu kỳ
- onboarding cinematic/overproduction

### Không được cắt cho M2
- Year 1 season pacing rõ
- Quarry / Iron / Warehouse / Builder Hut / Forge / Armory đủ ý nghĩa
- Autumn/Winter escalation
- Winter Y1 climax
- failure clarity và summary usability cơ bản

---

## 4. Thứ tự đề xuất để làm M2

### Wave 1 — Year 1 structure
1. M2-A1 Chốt lịch Year 1 playable
2. M2-A3 Defend transition rõ và có trọng lượng
3. M2-A4 End-of-season cadence có ích
4. M2-E3 Failure clarity pass cho Year 1

### Wave 2 — Infrastructure milestones
5. M2-B1 Quarry được đưa vào loop Year 1
6. M2-B2 Iron source được đưa vào loop Year 1
7. M2-B3 Warehouse có vai trò rõ hơn trong Year 1
8. M2-B4 Builder Hut có vai trò rõ hơn
9. M2-B5 Forge / Armory ổn định hơn ở scale Year 1

### Wave 3 — Combat content Year 1
10. M2-D1 Roster basic đủ readable cho Year 1
11. M2-D2 Autumn → Winter escalation hợp lý
12. M2-D3 Winter Y1 climax / boss equivalent
13. M2-D4 Tower role readability ở Year 1

### Wave 4 — UX / summary / learning loop
14. M2-E1 First-run onboarding pass hoàn chỉnh hơn
15. M2-E2 Warning priority tuning
16. M2-E4 Inspect panel bottleneck readability pass
17. M2-F1 End-of-season summary usable
18. M2-F2 End-of-Year-1 recap hoặc equivalent
19. M2-F3 Reward cadence tuning

### Wave 5 — Validation
20. M2-G1 Year 1 smoke checklist
21. M2-G2 Guided Year 1 playtest script
22. M2-G3 Cut list nếu M2 phình scope

---

## 5. M2 done means what?

M2 chỉ tính DONE khi build Year 1 trả lời được 3 câu:
1. Year 1 có đủ là một hành trình mini hoàn chỉnh chưa?
2. Người chơi có thấy bottleneck và payoff đủ rõ chưa?
3. Sau Winter Y1, người chơi có thật sự muốn bước vào Year 2 không?

Nếu chưa trả lời được 3 câu đó, M2 chưa xong dù content đã nhiều hơn M1.

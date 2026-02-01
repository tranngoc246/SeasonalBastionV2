# Seasonal Bastion — M0–M2 Setup (UI-first, no Debug)

## M0: New Input System only (REQUIRED)
- Project Settings → Player → **Active Input Handling** = **Input System Package (New)**
- Không dùng `StandaloneInputModule` (legacy). Pack nhớ tự xoá legacy module nếu có.

## Copy files into project
Copy folder `Assets/_Game/...` từ pack vào Unity project của bạn (merge folders).

### Files you will overwrite (existing):
- `Assets/_Game/Core/Boot/GameBootstrap.cs`
- `Assets/_Game/UI/Runtime/Scripts/UiRoot.cs`
- `Assets/_Game/Rewards/RunOutcomeService.cs`

### New files:
- `Assets/_Game/Core/Input/InputSystemOnlyGuard.cs`
- `Assets/_Game/Core/App/AppSettings.cs` (lưu PlayerPrefs, **chưa cần AudioService**)
- `Assets/_Game/Core/Boot/GameAppController.cs`
- `Assets/_Game/UI/Runtime/Scripts/MainMenu/MainMenuScreen.cs`
- `Assets/_Game/UI/Runtime/Scripts/Modals/ModalsPresenter.cs`
- `Assets/_Game/UI/Runtime/UXML/MainMenu.uxml`
- `Assets/_Game/UI/Runtime/USS/MainMenu.uss`
- `Assets/_Game/UI/Runtime/UXML/ModalsRoot.uxml`
- `Assets/_Game/UI/Runtime/USS/Modals.uss`
- `Assets/_Game/UI/Runtime/UXML/HUD.uxml` (đã thêm BtnMenu “≡”)

> Lưu ý quan trọng:
> - `IResettable` trong project của bạn là **internal** (theo asmdef), nên `RunOutcomeService` **không implement IResettable** nữa để tránh lỗi compile.
> - New Game/Restart trong M0–M2 reset state bằng **reload scene** (đúng ship UX).

## Create Scene: MainMenu (M1)
1) File → New Scene, save as `Assets/_Game/Scenes/MainMenu.unity`
2) Create empty GameObject `GameAppController` → add component `GameAppController`
3) Create GameObject `MainMenuUI`:
   - Add `UIDocument`
   - Add `MainMenuScreen`
   - Set UIDocument.VisualTreeAsset = `Assets/_Game/UI/Runtime/UXML/MainMenu.uxml`
   - Set PanelSettings = panel settings bạn đang dùng (DefaultPanelSettings)

## Update Scene: Game (M2)
Trong scene Game (scene có `GameBootstrap` + `UiRoot`):

1) Ensure `GameBootstrap` exists (auto start OFF theo code, nhưng inspector có thể override).
2) Add Modals UIDocument:
   - Create GameObject `ModalsDocument`
   - Add `UIDocument`
   - Set VisualTreeAsset = `Assets/_Game/UI/Runtime/UXML/ModalsRoot.uxml`
   - Set PanelSettings giống HUD
3) On `UiRoot` component:
   - Assign `_modalsDocument` = `ModalsDocument`

## Build Settings
- Add scenes: `MainMenu` (index 0), `Game` (index 1)

## Smoke test checklist
- Run → vào MainMenu
- New Game → vào gameplay
- Nhấn **ESC** hoặc nút **≡** → Pause modal hiện
- Save → tạo `run_save.json` ở `Application.persistentDataPath`
- Back to Menu → Continue enabled → Continue load OK
- Defeat HQ (hoặc trigger victory) → RunEnd modal hiện

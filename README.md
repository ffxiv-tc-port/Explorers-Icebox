# Explorer's Icebox

[Explorer's Icebox](https://github.com/LeontopodiumNivale14/Explorers-Icebox) 的台服移植版。搭配 vnavmesh 使用的無人島採集自動化插件，協助提升開拓等級與採集無人島素材。採集一律由你在主視窗按下開始，載入或進島不會自己開始。

## 功能

### 模式

- **地面經驗值**：跑內建的黏土／砂礫路線刷開拓經驗
- **飛行經驗值**：跑內建的石英（高山）路線刷開拓經驗
- **素材採集**：從內建的多條路線中挑一條，反覆採集該路線的素材

### 循環與出售

- 表格列出所選路線每輪會採到的素材、目前持有數量，可逐項設定要採集的數量，自動算出需要的循環次數與最多可跑幾輪
- 可勾選「執行最多循環次數」直接跑到上限，或「重複循環」指定次數
- 預計會超過 999 個上限的素材，會先到產品交易賣掉多出的部分再繼續採集；可設定「保留此數量的物品」，賣出時不會低於這個數量，也可勾選「略過出售物品」

## 指令

- `/icebox` 或 `/explorersicebox`：開關主視窗

## 台服版的修改

- 介面與路線名稱繁體中文化
- 不對正在關閉中或還沒載完的無人島視窗重複按下，降低崩潰風險
- 修正啟動時每幀呼叫停止流程造成的 IPC 例外
- 修正 vnavmesh 尋路 IPC 的簽章
- 提供急停 IPC `ExplorersIcebox.StopAll` 給其他插件呼叫（只能停止；開始一律要你自己按）
- 缺少 vnavmesh 時的安裝提示改指台服的第三方插件倉庫，不會導向國際服

## 安裝

1. 在遊戲內輸入 `/xlsettings` 開啟「Dalamud 設定」，到「試驗性功能」分頁的「第三方插件倉庫」加入
   `https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json`
   勾選啟用後儲存。
2. 輸入 `/xlplugins` 開啟「插件管理員」，搜尋「Explorer's Icebox」安裝。

同一個插件只裝這裡的版本，不要同時安裝上游（國際服）版本。

## 需要搭配的插件

- 必要：[vnavmesh](https://github.com/ffxiv-tc-port/vnavmesh)（移動與尋路）

原作者：[LeontopodiumNivale14](https://github.com/LeontopodiumNivale14)

授權：AGPL-3.0（見 LICENSE.md）

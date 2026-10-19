# Explorer's Icebox

[Explorer's Icebox](https://github.com/LeontopodiumNivale14/Explorers-Icebox) 的台服移植版。搭配 vnavmesh 使用的無人島採集自動化插件，協助提升開拓等級與採集無人島素材。

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
- 插件庫安裝提示改指台服 feed，缺少 vnavmesh 時不會導向國際服

## 需要搭配的插件

- 必要：vnavmesh（移動與尋路）

## 安裝

在 Dalamud 設定的「自訂插件庫」加入
`https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json`
並啟用，再從插件列表安裝。

原作者：[LeontopodiumNivale14](https://github.com/LeontopodiumNivale14)

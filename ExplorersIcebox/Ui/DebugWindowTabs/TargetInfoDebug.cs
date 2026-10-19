using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class TargetInfoDebug
    {
        public static void Draw()
        {
            var target = Svc.Targets?.Target;

            if (target != null)
            {
                if (ImGui.Button($"名稱: {target.Name}"))
                {
                    ImGui.SetClipboardText($"GatherName = \"{target.Name}\",");
                }
                if (ImGui.Button($"物件ID: {target.GameObjectId}"))
                {
                    ImGui.SetClipboardText($"{target.GameObjectId}");
                }
                if (ImGui.Button($"資料ID: {target.DataId}"))
                {
                    ImGui.SetClipboardText($"{target.DataId}");
                }
            }
        }
    }
}

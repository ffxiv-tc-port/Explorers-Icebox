using Dalamud.Game.ClientState.Objects.Types;
using Pictomancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorersIcebox.Ui.DebugWindowTabs
{
    internal class PictoTestDebug
    {
        private static uint ToUintABGR(Vector4 col)
        {
            byte a = (byte)(col.W * 255);
            byte b = (byte)(col.Z * 255);
            byte g = (byte)(col.Y * 255);
            byte r = (byte)(col.X * 255);
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        private static Vector4 FromUintABGR(uint color)
        {
            float a = ((color >> 24) & 0xFF) / 255f;
            float b = ((color >> 16) & 0xFF) / 255f;
            float g = ((color >> 8) & 0xFF) / 255f;
            float r = (color & 0xFF) / 255f;
            return new Vector4(r, g, b, a);
        }

        private static Vector4 ImGuiCircleCol = FromUintABGR(C.PictoCircleColor); // ABGR Red
        private static Vector4 ImGuiDotColor = FromUintABGR(C.PictoWPColor); // ABGR Red
        private static Vector4 ImGuiLineColor = FromUintABGR(C.PictoLineColor); // ABGR Red
        private static Vector4 ImGuiTextColor = FromUintABGR(C.PictoTextCol); // ABGR Red
        private static bool ShowDot = false;
        private static float DotRadius = C.DotRadius;
        private static bool ShowLine = false;
        private static float LineWidth = C.LineWidth;
        private static bool ShowCircle = false;
        private static bool ShowCircleOutline = false;
        private static bool ShowDonut = false;
        private static Vector2 DonutRadius = C.DonutRadius;
        private static Vector2 FanPosition = C.FanPosition;
        private static bool ShowVFX = false;
        private static bool ShowName = false;
        private static float FloatDistance = C.TextFloatPlus;
        private static float FloatTextScale = 0.0f;

        public static void Draw()
        {
            ImGui.Text("選擇顏色:");

            if (ImGui.ColorEdit4("圓形顏色", ref ImGuiCircleCol))
            {
                C.PictoCircleColor = ToUintABGR(ImGuiCircleCol);
                C.Save();
            }
            if (ImGui.ColorEdit4("點顏色", ref ImGuiDotColor))
            {
                C.PictoWPColor = ToUintABGR(ImGuiDotColor);
                C.Save();
            }
            if (ImGui.ColorEdit4("線條顏色", ref ImGuiLineColor))
            {
                C.PictoLineColor = ToUintABGR(ImGuiLineColor);
                C.Save();
            }
            if (ImGui.ColorEdit4("文字顏色", ref ImGuiTextColor))
            {
                C.PictoTextCol = ToUintABGR(ImGuiTextColor);
                C.Save();
            }

            IGameObject? target = Svc.Targets.Target;
            var PlayerPos = Svc.Objects.LocalPlayer?.Position ?? new Vector3(0);

            if (target != null)
            {
                using (var drawList = PictoService.Draw())
                {
                    if (drawList == null)
                        return;
                    // Draw a circle around a GameObject's hitbox
                    Vector3 worldPosition = target.Position;
                    float radius = target.HitboxRadius;

                    ImGui.Checkbox("顯示點", ref ShowDot);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);

                    if (ImGui.DragFloat("點大小", ref DotRadius, 0.2f))
                    {
                        C.DotRadius = DotRadius;
                        C.Save();
                    }
                    ImGui.Checkbox("顯示線條", ref ShowLine);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.DragFloat("線條寬度", ref LineWidth, 0.1f))
                    {
                        C.LineWidth = LineWidth;
                        C.Save();
                    }
                    ImGui.Checkbox("顯示圓形", ref ShowCircle);
                    ImGui.Checkbox("顯示圓形外框", ref ShowCircleOutline);
                    ImGui.Checkbox("顯示扇形/環形", ref ShowDonut);
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.DragFloat2("內徑 | 外徑", ref DonutRadius, 0.1f))
                    {
                        C.DonutRadius = DonutRadius;
                        C.Save();
                    }
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.DragFloat2("起始/結束位置", ref FanPosition, 0.1f))
                    {
                        C.FanPosition = FanPosition;
                        C.Save();
                    }
                    ImGui.Checkbox("顯示 VFX", ref ShowVFX);
                    ImGui.Checkbox("顯示名稱", ref ShowName);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    if (ImGui.DragFloat("名稱浮動距離", ref FloatDistance))
                    {
                        C.TextFloatPlus = FloatDistance;
                        C.Save();
                    }
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(100);
                    ImGui.DragFloat("縮放", ref FloatTextScale);


                    if (ShowDot)
                        drawList.AddDot(worldPosition, DotRadius, C.PictoWPColor);
                    if (ShowLine && (PlayerPos != new Vector3(0)))
                        drawList.AddLine(PlayerPos, worldPosition, LineWidth, C.PictoLineColor);
                    if (ShowCircle)
                        drawList.AddCircle(worldPosition, radius, C.PictoCircleColor);
                    if (ShowCircleOutline)
                        drawList.AddCircleFilled(worldPosition, radius, C.PictoCircleColor);
                    if (ShowDonut)
                        drawList.AddFanFilled(worldPosition, DonutRadius.X, DonutRadius.Y, FanPosition.X, FanPosition.Y, C.PictoCircleColor);
                    if (ShowVFX)
                        PictoService.VfxRenderer.AddFan("TestId", worldPosition, DonutRadius.X, DonutRadius.Y, FanPosition.X, FanPosition.Y, ImGuiCircleCol);
                    if (ShowName)
                    {
                        Vector3 textWorldPosition = new Vector3(worldPosition.X, worldPosition.Y + FloatDistance, worldPosition.Z);
                        drawList.AddText(textWorldPosition, C.PictoTextCol, $"{target.Name.ToString()}", FloatTextScale);
                    }
                }
            }
        }
    }
}

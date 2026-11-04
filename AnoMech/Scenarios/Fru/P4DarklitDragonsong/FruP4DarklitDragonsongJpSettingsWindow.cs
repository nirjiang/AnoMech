using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Fru.P4DarklitDragonsong;

public sealed class FruP4DarklitDragonsongJpSettingsWindow
{
    public FruP4DarklitDragonsongJpStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.EastWing = null;
            Overrides.UsurperNorth = null;
        }
        if (SettingsGrid.Begin("##frup4darklit"))
        {
            SettingsGrid.FightOnlyNote();
            var north = Overrides.UsurperNorth;
            SettingsGrid.Row("Shiva opening:");
            if (ImGui.RadioButton("Random##dlopening", north == null)) Overrides.UsurperNorth = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("North##dlopening", north == true)) Overrides.UsurperNorth = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("South (opposite Fragment)##dlopening", north == false)) Overrides.UsurperNorth = false;
            var v = Overrides.EastWing;
            SettingsGrid.Row("Hallowed Wings:");
            if (ImGui.RadioButton("Auto##dlwing", v == null)) Overrides.EastWing = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("East##dlwing", v == true)) Overrides.EastWing = true;
            ImGui.SameLine();
            if (ImGui.RadioButton("West##dlwing", v == false)) Overrides.EastWing = false;
            SettingsGrid.End();
        }
    }
}

using System;
using UnityEngine;
using Verse;

namespace Mulld.Priorities
{
    public sealed class PresetSaveDialog : Window
    {
        private string name, error;
        private readonly Func<string, bool, string> save;
        private bool focus = true;
        public override Vector2 InitialSize => new Vector2(440, 260);
        public PresetSaveDialog(string initialName, Func<string, bool, string> save)
        {
            name = initialName == "Custom" ? "" : initialName; this.save = save;
            doCloseX = true; absorbInputAroundWindow = true; forcePause = true;
            closeOnAccept = false;
        }
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width, 32), "Save Rules As"); Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 42, rect.width, 24), "Preset name");
            GUI.SetNextControlName("mulldPresetName");
            name = Widgets.TextField(new Rect(0, 69, rect.width, 30), name, 40);
            if (focus) { UI.FocusControl("mulldPresetName", this); focus = false; }
            Widgets.Label(new Rect(0, 108, rect.width, 42), error ?? "Existing names can be overwritten after confirmation. Priorities remain unchanged.");
            float width = (rect.width - 8) / 2;
            if (Widgets.ButtonText(new Rect(0, rect.height - 35, width, 30), "Cancel")) Close();
            if (Widgets.ButtonText(new Rect(width + 8, rect.height - 35, width, 30), "Save Preset")) Submit();
        }
        private void Submit()
        {
            error = NamedPresetStore.ValidateName(name);
            if (error != null) return;
            string proposedName = name.Trim();
            if (PresetCatalog.Exists(proposedName))
                Find.WindowStack.Add(new PresetOverwriteDialog(proposedName, () => Commit(proposedName, true)));
            else Commit(proposedName, false);
        }
        public override void OnAcceptKeyPressed()
        {
            Submit();
            if (Event.current != null && Event.current.type == EventType.KeyDown) Event.current.Use();
        }
        private void Commit(string presetName, bool overwrite)
        {
            error = save(presetName, overwrite);
            if (error == null) Close();
        }
#if MULLD_SMOKE_TESTS
        internal void SmokeSubmit(string value) { name = value; OnAcceptKeyPressed(); }
#endif
    }
    public sealed class PresetOverwriteDialog : Window
    {
        public readonly string PresetName;
        private readonly Action overwrite;
        public override Vector2 InitialSize => new Vector2(440, 210);
        public PresetOverwriteDialog(string name, Action overwrite)
        {
            PresetName = name; this.overwrite = overwrite;
            doCloseX = true; absorbInputAroundWindow = true; forcePause = true;
            closeOnAccept = false;
        }
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, rect.width, 32), "Overwrite “" + PresetName + "”?"); Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0, 44, rect.width, 55), "Replace the saved rules for this preset? Current priorities remain unchanged.");
            float width = (rect.width - 8) / 2;
            if (Widgets.ButtonText(new Rect(0, rect.height - 35, width, 30), "Back")) Close();
            if (Widgets.ButtonText(new Rect(width + 8, rect.height - 35, width, 30), "Overwrite Preset")) Confirm();
        }
        private void Confirm() { overwrite(); Close(); }
#if MULLD_SMOKE_TESTS
        internal void SmokeConfirm() => Confirm();
#endif
    }
}

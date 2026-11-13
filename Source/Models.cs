using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Mulld.Priorities
{
    public sealed class WorkRule : IExposable
    {
        public WorkTypeDef Work;
        public bool Enabled;
        public string Mode = "Count";
        public int Amount = 2;
        public int Priority = 2;
        public int MinSkill;
        public bool FavorPassion = true;
        public bool AdultsOnly;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref Work, "work");
            Scribe_Values.Look(ref Enabled, "enabled");
            Scribe_Values.Look(ref Mode, "mode", "Count");
            Scribe_Values.Look(ref Amount, "amount", 2);
            Scribe_Values.Look(ref Priority, "priority", 2);
            Scribe_Values.Look(ref MinSkill, "minSkill");
            Scribe_Values.Look(ref FavorPassion, "favorPassion", true);
            Scribe_Values.Look(ref AdultsOnly, "adultsOnly");
        }
    }

    public sealed class Assignment : IExposable
    {
        public Pawn Pawn;
        public WorkTypeDef Work;
        public int Before;
        public int After;
        public string Reason = "Manual adjustment";
        public string NeedKey;
        public int ExpiresAt;
        public string Key => Pawn == null || Work == null ? "" : Pawn.ThingID + "|" + Work.defName;
        public Assignment() { }
        public Assignment(Pawn pawn, WorkTypeDef work, int before, int after, string reason)
        { Pawn = pawn; Work = work; Before = before; After = after; Reason = reason; }
        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Defs.Look(ref Work, "work");
            Scribe_Values.Look(ref Before, "before");
            Scribe_Values.Look(ref After, "after");
            Scribe_Values.Look(ref Reason, "reason", "Manual adjustment");
            Scribe_Values.Look(ref NeedKey, "needKey");
            Scribe_Values.Look(ref ExpiresAt, "expiresAt");
        }
        public Assignment Copy() => new Assignment(Pawn, Work, Before, After, Reason) { NeedKey = NeedKey, ExpiresAt = ExpiresAt };
    }

    public sealed class ColonyNeed
    {
        public string Key, Label, Value, Status, Evidence, WorkName, Explanation;
        public int Severity;
        public bool Resolved;
        public Assignment Suggestion;
    }

    public sealed class ColonySnapshot
    {
        public int Tick;
        public float FoodDays, RawNutrition;
        public int Patients, ReadyCrops, RiskyCrops, Haulables;
        public readonly List<ColonyNeed> Needs = new List<ColonyNeed>();
    }

    public static class WorkAccess
    {
        public static string Key(Pawn p, WorkTypeDef w) => p.ThingID + "|" + w.defName;
        public static bool Capable(Pawn p, WorkTypeDef w) => p != null && !p.Dead && p.workSettings != null && p.workSettings.EverWork && !p.WorkTypeIsDisabled(w);
        public static bool Available(Pawn p) => !p.Downed && !p.Drafted && !p.InMentalState;
        public static bool Adult(Pawn p) => p.DevelopmentalStage == DevelopmentalStage.Adult;
        public static float Skill(Pawn p, WorkTypeDef w) => p.skills == null || w.relevantSkills == null || w.relevantSkills.Count == 0 ? 0f : p.skills.AverageOfRelevantSkillsFor(w);
        public static float Score(Pawn p, WorkTypeDef w, bool passion) => Skill(p, w) + (passion && p.skills != null ? (int)p.skills.MaxPassionOfRelevantSkillsFor(w) * 3f : 0f);
        public static bool Dangerous(WorkTypeDef w) => w.defName == "Hunting" || w.defName == "Handling" || w.defName == "Firefighter";
        public static bool Emergency(WorkTypeDef w) => w.defName == "Firefighter" || w.defName == "Patient" || w.defName == "PatientBedRest";
        public static string Label(WorkTypeDef w) => (w.labelShort ?? w.label ?? w.defName).CapitalizeFirst();
        public static List<WorkTypeDef> Works() => DefDatabase<WorkTypeDef>.AllDefsListForReading.FindAll(w => w.visible && w.VisibleCurrently);
        public static string PriorityLabel(int value) => value == 0 ? "Off" : value.ToString();
    }
}

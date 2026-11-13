using System;
using System.Collections.Generic;
using System.Linq;
using Mulld.Priorities;

public static class PlannerTests
{
    public static string Run()
    {
        int passed = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; }
        Check(Planner.StaffingTarget("Count", 4, 2) == 2, "Count limited by eligibility");
        Check(Planner.StaffingTarget("Percent", 25, 5) == 2, "Percent rounds up");
        Check(Planner.StaffingTarget("Percent", 0, 5) == 0, "Zero percent");
        Check(Planner.StaffingTarget("Percent", 200, 5) == 5, "Percent capped");
        Check(Planner.StaffingTarget("Everyone", 0, 5) == 5, "Everyone");
        Check(Planner.StaffingTarget("Off", 99, 5) == 0, "Off");
        Check(Planner.StaffingTarget("Count", -2, 5) == 0, "Negative count");
        Check(Planner.StaffingTarget("Percent", 50, 0) == 0, "Empty eligible pool");
        var pawns = new List<Candidate> { new Candidate { Id = "a", Score = 14 }, new Candidate { Id = "b", Score = 10 }, new Candidate { Id = "c", Score = 3 } };
        var plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 2 && plan["b"] == 2 && plan["c"] == 0, "Skilled workers first");
        pawns[0].MainJobs = 2;
        plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 0 && plan["b"] == 2 && plan["c"] == 2, "Main-job cap respected");
        plan = Planner.Assign(pawns, 3, 1, 2, true);
        Check(plan.Values.All(v => v == 1), "Emergency exempt from cap");
        plan = Planner.Assign(pawns, 3, 4, 2, false);
        Check(plan.Values.All(v => v == 4), "Spare-time jobs exempt from cap");
        pawns[0].Locked = true; pawns[0].Current = 1;
        plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 1 && plan["b"] == 2 && plan["c"] == 0, "Locked assignments reserve staffing slot");
        plan = Planner.Assign(pawns, 0, 2, 2, false);
        Check(plan["a"] == 1 && plan["b"] == 0, "Off rule preserves lock");
        pawns[0].Current = 0;
        plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 0 && plan["b"] == 2 && plan["c"] == 2, "Locked off stays off");
        pawns[0].Locked = false; pawns[0].MainJobs = 0; pawns[0].Eligible = false;
        plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 0 && plan["b"] == 2, "Age and skill disqualification");
        pawns[0].Locked = true; pawns[0].Current = 3;
        plan = Planner.Assign(pawns, 2, 2, 2, false);
        Check(plan["a"] == 3, "Ineligible locked work is preserved");
        var tied = new[] { new Candidate { Id = "z", Score = 5 }, new Candidate { Id = "a", Score = 5 } };
        Check(Planner.Assign(tied, 1, 2, 2, false)["a"] == 2, "Stable ties");
        tied[1].MainJobs = 1;
        Check(Planner.Assign(tied, 1, 2, 2, false)["z"] == 2, "Lower load wins skill tie");
        Check(Planner.Assign(Array.Empty<Candidate>(), 1, 2, 2, false).Count == 0, "Empty plan");
        Check(Planner.CanRestore(2, 2, false), "Unmodified boost restored");
        Check(!Planner.CanRestore(3, 2, false), "External edit preserved");
        Check(!Planner.CanRestore(2, 2, true), "New protection preserved");
        var ids = new[] { "Cod", "Green", "Abrusa", "Wombat", "Giraffe" };
        ColonyWork Role(string name, double[] skills, bool essential = false, int priority = 2, int fallback = 4) => new ColonyWork
        {
            Id = name, Target = 1, Priority = priority, Backups = 1, BackupPriority = 3, FallbackPriority = fallback, Essential = essential,
            Candidates = ids.Select((id, i) => new Candidate { Id = id, Score = skills[i] }).ToList()
        };
        var colony = new List<ColonyWork> {
            Role("Doctor", new double[]{0,0,12,0,1}, true, 1),
            Role("Cook", new double[]{0,1,0,0,12}, true),
            Role("Grow", new double[]{14,0,0,0,5}, true),
            Role("Construct", new double[]{0,15,0,0,5}, true),
            Role("Mine", new double[]{2,3,1,4,0}),
            Role("Research", new double[]{1,0,3,0,10}),
            Role("Handle", new double[]{0,0,10,0,0}),
            new ColonyWork { Id="Haul", Target=5, Priority=4, FallbackPriority=4, GeneralWork=true, Candidates=ids.Select(id=>new Candidate{Id=id}).ToList() }
        };
        for (int i=0;i<colony.Count;i++) colony[i].Order=i;
        var balanced = Planner.AssignColony(colony, 2);
        Check(balanced["Doctor"]["Abrusa"] == 1, "Balanced: best doctor receives urgent coverage");
        Check(colony.Where(r=>r.Essential).All(r=>balanced[r.Id].Values.Any(v=>v>0&&v<=2)), "Balanced: every essential role has primary coverage");
        Check(colony.Take(7).All(r=>balanced[r.Id].Values.Count(v=>v>0&&v<=3)>=2), "Balanced: useful backups retained");
        Check(balanced.Values.All(values=>values.Values.All(v=>v>0)), "Balanced: capable fallback work is not wiped");
        Check(ids.All(id=>balanced.Values.Any(v=>v[id] is 1 or 2)), "Balanced: every pawn has a primary role");
        Check(ids.All(id=>balanced.Values.Count(v=>v[id] is 1 or 2)<=2), "Balanced: primary roles spread within budget");
        var solo=colony.Select(r=>new ColonyWork {Id=r.Id,Order=r.Order,Target=1,Priority=r.Priority,Backups=1,BackupPriority=3,FallbackPriority=r.FallbackPriority,Essential=r.Essential,GeneralWork=r.GeneralWork,Candidates=new List<Candidate>{new Candidate{Id="solo",Score=5}}}).ToList();
        balanced=Planner.AssignColony(solo,2);
        Check(solo.Where(r=>r.Essential).All(r=>balanced[r.Id]["solo"]>0), "Solo: essential work stays covered after cap");
        Check(balanced.Values.Count(v=>v["solo"] is 1 or 2)<=2, "Solo: excess main duties become support work");
        colony[1].Candidates[0].Locked=true; colony[1].Candidates[0].Current=0;
        balanced=Planner.AssignColony(colony,2);
        Check(balanced["Cook"]["Cod"]==0, "Balanced: locked-off assignments stay off");
        colony[2].Candidates[0].Eligible=false;
        balanced=Planner.AssignColony(colony,2);
        Check(balanced["Grow"]["Cod"]==0, "Balanced: eligibility applies to fallback and backup work");
        var idle = new[] { new ColonyWork { Id="Haul",Target=3,Priority=4,FallbackPriority=4,GeneralWork=true,Candidates=ids.Take(3).Select(id=>new Candidate{Id=id}).ToList() } };
        Check(Planner.AssignColony(idle,2)["Haul"].Values.All(v=>v==2), "Balanced: otherwise idle workers receive useful general work");
        Check(Planner.AssignColony(Array.Empty<ColonyWork>(),2).Count==0, "Balanced: empty colony safe");
        return passed + " policy checks passed.";
    }
}

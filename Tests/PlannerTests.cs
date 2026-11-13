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
        return passed + " policy checks passed.";
    }
}

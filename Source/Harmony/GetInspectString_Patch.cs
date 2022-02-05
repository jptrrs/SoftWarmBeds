using HarmonyLib;
using System.Text;
using RimWorld;
using Verse;

namespace SoftWarmBeds
{
    //Adds info on used bedding material to the inspector pane
    [HarmonyPatch(typeof(Building_Bed), "GetInspectString")]
    public class GetInspectString_Patch
    {
        public static void Postfix(object __instance, ref string __result)
        {
            if (!(__instance is Building_Bed bed)) return;
            CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
            if (bedComp == null) return;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine();
            if (bedComp.Loaded)
            {
                float health = (float)bedComp.loadedBedding.HitPoints / (float)bedComp.loadedBedding.MaxHitPoints;
                stringBuilder.AppendLine("BedMade".Translate(bedComp.blanketStuff.LabelCap, bedComp.blanketStuff, health.ToStringPercent()));
            }
            else
            {
                stringBuilder.AppendLine("BedNotMade".Translate());
            }
            __result += stringBuilder.ToString().TrimEndNewlines();
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;

namespace SoftWarmBeds
{
    //test
    [HarmonyPatch(typeof(Pawn), "TickRare")]
    public class Pawn_TickRare_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            if (!__instance.RaceProps.Humanlike || !__instance.InBed()) return;
            Building_Bed softWarmBed = __instance.CurrentBed();
            CompMakeableBed bedComp = softWarmBed.TryGetComp<CompMakeableBed>();
            if (bedComp != null && !bedComp.Loaded)
            {

            }
        }
    }
}

using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace SoftWarmBeds
{
    //Taints bedding when pawns dies while sleeping on it.
    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Pawn_Kill_Patch
    {
        public static void Prefix(Pawn __instance)
        {
            if (!__instance.RaceProps.Humanlike || !__instance.InBed()) return;
            CompMakeableBed bedComp = __instance.CurrentBed().TryGetComp<CompMakeableBed>();
            if (bedComp != null && bedComp.Loaded)
            {
                var bedding = (Bedding)bedComp.loadedBedding;
                bedding.Notify_PawnKilled();
            }
        }
    }
}

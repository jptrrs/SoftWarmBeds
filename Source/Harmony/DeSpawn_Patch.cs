using HarmonyLib;
using RimWorld;
using Verse;

namespace SoftWarmBeds
{
    //Instructions to deal with the used bedding on de-spawn
    [HarmonyPatch(typeof(Building_Bed), "DeSpawn")]
    public class DeSpawn_Patch
    {
        public static void Prefix(object __instance)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null && bedComp.Loaded && bedComp.NotTheBlanket)
                {
                    bedComp.Unmake();
                }
            }
        }
    }
}

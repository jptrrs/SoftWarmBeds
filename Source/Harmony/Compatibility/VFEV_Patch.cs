using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace SoftWarmBeds
{
    //Prevents VFE - Vikings Beds from curing Hypotermia if they're not made
    [StaticConstructorOnStartup]
    public static class VFEV_Patch
    {
        static VFEV_Patch()
        {
            if (LoadedModManager.RunningModsListForReading.Any(x => x.PackageIdPlayerFacing.StartsWith("OskarPotocki.VFE.Vikings")))
            {
                var harmonyInstance = new Harmony("JPT_SoftWarmBeds.VFEV");

                Log.Message("[SoftWarmBeds] Vanilla Factions Expanded - Vikings detected! Adapting...");

                harmonyInstance.Patch(original: AccessTools.Method("VFEV.CompCureHypothermia:CompTickRare"),
                    prefix: new HarmonyMethod(typeof(VFEV_Patch), nameof(Prefix)), postfix: null, transpiler: null);
            }
        }

        public static bool Prefix(object __instance)
        {
            if (__instance is ThingComp compInstance)
            {
                if (compInstance.parent is Building_Bed bed)
                {
                    CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                    if (bedComp != null)
                    {
                        return !bedComp.Loaded;
                    }
                }
            }
            return true;
        }
    }
}

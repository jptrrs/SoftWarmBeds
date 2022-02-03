using HarmonyLib;
using RimWorld;
using Verse;

namespace SoftWarmBeds
{
    //Command to update the blanket color when needed
    [HarmonyPatch(typeof(Thing), "Notify_ColorChanged")]
    public class Notify_ColorChanged_Patch
    {
        public static void Postfix(object __instance)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null && bedComp.Loaded && bedComp.blanketDef != null)
                {
                    bedComp.blanket.Notify_ColorChanged();
                }
            }
        }
    }
}

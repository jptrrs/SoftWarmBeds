using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SoftWarmBeds
{
    //Tweak to the bed's secondary color
    [HarmonyPatch(typeof(Building_Bed), "DrawColorTwo", MethodType.Getter)] // Wait for a Harmony fix!
    public class DrawColorTwo_Patch
    {
        public static void Postfix(object __instance, ref Color __result)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null || bed.def.MadeFromStuff) // unmakeable non-stuffed beds aren't affected
                {
                    bool forPrisoners = bed.ForPrisoners;
                    bool medical = bed.Medical;
                    bool invertedColorDisplay = (SoftWarmBedsSettings.colorDisplayOption == ColorDisplayOption.Blanket);
                    if (!forPrisoners && !medical && !invertedColorDisplay)
                    {
                        if (bedComp != null && bedComp.Loaded && bedComp.blanketDef == null) // bedding color for beds that are made
                        {
                            __result = bedComp.blanketStuff.stuffProps.color;
                        }
                        else if (bed.def.MadeFromStuff) // stuff color for umade beds & bedrolls
                        {
                            __result = bed.DrawColor;
                        }
                    }
                }
            }
        }
    }
}

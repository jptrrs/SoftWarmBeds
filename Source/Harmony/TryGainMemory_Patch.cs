using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace SoftWarmBeds
{
    //Makes bed thoughts consider the bed stats when judging confortable temperature (new, more direct & mod-friendly approach)
    [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory", new Type[] { typeof(ThoughtDef), typeof(Pawn), typeof(Precept) })]
    public class TryGainMemory_Patch
    {
        public static bool Prefix(ThoughtDef def, Pawn ___pawn)
        {
            if (def.IsMemory)
            {
                var bed = ___pawn.CurrentBed();
                if (bed != null)
                {
                    if (def == ThoughtDefOf.SleptInCold)
                    {
                        float minTempInBed = ___pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin, null) - bed.GetStatValue(BedInsulationCold.Bed_Insulation_Cold, true);
                        return ___pawn.AmbientTemperature < minTempInBed;
                    }
                    if (def == ThoughtDefOf.SleptInHeat)
                    {
                        float maxTempInBed = ___pawn.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax, null) + bed.GetStatValue(BedInsulationHeat.Bed_Insulation_Heat, true);
                        return ___pawn.AmbientTemperature > maxTempInBed;
                    }
                }
            }
            return true;
        }
    }
}

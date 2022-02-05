using HarmonyLib;
using RimWorld;
using System;
using Verse;

namespace SoftWarmBeds
{
    //test
    [HarmonyPatch(typeof(Pawn), "TickRare")]
    public static class Pawn_TickRare_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            if (!__instance.RaceProps.Humanlike || !__instance.InBed()) return;
            CompMakeableBed bedComp = __instance.CurrentBed().TryGetComp<CompMakeableBed>();
            if (bedComp != null && bedComp.Loaded)
            {
                TakeWearoutDamageForNight(__instance, bedComp);
            }
        }

        public static void TakeWearoutDamageForNight(Pawn pawn, CompMakeableBed bedComp)
        {

            Thing ap = bedComp.loadedBedding;
            float num = GenMath.RoundRandom(ap.def.apparel.wearPerDay);
            Log.Message($"Wear damage for {ap.def} ({ap.HitPoints}/{ap.MaxHitPoints}): {num}");
            if (num > 0)
            {
                ap.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, num, 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
            }
            if (ap.Destroyed && PawnUtility.ShouldSendNotificationAbout(pawn) && !pawn.Dead)
            {
                bedComp.Unmake();
                Messages.Message("MessageWornApparelDeterioratedAway".Translate(GenLabel.ThingLabel(ap.def, ap.Stuff, 1), pawn).CapitalizeFirst(), pawn, MessageTypeDefOf.NegativeEvent, true);
            }
        }
    }
}

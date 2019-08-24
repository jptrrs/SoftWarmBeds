﻿using Harmony;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace SoftWarmBeds
{
    [StaticConstructorOnStartup]
    internal static class HarmonyInit
    {
        static HarmonyInit()
        {
            //HarmonyInstance.DEBUG = true;
            HarmonyInstance harmonyInstance = HarmonyInstance.Create("JPT_SoftWarmBeds");
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    //Instruction to draw pawn body when on a bed that's unmade
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt", new Type[] { typeof(Vector3) })]
    public static class PawnOnBedRenderer_Patch
    {
        public static void Postfix(PawnRenderer __instance, Pawn ___pawn, Vector3 drawLoc)
        {
            Building_Bed softWarmBed = ___pawn.CurrentBed();
            CompMakeableBed bedComp = softWarmBed.TryGetComp<CompMakeableBed>();
            if (___pawn.RaceProps.Humanlike && bedComp != null)//___pawn.CurrentBed() is Building_SoftWarmBed)
            {
                //if (bedComp != null)
                //{
                if (!bedComp.Loaded)
                {
                    // Thanks to @Zamu & @Mehni!
                    var rotDrawMode = (RotDrawMode)AccessTools.Property(typeof(PawnRenderer), "CurRotDrawMode").GetGetMethod(true).Invoke(__instance, new object[0]);
                    MethodInfo layingFacing = AccessTools.Method(type: typeof(PawnRenderer), name: "LayingFacing");
                    Rot4 rot = (Rot4)layingFacing.Invoke(__instance, new object[0]);
                    float angle;
                    Vector3 rootLoc;
                    Rot4 rotation = softWarmBed.Rotation;
                    rotation.AsInt += 2;
                    angle = rotation.AsAngle;
                    AltitudeLayer altLayer = (AltitudeLayer)Mathf.Max((int)softWarmBed.def.altitudeLayer, 15);
                    Vector3 vector2 = ___pawn.Position.ToVector3ShiftedWithAltitude(altLayer);
                    Vector3 vector3 = vector2;
                    vector3.y += 0.02734375f;
                    float d = -__instance.BaseHeadOffsetAt(Rot4.South).z;
                    Vector3 a = rotation.FacingCell.ToVector3();
                    rootLoc = vector2 + a * d;
                    rootLoc.y += 0.0078125f;
                    MethodInfo renderPawnInternal = AccessTools.Method(type: typeof(PawnRenderer), name: "RenderPawnInternal", parameters: new[] { typeof(Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool) });
                    renderPawnInternal.Invoke(__instance, new object[] { rootLoc, angle, true, rot, rot, rotDrawMode, false, false });
                }
                //}
            }
        }
    }

    //Makes bed thoughts consider the bed stats when judging confortable temperature (while juggling Hospitality functions!)
    [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
    [HarmonyBefore(new string[] { "HugsLib.Hospitality" })]
    public class ApplyBedThoughts_Patch
    {
        public static bool Prefix(object __instance, Pawn actor)
        {
            bool result;
            if (actor.needs.mood == null)
            {
                result = false;
            }
            else if (actor.CurrentBed() == null)
            {
                result = true;
            }
            else
            {
                Building_Bed building_Bed = new Building_Bed();
                bool hospitalityIsGuest = false;
                if (LoadedModManager.RunningModsListForReading.Any(x => x.Name == "Hospitality"))
                {
                    //reflection info
                    MethodInfo isGuest = AccessTools.Method("Hospitality.GuestUtility:IsGuest", new[] { typeof(Pawn) });
                    MethodInfo getGuestBeds = AccessTools.Method("Hospitality.GuestUtility:GetGuestBeds", new[] { typeof(Map), typeof(Area) });
                    MethodInfo getGuestArea = AccessTools.Method("Hospitality.GuestUtility:GetGuestArea", new[] { typeof(Pawn) });
                    hospitalityIsGuest = (bool)isGuest.Invoke(__instance, new object[] { actor });
                    Area hospitalityGetGuestArea = (Area)getGuestArea.Invoke(__instance, new object[] { actor });
                    IEnumerable hospitalityGetGuestBeds = (IEnumerable)getGuestBeds.Invoke(__instance, new object[] { actor.MapHeld, hospitalityGetGuestArea });
                    //
                    building_Bed = hospitalityIsGuest ? hospitalityGetGuestBeds.Cast<Building_Bed>().FirstOrDefault() : actor.CurrentBed();
                }
                else
                {
                    building_Bed = actor.CurrentBed();
                }
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBedroom);
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInBarracks);
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOutside);
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptOnGround);
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInCold);
                actor.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.SleptInHeat);
                if (actor.GetRoom(RegionType.Set_Passable).PsychologicallyOutdoors)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptOutside, null);
                }
                if (building_Bed == null || building_Bed.CostListAdjusted().Count == 0)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptOnGround, null);
                }
                //taking bed insulation into consideration:
                //Log.Message("bedStat: " + building_Bed.GetStatValue(BedInsulationCold.Bed_Insulation_Cold, true));
                float minTempInBed = actor.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin, null) - building_Bed.GetStatValue(BedInsulationCold.Bed_Insulation_Cold, true);
                float maxTempInBed = actor.def.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax, null) + building_Bed.GetStatValue(BedInsulationHeat.Bed_Insulation_Heat, true);
                if (actor.AmbientTemperature < minTempInBed)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptInCold, null);
                    //Log.Message("Temperature is " + actor.AmbientTemperature + " and bed insulation is " + minTempInBed + ",  so " + actor + " gains cold memory");
                }
                if (actor.AmbientTemperature > maxTempInBed)
                {
                    actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleptInHeat, null);
                    //Log.Message("Temperature is " + actor.AmbientTemperature + " and bed insulation is " + maxTempInBed + ",  so " + actor + " gains heat memory");
                }
                //adapted from hospitality:
                if (building_Bed != null && (hospitalityIsGuest || building_Bed == actor.ownership.OwnedBed) && !building_Bed.ForPrisoners && !actor.story.traits.HasTrait(TraitDefOf.Ascetic))
                {
                    ThoughtDef thoughtDef = null;
                    if (building_Bed.GetRoom(RegionType.Set_Passable).Role == RoomRoleDefOf.Bedroom)
                    {
                        thoughtDef = ThoughtDefOf.SleptInBedroom;
                    }
                    else
                    {
                        if (building_Bed.GetRoom(RegionType.Set_Passable).Role == RoomRoleDefOf.Barracks)
                        {
                            thoughtDef = ThoughtDefOf.SleptInBarracks;
                        }
                    }
                    if (thoughtDef != null)
                    {
                        int scoreStageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(building_Bed.GetRoom(RegionType.Set_Passable).GetStat(RoomStatDefOf.Impressiveness));
                        if (thoughtDef.stages[scoreStageIndex] != null)
                        {
                            actor.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(thoughtDef, scoreStageIndex), null);
                        }
                    }
                }
                result = false;
            }
            return result;
        }
    }

    //Makes the bed stats cover for a possible lack of apparel when calculating comfortable temperature range for pawns in bed
    [HarmonyPatch(typeof(GenTemperature), "ComfortableTemperatureRange", new Type[] { typeof(Pawn) })]
    public class ComfortableTemperatureRange_Patch
    {
        public static void Postfix(Pawn p, ref FloatRange __result)
        {
            if (p.RaceProps.Humanlike && p.InBed())
            {
                ThingDef raceDef = p.kindDef.race;
                Building_Bed bed = p.CurrentBed();
                FloatRange altResult = new FloatRange(raceDef.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin, null), raceDef.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax, null));
                altResult.min -= bed.GetStatValue(BedInsulationCold.Bed_Insulation_Cold, true);
                altResult.max += bed.GetStatValue(BedInsulationHeat.Bed_Insulation_Heat, true);
                if (__result.min > altResult.min) __result.min = altResult.min;
                if (__result.max < altResult.max) __result.max = altResult.max;
                //Log.Message("comfortable range modified for " + p + " by bed" + bed + ": " + __result.ToString());
            }
        }
    }

    //Adjusts the info report on comfortable temperatures
    [HarmonyPatch(typeof(StatsReportUtility), "FinalizeCachedDrawEntries")]
    public class FinalizeCachedDrawEntries_Patch
    {
        public static bool Prefix(IEnumerable<StatDrawEntry> original)
        {
            IEnumerable<StatDrawEntry> affected = (from de in original
                                                   where de.stat == StatDefOf.ComfyTemperatureMin || de.stat == StatDefOf.ComfyTemperatureMax
                                                   select de);
            foreach (StatDrawEntry target in affected)
            {
                StatRequest req = target.optionalReq;
                Pawn pawn = req.Thing as Pawn;
                StringBuilder alteredText = new StringBuilder();
                if (!pawn.InBed())
                {
                    alteredText.AppendLine(target.GetExplanationText(target.optionalReq));
                }
                else
                {
                    alteredText.AppendLine(target.GetExplanationText(StatRequest.ForEmpty()));
                    alteredText.AppendLine();
                    //reflection
                    MethodInfo baseValueInfo = AccessTools.Method(typeof(StatWorker), "GetBaseValueFor", new[] { typeof(BuildableDef) });
                    float baseValueFor = (float)baseValueInfo.Invoke(target.stat.Worker, new[] { req.Def });
                    FieldInfo numberSenseInfo = AccessTools.Field(typeof(StatDrawEntry), "numberSense");
                    ToStringNumberSense numberSense = (ToStringNumberSense)numberSenseInfo.GetValue(target);
                    //
                    alteredText.AppendLine(target.stat.Worker.GetExplanationUnfinalized(req, numberSense));
                    alteredText.AppendLine();
                    bool subtract = target.stat == StatDefOf.ComfyTemperatureMin;
                    StatDef modifier = subtract ? BedInsulationCold.Bed_Insulation_Cold : BedInsulationHeat.Bed_Insulation_Heat;
                    float bedStatValue = pawn.CurrentBed().GetStatValue(modifier, true);
                    float bedOffset = subtract ? bedStatValue * -1 : bedStatValue;
                    string signal = subtract ? null : "+";
                    alteredText.AppendLine("StatsReport_BedInsulation".Translate() + ": " + signal + bedOffset.ToStringTemperature());
                    alteredText.AppendLine();
                    alteredText.Append("StatsReport_FinalValue".Translate() + ": " + target.stat.ValueToString(baseValueFor + bedOffset, target.stat.toStringNumberSense));
                    //alteredText.AppendLine();
                    //alteredText.Append("DEBUG: subtract: " + subtract + " modifier: " + modifier.ToString() + " bedStatValue: " + bedStatValue + " bedOffset: " + bedOffset + " signal: " + signal);
                }
                target.overrideReportText = alteredText.ToString().TrimEndNewlines();
            }
            return true;
        }
    }

    //Adds info on used bedding material to the inspector pane
    [HarmonyPatch(typeof(Building_Bed), "GetInspectString")]
    public class GetInspectString_Patch
    {
        public static void Postfix(object __instance, ref string __result)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine();
                    if (bedComp.Loaded)
                    {
                        stringBuilder.AppendLine("BedMade".Translate(bedComp.blanketStuff.LabelCap, bedComp.blanketStuff));
                    }
                    else
                    {
                        stringBuilder.AppendLine("BedNotMade".Translate());
                    }
                    __result += stringBuilder.ToString().TrimEndNewlines();
                }
            }
        }
    }

    //Command to draw the blanket
    [HarmonyPatch(typeof(Building_Bed), "Draw")]
    public class Draw_Patch
    {
        public static void Postfix(object __instance)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null && bedComp.loaded)
                {
                    bedComp.DrawBed();
                }
            }
        }
    }

    //Instructions to deal with the used bedding on de-spawn
    [HarmonyPatch(typeof(Building_Bed), "DeSpawn")]
    public class DeSpawn_Patch
    {
        public static void Prefix(object __instance)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null && bedComp.loaded && bedComp.NotTheBlanket)
                {
                    bedComp.Unmake();
                }
            }
        }
    }

    //Tweak to the bed's secondary color
    [HarmonyPatch(typeof(Building_Bed), "DrawColorTwo", MethodType.Getter)]
    public class DrawColorTwo_Patch
    {
        public static void Prefix(object __instance, bool __state)
        {
            __state = false;
            if (__instance is Building_Bed bed && bed.def.MadeFromStuff) __state = true;
        }

        public static void Postfix(object __instance, bool __state, ref Color __result)
        {
            if (__instance is Building_Bed bed)
            {
                bool forPrisoners = bed.ForPrisoners;
                bool medical = bed.Medical;
                bool invertedColorDisplay = (SoftWarmBedsSettings.colorDisplayOption == ColorDisplayOption.Blanket);
                if (!forPrisoners && !medical && !invertedColorDisplay)
                {
                    CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                    if (bedComp != null && bedComp.loaded && bedComp.blanketDef == null)
                    {
                        __result = bedComp.blanketStuff.stuffProps.color;
                    }
                    else if (bed.def.MadeFromStuff)
                    {
                        __result = bed.DrawColor;
                    }
                    else
                    {
                        FieldInfo defaultColorInfo = AccessTools.Field(typeof(Building_Bed), "SheetColorNormal");
                        __result = (Color)defaultColorInfo.GetValue(bed);
                    }
                }
            }
        }
    }

    //Command to update the blanket color when needed
    [HarmonyPatch(typeof(Building_Bed), "Notify_ColorChanged")]
    public class Notify_ColorChanged_Patch
    {
        public static void Postfix(object __instance)
        {
            if (__instance is Building_Bed bed)
            {
                CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
                if (bedComp != null && bedComp.loaded && bedComp.blanketDef != null)
                {
                    bedComp.bedding.Notify_ColorChanged();
                }
            }
        }
    }

    //Interface to Hospitality for seamless guest bed switching
    [StaticConstructorOnStartup]
    public static class Hospitality_Patch
    {
        static Hospitality_Patch()
        {
            try
            {
                ((Action)(() =>
                {
                    if (LoadedModManager.RunningModsListForReading.Any(x => x.Name == "Hospitality"))
                    {
                        HarmonyInstance harmonyInstance = HarmonyInstance.Create("JPT_SoftWarmBeds.Hospitality");

                        Log.Message("[SoftWarmBeds] Hospitality detected! Adapting...");

                        harmonyInstance.Patch(original: AccessTools.Method("Hospitality.Building_GuestBed:Swap", new[] { typeof(Building_Bed) }),
                            prefix: new HarmonyMethod(type: typeof(Hospitality_Patch), name: nameof(SwapPatch)), postfix: null, transpiler: null);

                        harmonyInstance.Patch(original: AccessTools.Method("Hospitality.Building_GuestBed:GetInspectString"),
                            prefix: null, postfix: new HarmonyMethod(type: typeof(GetInspectString_Patch), name: nameof(GetInspectString_Patch.Postfix)), transpiler: null);
                    }
                }))();
            }
            catch (TypeLoadException ex) { }
        }

        public static bool SwapPatch(object __instance, Building_Bed bed)
        {
            CompMakeableBed bedComp = bed.TryGetComp<CompMakeableBed>();
            if (bedComp != null)
            {
                bedComp.NotTheBlanket = false;
                Swap(__instance, bed, bedComp.settings, bedComp);
                return false;
            }
            return true;
        }

        public static void Swap(object __instance, Building_Bed bed, StorageSettings settings, CompMakeableBed compMakeable)
        {
            ThingDef bedLoadedBedding = null;
            Thing bedBedding = null;
            if (compMakeable != null)
            {
                if (compMakeable.Loaded)
                {
                    bedLoadedBedding = compMakeable.loadedBedding;
                    bedBedding = compMakeable.bedding;
                }
            }
            //reflection info
            Type guestBed = AccessTools.TypeByName("Hospitality.Building_GuestBed");
            MethodInfo makeBedinfo = AccessTools.Method(guestBed, "MakeBed", new[] { typeof(Building_Bed), typeof(string) });
            //
            Building_Bed newBed;
            string newName;
            if (bed.GetType() == guestBed)
            {
                newName = bed.def.defName.Split(new string[] { "Guest" }, StringSplitOptions.RemoveEmptyEntries)[0];
            }
            else
            {
                newName = bed.def.defName + "Guest";
            }
            // Thanks again to @Zamu for figuring out it was actually very simple!
            newBed = (Building_Bed)makeBedinfo.Invoke(__instance, new object[] { bed, newName });
            newBed.SetFactionDirect(bed.Faction);
            var spawnedBed = (Building_Bed)GenSpawn.Spawn(newBed, bed.Position, bed.Map, bed.Rotation);
            spawnedBed.HitPoints = bed.HitPoints;
            spawnedBed.ForPrisoners = bed.ForPrisoners;
            var SpawnedCompQuality = spawnedBed.TryGetComp<CompQuality>();
            if (SpawnedCompQuality != null) SpawnedCompQuality.SetQuality(bed.GetComp<CompQuality>().Quality, ArtGenerationContext.Outsider);
            var SpawnedCompMakeable = spawnedBed.TryGetComp<CompMakeableBed>();
            if (SpawnedCompMakeable != null)
            {
                SpawnedCompMakeable.settings = settings;
                if (bedLoadedBedding != null)
                {
                    SpawnedCompMakeable.LoadBedding(bedLoadedBedding, bedBedding);
                }
            }
            Find.Selector.Select(spawnedBed, false, true);
        }
    }
}
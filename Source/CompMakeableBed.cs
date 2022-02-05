using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using HarmonyLib;
using System.Reflection;

namespace SoftWarmBeds
{
    using static SoftWarmBedsSettings;
    public class CompMakeableBed : CompFlickable , IStoreSettingsParent
    {
        public bool
            NotTheBlanket = true,
            customBlanketColor = false;
        private float curRotationInt;
        public ThingDef
            allowedBedding,
            blanketDef = null,
            blanketStuff = null;
        public Thing 
            blanket = null,
            loadedBedding;
        public Color BlanketDefaultColor = new Color(1f, 1f, 1f);
        public StorageSettings settings;

        private FieldInfo 
            baseWantSwitchInfo = AccessTools.Field(typeof(CompFlickable), "wantSwitchOn"),
            baseSwitchOnIntInfo = AccessTools.Field(typeof(CompFlickable), "switchOnInt");

        public bool switchOnInt
        {
            get
            {
                return (bool)baseSwitchOnIntInfo.GetValue(this);
            }
            set
            {
                baseSwitchOnIntInfo.SetValue(this, value);
            }
        }

        public bool wantSwitchOn
        {
            get
            {
                return (bool)baseWantSwitchInfo.GetValue(this);
            }
            set
            {
                baseWantSwitchInfo.SetValue(this, value);
            }
        }

        public bool Loaded => LoadedBedding != null; 

        public Thing LoadedBedding => loadedBedding;

        public CompProperties_MakeableBed Props => (CompProperties_MakeableBed)props;

        public bool StorageTabVisible => true;

        private Building_Bed BaseBed => parent as Building_Bed;

        private Color BlanketColor
        {
            get
            {
                if (!Loaded) return BlanketDefaultColor;
                if (allowColorVariation && customBlanketColor) return LoadedBedding.TryGetComp<CompColorable>().Color;
                return blanketStuff.stuffProps.color;
            }
        }

        private float CurRotation
        {
            get
            {
                return curRotationInt;
            }
            set
            {
                curRotationInt = value;
                if (curRotationInt > 360f)
                {
                    curRotationInt -= 360f;
                }
                if (curRotationInt < 0f)
                {
                    curRotationInt += 360f;
                }
            }
        }

        //private bool Occupied => BaseBed.CurOccupants != null; 

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
            {
                yield return gizmo;
            }
            if (!Loaded) yield break;
            if (manuallyUnmakeBed)
            {
                Props.commandTexture = Props.beddingDef.graphicData.texPath;
                foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                {
                    yield return gizmo;
                }
            }
            else
            {
                Command_Action unmake = new Command_Action
                {
                    defaultLabel = Props.commandLabelKey.Translate(),
                    defaultDesc = Props.commandDescKey.Translate(),
                    icon = LoadedBedding.def.uiIcon,
                    iconAngle = LoadedBedding.def.uiIconAngle,
                    iconOffset = LoadedBedding.def.uiIconOffset,
                    iconDrawScale = GenUI.IconDrawScale(LoadedBedding.def),
                    action = delegate ()
                    {
                        Unmake();
                    }
                };
                yield return unmake;
            }
        }

        public override void CompTick()
        {
            if (!Loaded || settings.filter.Allows(blanketStuff)) return;
            if (!manuallyUnmakeBed || (switchOnInt && wantSwitchOn)) Unmake();
        }

        public void DrawBed()
        {
            if (blanketDef == null || this.blanket == null) return;
            Building_Blanket blanket = this.blanket as Building_Blanket;
            bool invertedColorDisplay = (colorDisplayOption == ColorDisplayOption.Blanket);
            if (invertedColorDisplay)
            {
                blanket.DrawColor = parent.Graphic.colorTwo;
                blanket.colorTwo = BlanketColor;
            }
            else
            {
                blanket.DrawColor = BlanketColor;
                if (parent.DrawColorTwo == parent.DrawColor)
                {
                    blanket.colorTwo = BlanketDefaultColor;
                }
                else
                {
                    blanket.colorTwo = parent.Graphic.colorTwo;
                }
            }
            this.blanket.Graphic.Draw(parent.DrawPos + Altitudes.AltIncVect, parent.Rotation, this.blanket);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (Loaded) DrawBed();
        }

        public StorageSettings GetParentStoreSettings()
        {
            return parent.def.building.fixedStorageSettings;
        }

        public StorageSettings GetStoreSettings()
        {
            return settings;
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            allowedBedding = new ThingDef();
            allowedBedding = Props.beddingDef;
            blanketDef = new ThingDef();
            blanketDef = Props.blanketDef;
            SetUpStorageSettings();
        }

        public void LoadBedding(Thing bedding)
        {
            loadedBedding = bedding;
            blanketStuff = bedding.Stuff;
            GenerateBlanket();
            parent.Notify_ColorChanged();
            wantSwitchOn = true;
            switchOnInt = true;
        }

        private void GenerateBlanket()
        {
            if (blanketDef == null || !Loaded) return;
            blanket = ThingMaker.MakeThing(blanketDef, blanketStuff);
            customBlanketColor = loadedBedding.TryGetComp<CompColorable>().Active;
            if (Scribe.mode == LoadSaveMode.Inactive && BaseBed.Faction != null) DrawBed();
        }

        public void LoadBedding(ThingDef stuff)
        {
            Thing bedding = ThingMaker.MakeThing(Props.beddingDef, stuff);
            if (bedding != null) LoadBedding(bedding);
            else Log.Error($"[SoftWarmBeds] Error creating {stuff.label} bedding for {parent}.");
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look<StorageSettings>(ref settings, "settings", new object[] { this });
            if (settings == null) SetUpStorageSettings();
            Scribe_Defs.Look<ThingDef>(ref blanketStuff, "blanketStuff");
            if (Scribe.mode != LoadSaveMode.Saving) //backward compatibility measure:
            {
                bool oldLoaded = false;
                Scribe_Values.Look<bool>(ref oldLoaded, "loaded", false, false);
                if (oldLoaded && blanketStuff != null)
                {
                    Log.Message($"[SoftWarmBeds] {parent} was saved on the old format. Fixing it up with a neat {blanketStuff.label} bedding.");
                    LoadBedding(blanketStuff);
                    return;
                }
            }
            Scribe_Deep.Look<Thing>(ref loadedBedding, "loadedBedding", new object[0]);
            GenerateBlanket();
        }

        public override void PostSplitOff(Thing bedding)
        {
            if (blanketDef != null && this.blanket != null)
            {
                Building_Blanket blanket = this.blanket as Building_Blanket;
                blanket.colorTwo = parent.Graphic.colorTwo;
                parent.Notify_ColorChanged();
            }
        }

        public void SetUpStorageSettings()
        {
            if (GetParentStoreSettings() != null)
            {
                settings = new StorageSettings(this);
                settings.CopyFrom(GetParentStoreSettings());
            }
        }

        public void Unmake()
        {
            if (manuallyUnmakeBed)
            {
                wantSwitchOn = !wantSwitchOn;
                FlickUtility.UpdateFlickDesignation(parent);
            }
            else RemoveBedding();
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (manuallyUnmakeBed && Loaded && !wantSwitchOn) RemoveBedding();
        }

        public void RemoveBedding()
        {
            if (LoadedBedding == null)
            {
                Log.Warning("[SoftWarmBeds] Tried to unmake a bed with no loaded bedding!");
            }
            if (LoadedBedding.Destroyed || GenPlace.TryPlaceThing(LoadedBedding, BaseBed.Position, BaseBed.Map, ThingPlaceMode.Near, null, null))
            {
                loadedBedding = null;
                customBlanketColor = false;
                BaseBed.Notify_ColorChanged();
            }
        }
    }
}
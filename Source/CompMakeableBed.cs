using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using HarmonyLib;
using System.Reflection;

namespace SoftWarmBeds
{
    public class CompMakeableBed : CompFlickable , IStoreSettingsParent
    {
        public ThingDef allowedBedding;
        public Thing blanket = null;
        public ThingDef blanketDef = null;
        public Color BlanketDefaultColor = new Color(1f, 1f, 1f);
        public ThingDef blanketStuff = null;
        public bool loaded = false;
        public ThingDef loadedBedding;
        public bool NotTheBlanket = true;
        public StorageSettings settings;
        private float curRotationInt;

        private FieldInfo baseWantSwitchInfo = AccessTools.Field(typeof(CompFlickable), "wantSwitchOn");
        private FieldInfo baseSwitchOnIntInfo = AccessTools.Field(typeof(CompFlickable), "switchOnInt");

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

        public bool Loaded
        {
            get
            {
                return LoadedBedding != null;
            }
        }

        public ThingDef LoadedBedding
        {
            get
            {
                return loadedBedding;
            }
        }

        public CompProperties_MakeableBed Props
        {
            get
            {
                return (CompProperties_MakeableBed)props;
            }
        }

        public void Notify_SettingsChanged()
        {
            
        }

        public bool StorageTabVisible
        {
            get
            {
                return true;
            }
        }

        private Building_Bed BaseBed
        {
            get
            {
                return parent as Building_Bed;
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

        private bool Occupied
        {
            get
            {
                return BaseBed.CurOccupants != null;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(settings))
            {
                yield return gizmo;
            }
            if (loaded)
            {
                if (SoftWarmBedsSettings.manuallyUnmakeBed)
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
                        icon = LoadedBedding.uiIcon,
                        iconAngle = LoadedBedding.uiIconAngle,
                        iconOffset = LoadedBedding.uiIconOffset,
                        iconDrawScale = GenUI.IconDrawScale(LoadedBedding),
                        action = delegate ()
                        {
                            Unmake();
                        }
                    };
                    yield return unmake;
                }
            }
            yield break;
        }

        public override void CompTick()
        {
            if (Loaded && !settings.filter.Allows(blanketStuff))
            {
                bool act = true;
                if (SoftWarmBedsSettings.manuallyUnmakeBed)
                {
                    act = switchOnInt && wantSwitchOn;
                }
                if (act) Unmake();
            }
        }

        //not present in CompChangeableProjectiles
        public void DrawBed()
        {
            if (blanketDef != null && blanket != null)
            {
                Building_Blanket blanket = this.blanket as Building_Blanket;
                bool invertedColorDisplay = (SoftWarmBedsSettings.colorDisplayOption == ColorDisplayOption.Blanket);
                if (invertedColorDisplay)
                {
                    blanket.DrawColor = parent.Graphic.colorTwo;
                    blanket.colorTwo = blanketStuff.stuffProps.color;
                }
                else
                {
                    blanket.DrawColor = blanketStuff.stuffProps.color;
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
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (Loaded) DrawBed();
        }

        public StorageSettings GetParentStoreSettings()
        {
            return parent.def.building.fixedStorageSettings;//defaultStorageSettings;
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

        //modified from CompChangeableProjectiles
        public void LoadBedding(Thing bedding)
        {
            loaded = true;
            loadedBedding = bedding.def;
            blanketStuff = bedding.Stuff;
            //if (this.bedding.TryGetComp<CompColorable>().Active) //test
            //{
            //    blanketColor = bedding.TryGetComp<CompColorable>().Color;
            //}
            //else
            //{
            //    blanketColor = blanketStuff.stuffProps.color;
            //}
            if (blanketDef != null)
            {
                this.blanket = ThingMaker.MakeThing(blanketDef, blanketStuff);
                //Building_Blanket blanket = this.bedding as Building_Blanket;
                //blanket.TryGetComp<CompColorable>().Color = blanketColor;
                //blanket.hasColor = true;
                if (BaseBed.Faction != null) DrawBed();
            }
            parent.Notify_ColorChanged();
            wantSwitchOn = true;
            switchOnInt = true;
        }

        public void LoadBedding(ThingDef stuff)
        {
            Thing bedding = ThingMaker.MakeThing(Props.beddingDef, stuff);
            LoadBedding(bedding);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look<bool>(ref loaded, "loaded", false, false);
            Scribe_Defs.Look<ThingDef>(ref loadedBedding, "loadedBedding");
            Scribe_Deep.Look<Thing>(ref blanket, "bedding", new object[0]);
            Scribe_Defs.Look<ThingDef>(ref blanketStuff, "blanketStuff");
            //if (loaded && blanketDef != null)
            //{
            //    Building_Blanket blanket = this.blanket as Building_Blanket;
            //    //Scribe_Values.Look<bool>(ref blanket.hasColor, "hasColor", false, false);
            //}
            Scribe_Deep.Look<StorageSettings>(ref settings, "settings", new object[] { this });
            if (settings == null)
            {
                SetUpStorageSettings();
            }
        }

        public override void PostSplitOff(Thing bedding)
        {
            if (blanketDef != null && this.blanket != null)
            {
                Building_Blanket blanket = this.blanket as Building_Blanket;
                //if (blanket.hasColor)
                //{
                blanket.colorTwo = parent.Graphic.colorTwo;
                parent.Notify_ColorChanged();
                //}
            }
        }

        //modified from CompChangeableProjectiles to accept stuff
        public Thing RemoveBedding(ThingDef stuff)
        {
            Thing thing = ThingMaker.MakeThing(loadedBedding, stuff);
            loaded = false;
            loadedBedding = null;
            return thing;
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
            if (SoftWarmBedsSettings.manuallyUnmakeBed)
            {
                wantSwitchOn = !wantSwitchOn;
                FlickUtility.UpdateFlickDesignation(this.parent);
            }
            else DoUnmake();
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (SoftWarmBedsSettings.manuallyUnmakeBed && Loaded && !wantSwitchOn) DoUnmake();
        }

        public void DoUnmake()
        {
            ThingDef stuff = blanketStuff;
            GenPlace.TryPlaceThing(RemoveBedding(stuff), BaseBed.Position, BaseBed.Map, ThingPlaceMode.Near, null, null);
            BaseBed.Notify_ColorChanged();
        }
    }
}
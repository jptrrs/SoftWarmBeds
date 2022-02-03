using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SoftWarmBeds
{
    public class JobDriver_MakeBed : JobDriver
    {
        private const TargetIndex 
            BeddingInd = TargetIndex.B,
            MakeableInd = TargetIndex.A;

        private const int MakingDuration = 180;

        protected Thing Bed => job.GetTarget(TargetIndex.A).Thing;

        protected CompMakeableBed bedComp => Bed.TryGetComp<CompMakeableBed>();

        protected Thing Bedding => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = Bed;
            Job job = this.job;
            bool result;
            if (pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
            {
                pawn = this.pawn;
                target = Bedding;
                job = this.job;
                result = pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
            }
            else
            {
                result = false;
            }
            return result;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            base.AddEndCondition(() => (!bedComp.Loaded) ? JobCondition.Ongoing : JobCondition.Succeeded);
            job.count = 1;
            Toil reserveBedding = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
            yield return reserveBedding;
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveBedding, TargetIndex.B, TargetIndex.None, true, null);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(MakingDuration, TargetIndex.None).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            Toil makeTheBed = new Toil();
            makeTheBed.initAction = delegate
            {
                Pawn actor = makeTheBed.actor;
                bedComp.LoadBedding(actor.CurJob.targetB.Thing);
                //actor.carryTracker.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
                actor.carryTracker.innerContainer.Clear();
            };
            makeTheBed.defaultCompleteMode = ToilCompleteMode.Instant;
            makeTheBed.FailOnDespawnedOrNull(TargetIndex.A);
            yield return makeTheBed;
            yield break;
        }
    }
}
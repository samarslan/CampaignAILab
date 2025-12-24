using CampaignAILab.Decisions;
using CampaignAILab.Logging;
using CampaignAILab.Tracking;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CampaignAILab.Resolution
{
    public static class OutcomeResolver
    {
        /* =========================================================
         * ARMY OUTCOMES
         * ========================================================= */

        public static void OnPartyJoinedArmy(MobileParty party)
        {
            if (party == null)
                return;

            var decision = DecisionTracker.GetActive(party.StringId);
            if (decision == null || decision.DecisionType != "JoinArmy")
                return;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = decision.DecisionId,
                OutcomeType = "Success",
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(party.StringId);
        }

        public static void OnPartyLeftArmy(MobileParty party)
        {
            if (party == null)
                return;

            var decision = DecisionTracker.GetActive(party.StringId);
            if (decision == null || decision.DecisionType != "JoinArmy")
                return;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = decision.DecisionId,
                OutcomeType = "Aborted",
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(party.StringId);
        }

        /* =========================================================
         * RAID OUTCOMES
         * ========================================================= */

        public static void OnVillageLooted(Village village)
        {
            if (village == null)
                return;

            foreach (var decision in DecisionTracker.GetAllActive())
            {
                if (decision.DecisionType != "RaidVillage")
                    continue;

                if (decision.TargetId != village.StringId)
                    continue;

                float durationHours =
                    (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

                AsyncLogger.EnqueueOutcome(new OutcomeRecord
                {
                    DecisionId = decision.DecisionId,
                    OutcomeType = "Success",
                    ResolutionTime = CampaignTime.Now,
                    DurationHours = durationHours
                });

                DecisionTracker.Resolve(decision.PartyId);
            }
        }

        /* =========================================================
         * MAP EVENT END
         * ========================================================= */

        public static void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.IsRaid)
                return;

            var raider = mapEvent.AttackerSide?.LeaderParty?.MobileParty;
            if (raider == null)
                return;

            var decision = DecisionTracker.GetActive(raider.StringId);
            if (decision == null || decision.DecisionType != "RaidVillage")
                return;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = decision.DecisionId,
                OutcomeType = "Failure",
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(raider.StringId);
        }

        /* =========================================================
         * PARTY DESTRUCTION
         * ========================================================= */

        public static void OnPartyRemoved(PartyBase party)
        {
            var mobileParty = party?.MobileParty;
            if (mobileParty == null)
                return;

            var decision = DecisionTracker.GetActive(mobileParty.StringId);
            if (decision == null)
                return;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = decision.DecisionId,
                OutcomeType = "Failure",
                PartyDestroyed = true,
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(mobileParty.StringId);
        }
    }
}

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

            var entry = DecisionTracker.GetRegistryEntry(party.StringId);
            if (entry == null ||
                entry.Status != DecisionStatus.Executing ||
                entry.Decision.DecisionType != "JoinArmy")
                return;

            entry.Status = DecisionStatus.Completed;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = "Completed",
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(party.StringId);
        }

        public static void OnPartyLeftArmy(MobileParty party)
        {
            if (party == null)
                return;

            var entry = DecisionTracker.GetRegistryEntry(party.StringId);
            if (entry == null ||
                entry.Status != DecisionStatus.Executing ||
                entry.Decision.DecisionType != "JoinArmy")
                return;

            entry.Status = DecisionStatus.Aborted;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
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

            foreach (var partyId in DecisionTracker.GetAllActivePartyIds())
            {
                var entry = DecisionTracker.GetRegistryEntry(partyId);
                if (entry == null ||
                    entry.Status != DecisionStatus.Executing ||
                    entry.Decision.DecisionType != "RaidVillage" ||
                    entry.Decision.TargetId != village.StringId)
                    continue;

                entry.Status = DecisionStatus.Completed;

                float durationHours =
                    (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

                AsyncLogger.EnqueueOutcome(new OutcomeRecord
                {
                    DecisionId = entry.Decision.DecisionId,
                    OutcomeType = "Completed",
                    ResolutionTime = CampaignTime.Now,
                    DurationHours = durationHours
                });

                DecisionTracker.Resolve(partyId);
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

            var entry = DecisionTracker.GetRegistryEntry(raider.StringId);
            if (entry == null ||
                entry.Status != DecisionStatus.Executing ||
                entry.Decision.DecisionType != "RaidVillage")
                return;

            entry.Status = DecisionStatus.Invalidated;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = "Invalidated",
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

            var entry = DecisionTracker.GetRegistryEntry(mobileParty.StringId);
            if (entry == null ||
                entry.Status != DecisionStatus.Executing)
                return;

            entry.Status = DecisionStatus.Invalidated;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = "Invalidated",
                PartyDestroyed = true,
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours
            });

            DecisionTracker.Resolve(mobileParty.StringId);
        }
    }
}

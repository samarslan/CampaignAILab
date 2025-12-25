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

            // ✅ Delegates lifecycle + logging to DecisionTracker
            DecisionTracker.MarkCompleted(
                party.StringId,
                "JoinedArmy"
            );
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

            DecisionTracker.MarkAborted(
                party.StringId,
                "LeftArmy"
            );
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

                DecisionTracker.MarkCompleted(
                    partyId,
                    "VillageLooted"
                );
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

            DecisionTracker.MarkInvalidated(
                raider.StringId,
                "RaidEndedWithoutLoot"
            );
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

            DecisionTracker.MarkInvalidated(
                mobileParty.StringId,
                "PartyDestroyed"
            );
        }
    }
}

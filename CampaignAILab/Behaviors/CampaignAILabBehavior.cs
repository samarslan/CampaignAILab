using CampaignAILab.Resolution;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CampaignAILab.Behaviors
{
    public class CampaignAILabBehavior : CampaignBehaviorBase
    {
        private static void OnDailyFlushTick(MobileParty party)
        {
            // Guard: flush exactly once per day
            if (party == null || !party.IsMainParty)
                return;

            Logging.AsyncLogger.Flush();
        }

        public override void RegisterEvents()
        {
            // Core decision sampling tick
            CampaignEvents.DailyTickPartyEvent
                .AddNonSerializedListener(this, Tracking.DecisionTracker.OnDailyPartyTick);

            // Logger flush tick (ONCE per day, guarded)
            CampaignEvents.DailyTickPartyEvent
                .AddNonSerializedListener(this, OnDailyFlushTick);

            // Army lifecycle
            CampaignEvents.OnPartyJoinedArmyEvent
                .AddNonSerializedListener(this, Resolution.OutcomeResolver.OnPartyJoinedArmy);

            CampaignEvents.PartyRemovedFromArmyEvent
                .AddNonSerializedListener(this, Resolution.OutcomeResolver.OnPartyLeftArmy);

            // Raid / village outcomes
            CampaignEvents.VillageLooted
                .AddNonSerializedListener(this, Resolution.OutcomeResolver.OnVillageLooted);

            // All battles, raids, sieges end here
            CampaignEvents.MapEventEnded
                .AddNonSerializedListener(this, Resolution.OutcomeResolver.OnMapEventEnded);

            // Party destruction (PartyBase!)
            CampaignEvents.OnPartyRemovedEvent
                .AddNonSerializedListener(this, Resolution.OutcomeResolver.OnPartyRemoved);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}

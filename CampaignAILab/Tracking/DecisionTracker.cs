using CampaignAILab.Context;
using CampaignAILab.Decisions;
using CampaignAILab.Logging;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace CampaignAILab.Tracking
{
    /// <summary>
    /// Infers AI decisions by observing persistent, external party commitments.
    /// One active decision per party at any given time.
    /// </summary>
    public static class DecisionTracker
    {
        public static IEnumerable<DecisionRecord> GetAllActive()
        {
            return ActiveDecisions.Values;
        }

        private static readonly Dictionary<string, DecisionRecord> ActiveDecisions = new Dictionary<string, DecisionRecord>();

        private static readonly Dictionary<string, PartyObservation> LastObservations = new Dictionary<string, PartyObservation>();

        /// <summary>
        /// Entry point called from CampaignEvents.DailyTickPartyEvent
        /// </summary>
        public static void OnDailyPartyTick(MobileParty party)
        {
            if (!IsValidParty(party))
                return;

            var partyId = party.StringId;

            var current = PartyObservation.Capture(party);

            if (!LastObservations.TryGetValue(partyId, out var previous))
            {
                LastObservations[partyId] = current;
                return;
            }

            // Infer a new decision if a commitment-level change occurred
            var inferredDecision = InferDecision(previous, current, party);
            if (inferredDecision != null)
            {
                AbortDecisionIfExists(party);

                inferredDecision.IsLogged = true;
                ActiveDecisions[partyId] = inferredDecision;

                AsyncLogger.EnqueueDecision(inferredDecision);
            }


            LastObservations[partyId] = current;
        }

        /// <summary>
        /// Detects meaningful intent commitment changes.
        /// </summary>
        private static DecisionRecord InferDecision(
            PartyObservation prev,
            PartyObservation curr,
            MobileParty party)
        {
            // 1️⃣ Army join / leave
            if (!prev.InArmy && curr.InArmy && party.Army != null)
            {
                return CreateDecision(party, "JoinArmy", party.Army.LeaderParty?.StringId);
            }

            // 2️⃣ Movement target change (strong commitment)
            if (prev.TargetSettlementId != curr.TargetSettlementId &&
                curr.TargetSettlementId != null)
            {
                return CreateDecision(
                    party,
                    "MoveToSettlement",
                    curr.TargetSettlementId);
            }

            // 3️⃣ Hostile proximity → raid intent inference
            if (curr.NearHostileVillage && !prev.NearHostileVillage)
            {
                return CreateDecision(
                    party,
                    "RaidVillage",
                    curr.NearHostileVillageId);
            }

            // 4️⃣ Entered settlement → defend / resupply
            if (!prev.InSettlement && curr.InSettlement)
            {
                return CreateDecision(
                    party,
                    "EnterSettlement",
                    curr.CurrentSettlementId);
            }

            return null;
        }

        private static DecisionRecord CreateDecision(
            MobileParty party,
            string decisionType,
            string targetId)
        {
            return new DecisionRecord
            {
                DecisionId = Guid.NewGuid().ToString(),
                Timestamp = CampaignTime.Now,
                PartyId = party.StringId,
                FactionId = party.MapFaction?.StringId,
                DecisionType = decisionType,
                TargetId = targetId,
                Context = DecisionContextBuilder.Build(party)
            };
        }

        /// <summary>
        /// Abort current decision if party switches intent.
        /// </summary>
        public static void AbortDecisionIfExists(MobileParty party)
        {
            if (!ActiveDecisions.TryGetValue(party.StringId, out var decision))
                return;

            if (decision.IsLogged)
            {
                float durationHours =
                    (float)(CampaignTime.Now.ToHours - decision.Timestamp.ToHours);

                AsyncLogger.EnqueueOutcome(new OutcomeRecord
                {
                    DecisionId = decision.DecisionId,
                    OutcomeType = "Aborted",
                    ResolutionTime = CampaignTime.Now,
                    DurationHours = durationHours
                });
            }

            ActiveDecisions.Remove(party.StringId);
        }


        public static DecisionRecord GetActive(string partyId)
            => ActiveDecisions.TryGetValue(partyId, out var d) ? d : null;

        public static void Resolve(string partyId)
            => ActiveDecisions.Remove(partyId);

        private static bool IsValidParty(MobileParty party)
        {
            return party != null
                && party.IsActive
                && !party.IsMilitia
                && !party.IsCaravan;
        }
    }

    /// <summary>
    /// Minimal snapshot to detect commitment changes between ticks.
    /// NOT logged.
    /// </summary>
    internal class PartyObservation
    {
        public bool InArmy;
        public string TargetSettlementId;
        public bool NearHostileVillage;
        public string NearHostileVillageId;
        public bool InSettlement;
        public string CurrentSettlementId;

        public static PartyObservation Capture(MobileParty party)
        {
            var obs = new PartyObservation
            {
                InArmy = party.Army != null,
                TargetSettlementId = party.TargetSettlement?.StringId,
                InSettlement = party.CurrentSettlement != null,
                CurrentSettlementId = party.CurrentSettlement?.StringId
            };

            // Detect hostile village proximity (raid intent inference)
            // Replace all usages of .Position2D with .GetPosition2D for both MobileParty and Settlement
            // In PartyObservation.Capture(MobileParty party):

            foreach (Village village in Village.All)
            {
                if (village.MapFaction != party.MapFaction &&
                    party.GetPosition2D.DistanceSquared(village.Settlement.GetPosition2D) < 400f)
                {
                    obs.NearHostileVillage = true;
                    obs.NearHostileVillageId = village.StringId;
                    break;
                }
            }

            return obs;
        }
    }
}

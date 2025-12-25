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
    /* =========================================================
     * DECISION LIFECYCLE
     *
     * Registered   → Decision observed & logged
     * Executing    → Decision currently active on map
     * Completed    → Intent fulfilled (not quality)
     * Aborted      → Decision stopped without replacement
     * Overridden   → Superseded by another decision
     * Invalidated  → Became impossible (party destroyed, etc)
     * ========================================================= */

    internal enum DecisionStatus
    {
        Registered,
        Executing,
        Completed,
        Aborted,
        Overridden,
        Invalidated
    }

    internal sealed class DecisionRegistryEntry
    {
        public DecisionRecord Decision;
        public DecisionStatus Status;
    }

    /// <summary>
    /// Infers AI decisions by observing persistent, external party commitments.
    /// One active decision per party at any given time.
    /// </summary>
    public static class DecisionTracker
    {
        /* =========================================================
         * REGISTRY ACCESS
         * ========================================================= */

        private static readonly Dictionary<string, DecisionRegistryEntry> ActiveDecisions
            = new Dictionary<string, DecisionRegistryEntry>();

        private static readonly Dictionary<string, PartyObservation> LastObservations
            = new Dictionary<string, PartyObservation>();

        internal static DecisionRegistryEntry GetRegistryEntry(string partyId)
            => ActiveDecisions.TryGetValue(partyId, out var e) ? e : null;

        internal static IEnumerable<string> GetAllActivePartyIds()
            => new List<string>(ActiveDecisions.Keys);

        public static IEnumerable<DecisionRecord> GetAllActive()
        {
            foreach (var e in ActiveDecisions.Values)
                yield return e.Decision;
        }

        public static DecisionRecord GetActive(string partyId)
            => ActiveDecisions.TryGetValue(partyId, out var e) ? e.Decision : null;

        /* =========================================================
         * ENTRY POINT
         * ========================================================= */

        /// <summary>
        /// Called from CampaignEvents.DailyTickPartyEvent
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

            // 1️⃣ Infer new decision
            var inferred = InferDecision(previous, current, party);
            if (inferred != null)
            {
                SupersedeDecisionIfExists(party, inferred);

                var entry = new DecisionRegistryEntry
                {
                    Decision = inferred,
                    Status = DecisionStatus.Registered
                };

                ActiveDecisions[partyId] = entry;
                AsyncLogger.EnqueueDecision(inferred);
            }

            // 2️⃣ Registered → Executing happens on first observed tick
            if (ActiveDecisions.TryGetValue(partyId, out var active) &&
                active.Status == DecisionStatus.Registered)
            {
                active.Status = DecisionStatus.Executing;
            }

            LastObservations[partyId] = current;
        }

        /* =========================================================
         * DECISION INFERENCE
         * ========================================================= */

        private static DecisionRecord InferDecision(
            PartyObservation prev,
            PartyObservation curr,
            MobileParty party)
        {
            // Army join
            if (!prev.InArmy && curr.InArmy && party.Army != null)
            {
                return CreateDecision(
                    party,
                    "JoinArmy",
                    party.Army.LeaderParty?.StringId);
            }

            // Movement commitment
            if (prev.TargetSettlementId != curr.TargetSettlementId &&
                curr.TargetSettlementId != null)
            {
                return CreateDecision(
                    party,
                    "MoveToSettlement",
                    curr.TargetSettlementId);
            }

            // Raid intent inference
            if (curr.NearHostileVillage && !prev.NearHostileVillage)
            {
                return CreateDecision(
                    party,
                    "RaidVillage",
                    curr.NearHostileVillageId);
            }

            // Settlement entry
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

        /* =========================================================
         * LIFECYCLE MANAGEMENT
         * ========================================================= */

        /// <summary>
        /// Supersedes an active decision when a new intent is observed.
        /// This is NOT an abort — intent was replaced.
        /// </summary>
        private static void SupersedeDecisionIfExists(MobileParty party, DecisionRecord newDecision)
        {
            if (!ActiveDecisions.TryGetValue(party.StringId, out var entry))
                return;

            if (entry.Status != DecisionStatus.Registered &&
                entry.Status != DecisionStatus.Executing)
                return;

            float durationHours =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            entry.Status = DecisionStatus.Overridden;

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = "Overridden",
                ResolutionTime = CampaignTime.Now,
                DurationHours = durationHours,

                // NEW — causal link
                OverriddenByDecisionType = newDecision.DecisionType
            });


            ActiveDecisions.Remove(party.StringId);
        }

        /// <summary>
        /// Finalizes a decision already in a terminal state.
        /// </summary>
        public static void Resolve(string partyId)
        {
            if (!ActiveDecisions.TryGetValue(partyId, out var entry))
                return;

            if (entry.Status == DecisionStatus.Registered ||
                entry.Status == DecisionStatus.Executing)
            {
                // This should NEVER happen
                AsyncLogger.EnqueueOutcome(new OutcomeRecord
                {
                    DecisionId = entry.Decision.DecisionId,
                    OutcomeType = "Invalidated",
                    ResolutionTime = CampaignTime.Now,
                    Notes = "ForcedResolveWithoutTerminalState"
                });
            }

            ActiveDecisions.Remove(partyId);
        }


        private static bool IsValidParty(MobileParty party)
        {
            return party != null
                && party.IsActive
                && !party.IsMilitia
                && !party.IsCaravan;
        }
    }

    /* =========================================================
     * PARTY OBSERVATION SNAPSHOT
     * ========================================================= */

    internal sealed class PartyObservation
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

            foreach (Village village in Village.All)
            {
                if (village.MapFaction != party.MapFaction &&
                    party.GetPosition2D.DistanceSquared(
                        village.Settlement.GetPosition2D) < 400f)
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

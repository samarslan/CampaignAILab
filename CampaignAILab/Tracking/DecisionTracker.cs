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
     * Registered   → Intent observed (not yet confirmed)
     * Executing    → Intent persists across time
     * Completed    → Intent fulfilled
     * Aborted      → Intent stopped without replacement
     * Overridden   → Superseded by another decision
     * Invalidated  → Became impossible
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

    public static class DecisionTracker
    {
        /* =========================================================
         * REGISTRY
         * ========================================================= */

        private static readonly Dictionary<string, DecisionRegistryEntry> ActiveDecisions
            = new Dictionary<string, DecisionRegistryEntry>();

        private static readonly Dictionary<string, PartyObservation> LastObservations
            = new Dictionary<string, PartyObservation>();

        internal static DecisionRegistryEntry GetRegistryEntry(string partyId)
            => ActiveDecisions.TryGetValue(partyId, out var e) ? e : null;

        internal static IEnumerable<string> GetAllActivePartyIds()
            => new List<string>(ActiveDecisions.Keys);

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

            string partyId = party.StringId;
            PartyObservation current = PartyObservation.Capture(party);

            if (!LastObservations.TryGetValue(partyId, out PartyObservation previous))
            {
                LastObservations[partyId] = current;
                return;
            }

            // 1️⃣ Infer new decision
            DecisionRecord inferred = InferDecision(previous, current, party);
            if (inferred != null)
            {
                SupersedeDecisionIfExists(party, inferred);

                ActiveDecisions[partyId] = new DecisionRegistryEntry
                {
                    Decision = inferred,
                    Status = DecisionStatus.Registered
                };

                AsyncLogger.EnqueueDecision(inferred);
            }

            // 2️⃣ Registered → Executing (time-gated, prevents instant execution)  ✅ NEW
            if (ActiveDecisions.TryGetValue(partyId, out var active) &&
                active.Status == DecisionStatus.Registered)
            {
                float hoursAlive =
                    (float)(CampaignTime.Now.ToHours - active.Decision.Timestamp.ToHours);

                if (hoursAlive >= 1f) // at least one daily tick persisted  ✅ NEW
                {
                    Transition(active, DecisionStatus.Executing);
                }
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
            if (!prev.InArmy && curr.InArmy && party.Army != null)
            {
                return CreateDecision(party, "JoinArmy",
                    party.Army.LeaderParty?.StringId);
            }

            if (prev.TargetSettlementId != curr.TargetSettlementId &&
                curr.TargetSettlementId != null)
            {
                return CreateDecision(party, "MoveToSettlement",
                    curr.TargetSettlementId);
            }

            if (curr.NearHostileVillage && !prev.NearHostileVillage)
            {
                return CreateDecision(party, "RaidVillage",
                    curr.NearHostileVillageId);
            }

            if (!prev.InSettlement && curr.InSettlement)
            {
                return CreateDecision(party, "EnterSettlement",
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
         * LIFECYCLE CORE
         * ========================================================= */

        // Centralized transition guard  ✅ NEW
        private static void Transition(
            DecisionRegistryEntry entry,
            DecisionStatus next)
        {
            if (IsTerminal(entry.Status) && !IsTerminal(next))
            {
                // Telemetry error, NOT a gameplay outcome  ✅ NEW
                AsyncLogger.EnqueueOutcome(new OutcomeRecord
                {
                    DecisionId = entry.Decision.DecisionId,
                    OutcomeType = "Error",
                    ResolutionTime = CampaignTime.Now
                });
                return;
            }

            entry.Status = next;
        }

        private static bool IsTerminal(DecisionStatus status) // ✅ NEW
        {
            return status == DecisionStatus.Completed
                || status == DecisionStatus.Aborted
                || status == DecisionStatus.Overridden
                || status == DecisionStatus.Invalidated;
        }

        /* =========================================================
         * TERMINAL MARKERS (HOOKS)
         * ========================================================= */

        public static void MarkCompleted(string partyId, string cause) // ✅ NEW
            => MarkTerminal(partyId, DecisionStatus.Completed, cause);

        public static void MarkAborted(string partyId, string cause) // ✅ NEW
            => MarkTerminal(partyId, DecisionStatus.Aborted, cause);

        public static void MarkInvalidated(string partyId, string cause) // ✅ NEW
            => MarkTerminal(partyId, DecisionStatus.Invalidated, cause);

        private static void MarkTerminal(
            string partyId,
            DecisionStatus terminal,
            string cause)
        {
            if (!ActiveDecisions.TryGetValue(partyId, out var entry))
                return;

            float duration =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            Transition(entry, terminal);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = terminal.ToString(),
                ResolutionTime = CampaignTime.Now,
                DurationHours = duration
            });

            ActiveDecisions.Remove(partyId);
        }

        /* =========================================================
         * SUPERSESSION
         * ========================================================= */

        private static void SupersedeDecisionIfExists(
            MobileParty party,
            DecisionRecord newDecision)
        {
            if (!ActiveDecisions.TryGetValue(party.StringId, out var entry))
                return;

            if (IsTerminal(entry.Status))
                return;

            float duration =
                (float)(CampaignTime.Now.ToHours - entry.Decision.Timestamp.ToHours);

            Transition(entry, DecisionStatus.Overridden);

            AsyncLogger.EnqueueOutcome(new OutcomeRecord
            {
                DecisionId = entry.Decision.DecisionId,
                OutcomeType = "Overridden",
                ResolutionTime = CampaignTime.Now,
                DurationHours = duration
            });

            ActiveDecisions.Remove(party.StringId);
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

            // NOTE: O(N) scan — acceptable for logging phase only  ⚠
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

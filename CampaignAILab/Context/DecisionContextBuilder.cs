using CampaignAILab.Decisions;
using CampaignAILab.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;

namespace CampaignAILab.Context
{
    public static class DecisionContextBuilder
    {
        public static DecisionContextSnapshot Build(MobileParty party, Settlement targetSettlement)

        {
            var leader = party.LeaderHero;
            var now = CampaignTime.Now;

            var snapshot = new DecisionContextSnapshot
            {
                /* ----------------------------
                 * PARTY STATE
                 * ---------------------------- */
                TroopCount = party.MemberRoster.TotalManCount,
                TierSum = party.MemberRoster.TotalManCount, // placeholder

                Food = (int)party.Food,
                Morale = (int)party.Morale,
                PartyType = ResolvePartyType(party),
                IsMainParty = party.IsMainParty,
                PartyAgeDays = -1, // placeholder

                CampaignDay = (int)now.ToDays,

                CampaignSeason = ((int)(now.ToDays % 120)) / 30,
                TimeOfDayBucket = ResolveTimeOfDayBucket(now),

                PartySpeed = party.Speed,
                IsAtSettlementAtDecision = party.CurrentSettlement != null,
                TargetDistanceStraightLine = ResolveTargetDistance(party, targetSettlement),

                /* ----------------------------
                 * HERO STATE
                 * ---------------------------- */
                Gold = leader?.Gold ?? 0,

                Aggression = GetTrait(leader, DefaultTraits.Valor),
                Caution = GetTrait(leader, DefaultTraits.Calculating),
                Honor = GetTrait(leader, DefaultTraits.Honor),
                Generosity = GetTrait(leader, DefaultTraits.Generosity),

                /* ----------------------------
                 * WAR CONTEXT
                 * ---------------------------- */
                IsAtWar = IsFactionAtWar(party),
                ActiveWarCount = CountActiveWars(party),

                /* ----------------------------
                 * NEGATIVE / NULL CONTROLS (1.4)
                 * ---------------------------- */
                ContextSchemaVersion = 4,

                PartyIdStringLength = party.StringId?.Length ?? 0,

                NullDeterministicHash =
                ((party.StringId?.GetHashCode() ?? 0) ^ (int)now.ToDays) & 0x7fffffff,

                /* ----------------------------
                * MANDATORY METADATA (1.5)
                * ---------------------------- */
                CampaignAILabAssemblyVersion =
                    VersionProbe.GetCampaignAILabVersion(),

                GameVersionString =
                    VersionProbe.GetNativeGameVersion()
            };

            /* ============================
             * 1.4 TARGET CONTEXT (ANCHOR)
             * ============================ */

            if (targetSettlement != null)
            {
                snapshot.TargetSettlementType =
                    ResolveSettlementType(targetSettlement);

                snapshot.TargetFactionId =
                    targetSettlement.MapFaction?.StringId;

                snapshot.TargetIsFriendly =
                    targetSettlement.MapFaction == party.MapFaction;
            }
            else
            {
                snapshot.TargetSettlementType = 0;
                snapshot.TargetFactionId = null;
                snapshot.TargetIsFriendly = false;
            }
            // populate last; reflection allowed for schema integrity checks
            snapshot.ContextFieldCount =
                typeof(DecisionContextSnapshot).GetFields().Length;

            return snapshot;
        }

        /* =========================================================
         * TRAITS
         * ========================================================= */

        private static int GetTrait(Hero hero, TraitObject trait)
        {
            if (hero == null || trait == null)
                return 0;

            return hero.GetTraitLevel(trait);
        }

        /* =========================================================
         * WAR STATE
         * ========================================================= */

        private static bool IsFactionAtWar(MobileParty party)
        {
            var faction = party.MapFaction;
            if (faction == null)
                return false;

            foreach (var other in Campaign.Current.Factions)
            {
                if (other == faction)
                    continue;

                if (FactionManager.IsAtWarAgainstFaction(faction, other))
                    return true;
            }

            return false;
        }

        private static int CountActiveWars(MobileParty party)
        {
            var faction = party.MapFaction;
            if (faction == null)
                return 0;

            int count = 0;
            foreach (var other in Campaign.Current.Factions)
            {
                if (other == faction)
                    continue;

                if (FactionManager.IsAtWarAgainstFaction(faction, other))
                    count++;
            }

            return count;
        }
        private static string ResolvePartyType(MobileParty party)
        {
            if (party.IsMainParty)
                return "MainParty";

            if (party.LeaderHero != null)
                return "LordParty";

            if (party.IsBandit)
                return "BanditParty";

            return "Other";
        }

        private static byte ResolveTimeOfDayBucket(CampaignTime now)
        {
            // Use ToHours ONLY for coarse bucketing
            var hour = (int)(now.ToHours % 24);

            if (hour >= 6 && hour < 10)
                return 0; // Morning
            if (hour >= 10 && hour < 18)
                return 1; // Day

            return 2; // Night
        }

        private static float ResolveTargetDistance(
            MobileParty party,
            Settlement target)
        {
            if (party == null || target == null)
                return -1f;

            Vec2 partyPos = party.GetPosition2D;
            Vec2 targetPos = target.GetPosition2D;

            return partyPos.Distance(targetPos);
        }
        private static byte ResolveSettlementType(Settlement settlement)
        {
            if (settlement.IsTown)
                return 1;

            if (settlement.IsCastle)
                return 2;

            if (settlement.IsVillage)
                return 3;

            if (settlement.IsHideout)
                return 4;
            
            return 0;
        }
    }
}

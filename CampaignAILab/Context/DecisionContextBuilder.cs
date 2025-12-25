using CampaignAILab.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;

namespace CampaignAILab.Context
{
    public static class DecisionContextBuilder
    {
        public static DecisionContextSnapshot Build(MobileParty party)
        {
            var leader = party.LeaderHero;
            var now = CampaignTime.Now;

            return new DecisionContextSnapshot
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

                // Season derived ONLY from day-of-year to avoid precision leakage
                CampaignSeason = ((int)(now.ToDays % 120)) / 30,

                TimeOfDayBucket = ResolveTimeOfDayBucket(now),

                /* ----------------------------
                 * HERO STATE
                 * ---------------------------- */
                Gold = leader?.Gold ?? 0,

                // Personality traits (mapped)
                Aggression = GetTrait(leader, DefaultTraits.Valor),
                Caution = GetTrait(leader, DefaultTraits.Calculating),
                Honor = GetTrait(leader, DefaultTraits.Honor),
                Generosity = GetTrait(leader, DefaultTraits.Generosity),

                /* ----------------------------
                 * WAR CONTEXT
                 * ---------------------------- */
                IsAtWar = IsFactionAtWar(party),
                ActiveWarCount = CountActiveWars(party)
            };
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
    }
}

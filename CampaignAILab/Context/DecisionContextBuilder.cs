using CampaignAILab.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace CampaignAILab.Context
{
    public static class DecisionContextBuilder
    {
        public static DecisionContextSnapshot Build(MobileParty party)
        {
            var leader = party.LeaderHero;

            return new DecisionContextSnapshot
            {
                /* ----------------------------
                 * PARTY STATE
                 * ---------------------------- */
                TroopCount = party.MemberRoster.TotalManCount,
                TierSum = party.MemberRoster.TotalManCount, // placeholder

                Food = (int)party.Food,
                Morale = (int)party.Morale,

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
    }
}

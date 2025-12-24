using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CampaignAILab
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter starter)
        {
            base.OnGameStart(game, starter);

            if (game.GameType is Campaign)
            {
                ((CampaignGameStarter)starter)
                    .AddBehavior(new Behaviors.CampaignAILabBehavior());
            }
        }
    }
}

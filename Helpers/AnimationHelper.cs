using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using yomusic.Services;

namespace yomusic.Helpers
{
    internal static class AnimationHelper
    {
        public static void SetEntranceTransition(Panel panel, bool stagger = true)
        {
            if (SettingsService.Instance.EnableAnimations)
            {
                panel.ChildrenTransitions = new TransitionCollection
                {
                    new EntranceThemeTransition { IsStaggeringEnabled = stagger }
                };
            }
        }
    }
}

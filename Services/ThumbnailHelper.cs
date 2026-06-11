using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ytmusic_net;

namespace yomusic.Services
{
    public static class ThumbnailHelper
    {
        public static BitmapImage? GetThumbnail(List<Thumbnail>? thumbnails)
        {
            if (thumbnails == null || thumbnails.Count == 0) return null;
            var url = thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault()?.Url;
            return url != null ? new BitmapImage(new Uri(url)) : null;
        }

        public static string GetBestThumbnailUrl(List<Thumbnail>? thumbnails)
        {
            if (thumbnails == null || thumbnails.Count == 0) return "";
            return thumbnails.OrderByDescending(t => t.Width * t.Height).FirstOrDefault()?.Url ?? "";
        }

        public static string? ExtractUrl(BitmapImage? image)
        {
            return image?.UriSource?.ToString();
        }

        public static T? FindParent<T>(DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }
}

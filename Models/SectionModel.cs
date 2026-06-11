using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace yomusic.Models
{
    public class SectionModel : DependencyObject
    {
        public string Title { get; set; } = "";
        public List<ContentItem> Contents { get; set; } = [];

        public void ReleaseImageSources()
        {
            foreach (var item in Contents)
            {
                if (item.Thumbnail is BitmapImage bmp)
                {
                    bmp.UriSource = null;
                }
                item.Thumbnail = null;
            }
        }
    }

    public class ContentItem : DependencyObject
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string ArtistName { get; set; } = "";
        public string? VideoId { get; set; }
        public string? BrowseId { get; set; }
        public string? Duration { get; set; }
        public BitmapImage? Thumbnail { get; set; }
        public string? Year { get; set; }
        public string? AlbumName { get; set; }
    }
}

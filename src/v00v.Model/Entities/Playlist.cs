﻿using System.Collections.Generic;
using Avalonia.Media.Imaging;
using v00v.Model.Enums;
using v00v.Model.Extensions;

namespace v00v.Model.Entities
{
    public class Playlist : ViewModelBase
    {
        #region Fields

        private int _count;
        private bool _isPopularPlaylist;
        private bool _isSearchPlaylist;
        private string _searchText;
        private string _selectedCountry;

        #endregion

        #region Properties

        public virtual Channel Channel { get; set; }

        public virtual List<string> Items { get; } = new();

        public string ChannelId { get; set; }

        public int Count
        {
            get => _count;
            set => Update(ref _count, value);
        }

        public IEnumerable<string> Countries { get; set; }

        public bool EnableGlobalSearch { get; set; }

        public bool HasNew => Count > 0;

        public string Id { get; set; }

        public bool IsPopularPlaylist
        {
            get => _isPopularPlaylist;
            set => Update(ref _isPopularPlaylist, value);
        }

        public bool IsSearchPlaylist
        {
            get => _isSearchPlaylist;
            set => Update(ref _isSearchPlaylist, value);
        }

        public bool IsStatePlaylist { get; set; }

        public int Order { get; set; }

        public string SearchText
        {
            get => _searchText;
            set => Update(ref _searchText, value);
        }

        public string SelectedCountry
        {
            get => _selectedCountry;
            set => Update(ref _selectedCountry, value);
        }

        public WatchState State { get; set; }

        public List<Item> StateItems { get; set; }

        public string SubTitle { get; set; }

        public byte SyncState { get; set; }

        public IBitmap Thumb => Thumbnail.CreateThumb();

        public byte[] Thumbnail { get; set; }

        public string ThumbnailLink { get; set; }

        public int ThumbSize { get; set; } = 30;

        public string Title { get; set; }

        #endregion
    }
}

using System.Linq;
using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers.Profiles
{
    public class PlaylistMapProfile : Profile
    {
        #region Constructors

        public PlaylistMapProfile()
        {
            CreateMap<Playlist, Model.Entities.Playlist>()
                .ForMember(dto => dto.Items, o => o.MapFrom(src => src.Items.Select(x => x.ItemId)))
                .ForMember(dto => dto.Countries, o => o.Ignore()).ForMember(dto => dto.EnableGlobalSearch, o => o.Ignore())
                .ForMember(dto => dto.IsPopularPlaylist, o => o.Ignore()).ForMember(dto => dto.IsSearchPlaylist, o => o.Ignore())
                .ForMember(dto => dto.IsStatePlaylist, o => o.Ignore()).ForMember(dto => dto.Order, o => o.Ignore())
                .ForMember(dto => dto.SearchText, o => o.Ignore()).ForMember(dto => dto.SelectedCountry, o => o.Ignore())
                .ForMember(dto => dto.State, o => o.Ignore()).ForMember(dto => dto.StateItems, o => o.Ignore())
                .ForMember(dto => dto.ThumbSize, o => o.Ignore());

            CreateMap<Model.Entities.Playlist, Playlist>().ForMember(dto => dto.Count, o => o.MapFrom(src => src.Items.Count))
                .ForMember(dto => dto.Channel, o => o.Ignore()).ForMember(dto => dto.Items,
                                                                          o => o.MapFrom(src => src.Items.Select(x => new ItemPlaylist
                                                                          {
                                                                              ItemId = x,
                                                                              PlaylistId = src
                                                                                  .Id
                                                                          })));
        }

        #endregion
    }
}

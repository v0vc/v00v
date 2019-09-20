using System.Linq;
using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers
{
    public class PlaylistMapProfile : Profile
    {
        #region Constructors

        public PlaylistMapProfile()
        {
            CreateMap<Playlist, Model.Entities.Playlist>()
                .ForMember(dto => dto.Items, o => o.MapFrom(src => src.Items.Select(x => x.ItemId)));

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

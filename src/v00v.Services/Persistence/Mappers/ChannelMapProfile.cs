using System.Linq;
using AutoMapper;
using v00v.Model.Extensions;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers
{
    public class ChannelMapProfile : Profile
    {
        #region Constructors

        public ChannelMapProfile()
        {
            CreateMap<Channel, Model.Entities.Channel>().ForMember(dto => dto.Playlists, o => o.MapFrom(src => src.Playlists))
                .ForMember(dto => dto.Items, o => o.MapFrom(src => src.Items))
                .ForMember(dto => dto.SubTitle, o => o.Ignore())
                //.ForMember(dto => dto.Title, o => o.MapFrom(src => src.Title.ArrangeToUi()))
                .ForMember(dto => dto.Tags, o => o.MapFrom(src => src.Tags.Select(x => new Tag { Id = x.TagId })));

            CreateMap<Model.Entities.Channel, Channel>().ForMember(dto => dto.Playlists, o => o.MapFrom(src => src.Playlists))
                .ForMember(dto => dto.Items, o => o.MapFrom(src => src.Items)).ForMember(dto => dto.Site, o => o.Ignore())
                //.ForMember(dto => dto.Title, o => o.MapFrom(src => src.Title.Trim()))
                .ForMember(dto => dto.SiteId, opt => opt.MapFrom(src => 1)).ForMember(dto => dto.Tags,
                                                                                      o => o.MapFrom(src =>
                                                                                                         src.Tags.Select(x =>
                                                                                                                             new
                                                                                                                                 ChannelTag
                                                                                                                                 {
                                                                                                                                     ChannelId
                                                                                                                                         = src
                                                                                                                                             .Id,
                                                                                                                                     TagId
                                                                                                                                         = x
                                                                                                                                             .Id
                                                                                                                                 })));
        }

        #endregion
    }
}

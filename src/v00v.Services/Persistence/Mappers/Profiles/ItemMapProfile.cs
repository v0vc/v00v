using System.Linq;
using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers.Profiles
{
    public class ItemMapProfile : Profile
    {
        #region Constructors

        public ItemMapProfile()
        {
            CreateMap<Item, Model.Entities.Item>().ForMember(dest => dest.Downloaded, opt => opt.MapFrom(src => src.FileName != null))
                .ForMember(dto => dto.ChannelTitle, opt => opt.MapFrom(src => src.Channel.Title))
                .ForMember(dto => dto.Tags, opt => opt.MapFrom(src => src.Channel.Tags.Select(x => x.TagId)))
                .ForMember(dto => dto.Description, o => o.Ignore())
                .ForMember(dto => dto.IsWorking, o => o.Ignore())
                .ForMember(dto => dto.LargeThumb, o => o.Ignore())
                .ForMember(dto => dto.Percentage, o => o.Ignore())
                .ForMember(dto => dto.SaveDir, o => o.Ignore());

            CreateMap<Model.Entities.Item, Item>().ForMember(dto => dto.Channel, o => o.Ignore());
        }

        #endregion
    }
}

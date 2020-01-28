using AutoMapper;
using v00v.Services.Database.Models;

namespace v00v.Services.Persistence.Mappers.Profiles
{
    public class TagMapProfile : Profile
    {
        #region Constructors

        public TagMapProfile()
        {
            CreateMap<Tag, Model.Entities.Tag>().ForMember(dto => dto.IsSaved, opt => opt.MapFrom(src => true))
                .ForMember(dto => dto.IsEditable, o => o.Ignore()).ForMember(dto => dto.IsEnabled, o => o.Ignore())
                .ForMember(dto => dto.IsRemovable, o => o.Ignore()).ForMember(dto => dto.RemoveCommand, o => o.Ignore());

            CreateMap<Model.Entities.Tag, Tag>();
        }

        #endregion
    }
}

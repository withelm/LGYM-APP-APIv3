using LgymApi.Api.DTOs;
using LgymApi.Api.Services;
using LgymApi.Application.Mapping.Core;
using LgymApi.Domain.Entities;

namespace LgymApi.Api.Mapping.Profiles;

public sealed class ExerciseProfile : IMappingProfile
{
    private const string TranslationsContextKey = "translations";

    public void Configure(MappingConfiguration configuration)
    {
        configuration.CreateMap<Exercise, ExerciseResponseDto>((source, context) =>
        {
            var name = source.Name;

            if (source.UserId is null)
            {
                var translations = context?.Get<IReadOnlyDictionary<Guid, string>>(TranslationsContextKey);
                if (translations != null && translations.TryGetValue(source.Id, out var translatedName))
                {
                    name = translatedName;
                }
            }

            return new ExerciseResponseDto
            {
                Id = source.Id.ToString(),
                Name = name,
                BodyPart = source.BodyPart.ToLookup(),
                Description = source.Description,
                Image = source.Image,
                UserId = source.UserId?.ToString()
            };
        });
    }
}

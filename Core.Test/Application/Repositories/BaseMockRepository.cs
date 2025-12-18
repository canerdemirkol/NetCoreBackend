using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetCoreBackend.NArchitecture.Core.Application.Rules;
using NetCoreBackend.NArchitecture.Core.Localization.Resource.Yaml;
using NetCoreBackend.NArchitecture.Core.Persistence.Repositories;
using NetCoreBackend.NArchitecture.Core.Test.Application.FakeData;
using NetCoreBackend.NArchitecture.Core.Test.Application.Helpers;

namespace NetCoreBackend.NArchitecture.Core.Test.Application.Repositories;

public abstract class BaseMockRepository<TRepository, TEntity, TEntityId, TMappingProfile, TBusinessRules, TFakeData>
    where TEntity : Entity<TEntityId>, new()
    where TRepository : class, IAsyncRepository<TEntity, TEntityId>, IRepository<TEntity, TEntityId>
    where TMappingProfile : Profile, new()
    where TBusinessRules : BaseBusinessRules
    where TFakeData : BaseFakeData<TEntity, TEntityId>, new()
{
    public IMapper Mapper;
    public Mock<TRepository> MockRepository;
    public TBusinessRules BusinessRules;

    protected BaseMockRepository(TFakeData fakeData)
    {
        Mapper = CreateMapper();
        MockRepository = MockRepositoryHelper.GetRepository<TRepository, TEntity, TEntityId>(fakeData.Data);
        BusinessRules = CreateBusinessRules();
    }

    private static IMapper CreateMapper()
    {
        var configExpression = new MapperConfigurationExpression();
        configExpression.AddProfile<TMappingProfile>();

        var configuration = new MapperConfiguration(configExpression, NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();

        return configuration.CreateMapper();
    }

    private TBusinessRules CreateBusinessRules()
    {
        var localizationManager = new ResourceLocalizationManager(resources: [])
        {
            AcceptLocales = new[] { "en" }
        };

        return (TBusinessRules)
                Activator.CreateInstance(
                    type: typeof(TBusinessRules),
                    MockRepository.Object,
                    localizationManager
                )! ?? throw new InvalidOperationException($"Cannot create an instance of {typeof(TBusinessRules).FullName}.");

    }
}

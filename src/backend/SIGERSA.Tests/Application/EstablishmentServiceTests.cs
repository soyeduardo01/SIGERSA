using FluentValidation;
using SIGERSA.Application.Establishments;
using SIGERSA.Domain.Entities;
using SIGERSA.Domain.Exceptions;
using SIGERSA.Domain.Repositories;

namespace SIGERSA.Tests.Application;

public sealed class EstablishmentServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task CreateNormalizesThePersistedDraft()
    {
        var repository = new FakeRepository();
        var service = new EstablishmentService(repository, new EstablishmentRequestValidator());

        await service.CreateAsync(ValidRequest() with
        {
            Code = "CODIGO-IGNORADO",
            Name = "  Planta Norte  ",
            Email = " CONTACTO@EXAMPLE.COM "
        }, ActorId, CancellationToken.None);

        Assert.NotNull(repository.LastDraft);
        Assert.Empty(repository.LastDraft.Code);
        Assert.Equal("Planta Norte", repository.LastDraft.Name);
        Assert.Equal("contacto@example.com", repository.LastDraft.Email);
    }

    [Fact]
    public async Task CreateRejectsDependentSamplingValueWithoutAPlan()
    {
        var repository = new FakeRepository();
        var service = new EstablishmentService(repository, new EstablishmentRequestValidator());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            ValidRequest() with
            {
                MicrobiologicalSamplingPlan = false,
                SamplingApplicationCode = "MATERIAS_PRIMAS"
            }, ActorId, CancellationToken.None));
    }

    [Fact]
    public async Task CreateConvertsDatesToUtcAndNormalizesMaskedContactIdentification()
    {
        var repository = new FakeRepository();
        var service = new EstablishmentService(repository, new EstablishmentRequestValidator());
        var localDate = new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.FromHours(-4));

        await service.CreateAsync(ValidRequest() with
        {
            OperationsStartDate = localDate,
            SanitaryPermitExpiresAt = localDate.AddYears(1),
            Contacts = [new EstablishmentContactDraft(
                "PRINCIPAL", "Propietario", "001-0000000-1", "809-555-1234", null)]
        }, ActorId, CancellationToken.None);

        Assert.Equal(TimeSpan.Zero, repository.LastDraft!.OperationsStartDate!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, repository.LastDraft.SanitaryPermitExpiresAt!.Value.Offset);
        Assert.Equal("00100000001", repository.LastDraft.Contacts.Single().Identification);
    }

    [Fact]
    public async Task UpdateRequiresAndProtectsTheRowVersion()
    {
        var repository = new FakeRepository { AllowUpdate = false };
        var service = new EstablishmentService(repository, new EstablishmentRequestValidator());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(
            Guid.NewGuid(), ValidRequest(), ActorId, CancellationToken.None));
        await Assert.ThrowsAsync<OptimisticConcurrencyException>(() => service.UpdateAsync(
            Guid.NewGuid(), ValidRequest() with { RowVersion = 2 }, ActorId, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRequiresEveryRiskInput()
    {
        var service = new EstablishmentService(new FakeRepository(), new EstablishmentRequestValidator());
        var incomplete = ValidRequest() with
        {
            AnnualProduction = null,
            HaccpImplemented = null,
            MicrobiologicalSamplingPlan = null,
            IsInabieSupplier = null,
            Products = []
        };

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(incomplete, ActorId, CancellationToken.None));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(EstablishmentRequest.AnnualProduction));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(EstablishmentRequest.HaccpImplemented));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(EstablishmentRequest.MicrobiologicalSamplingPlan));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(EstablishmentRequest.IsInabieSupplier));
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(EstablishmentRequest.Products));
    }

    private static EstablishmentRequest ValidRequest() => new(
        CompanyId: CompanyId,
        MunicipalityId: null,
        DpsDasId: null,
        CommercializationId: null,
        Code: "EST-001",
        Name: "Planta Norte",
        Street: null,
        AddressNumber: null,
        Phone: null,
        Email: null,
        OperationsStartDate: null,
        SanitaryPermitNumber: null,
        SanitaryPermitExpiresAt: null,
        ProductsDescription: null,
        AnnualProduction: 0,
        FemaleEmployees: 0,
        MaleEmployees: 0,
        MicrobiologicalRejectionsLastFiveYears: 0,
        HaccpImplemented: false,
        HaccpPercentage: null,
        MicrobiologicalSamplingPlan: false,
        SamplingApplicationCode: null,
        IsInabieSupplier: false,
        InabieDistributionCode: null,
        Status: "ACTIVO",
        MarketIds: [],
        Contacts: [],
        Products: [new EstablishmentProductDraft(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Producto", 1, "kg")],
        RowVersion: null);

    private sealed class FakeRepository : IEstablishmentRepository
    {
        public EstablishmentDraft? LastDraft { get; private set; }
        public bool AllowUpdate { get; init; } = true;

        public Task<EstablishmentsPage> SearchAsync(EstablishmentSearch search, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EstablishmentsPage([], search.Page, search.PageSize, 0));

        public Task<EstablishmentDetails?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<EstablishmentDetails?>(null);

        public Task<EstablishmentOptions> GetOptionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid> CreateAsync(EstablishmentDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<bool> UpdateAsync(Guid id, EstablishmentDraft draft, Guid actorId, CancellationToken cancellationToken = default)
        {
            LastDraft = draft;
            return Task.FromResult(AllowUpdate);
        }
    }
}

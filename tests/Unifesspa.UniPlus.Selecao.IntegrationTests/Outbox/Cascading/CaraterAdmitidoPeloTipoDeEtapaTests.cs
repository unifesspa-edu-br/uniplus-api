namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Kernel.Results;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

/// <summary>
/// O caráter declarado numa etapa é conferido contra o que o tipo admite no cadastro de
/// Configuração, atravessando a cadeia inteira: a carga inicial do catálogo, a projeção do
/// reader cross-módulo e a recusa do agregado.
/// </summary>
/// <remarks>
/// Passa pelo bus real, então o reader resolve contra o Postgres do Testcontainers — é o que
/// distingue este teste do unitário do handler, que mocka a vista do cadastro e por isso não
/// prova nem o valor semeado nem a projeção das colunas.
/// </remarks>
[Collection(CascadingCollection.Name)]
public sealed class CaraterAdmitidoPeloTipoDeEtapaTests
{
    /// <summary>Ids do seed determinístico do catálogo (carga inicial de tipos de etapa).</summary>
    private static readonly Guid AnaliseDocumental = new("019fee1e-7000-7000-8000-000000000006");
    private static readonly Guid ProvaObjetiva = new("019fee1e-7000-7000-8000-000000000001");

    private readonly CascadingFixture _fixture;

    public CaraterAdmitidoPeloTipoDeEtapaTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Etapa classificatória num tipo que não compõe a nota final é recusada")]
    public async Task EtapaClassificatoriaEmTipoQueNaoPontua_Recusa()
    {
        Guid processoId = await SemearProcessoAsync(nameof(EtapaClassificatoriaEmTipoQueNaoPontua_Recusa));

        Result<MutacaoAceita> resultado = await DefinirEtapaAsync(
            processoId, AnaliseDocumental, CaraterEtapa.Classificatoria, peso: 1m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("etapas[0].carater");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcesso.CaraterNaoAdmitidoPeloTipo);
        resultado.Errors[0].Error.Message.Should().Contain("Análise Documental",
            "a recusa nomeia o tipo, senão não diz qual dos dois campos revisar");
    }

    /// <summary>
    /// O mesmo tipo aceita caráter eliminatório: ele elimina, só não pontua. A prova precisa de
    /// duas etapas porque o certame exige ao menos uma compondo a nota final — e é a configuração
    /// que as planilhas do CEPS descrevem, com a análise documental ao lado da prova.
    /// </summary>
    [Fact(DisplayName = "Etapa eliminatória no tipo que não pontua é aceita ao lado de uma que compõe a nota")]
    public async Task EtapaEliminatoriaEmTipoQueNaoPontua_Aceita()
    {
        Guid processoId = await SemearProcessoAsync(nameof(EtapaEliminatoriaEmTipoQueNaoPontua_Aceita));

        Result<MutacaoAceita> resultado = await DefinirEtapasAsync(
            processoId,
            new EtapaProcessoInput("Prova", CaraterEtapa.Classificatoria, ProvaObjetiva, 1m, null, 1),
            new EtapaProcessoInput("Documentos", CaraterEtapa.Eliminatoria, AnaliseDocumental, null, 5m, 2));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo que compõe a nota final aceita etapa classificatória com peso")]
    public async Task EtapaClassificatoriaEmTipoQuePontua_Aceita()
    {
        Guid processoId = await SemearProcessoAsync(nameof(EtapaClassificatoriaEmTipoQuePontua_Aceita));

        Result<MutacaoAceita> resultado = await DefinirEtapaAsync(
            processoId, ProvaObjetiva, CaraterEtapa.Classificatoria, peso: 1m);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    private async Task<Guid> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private Task<Result<MutacaoAceita>> DefinirEtapaAsync(
        Guid processoId,
        Guid tipoEtapaOrigemId,
        CaraterEtapa carater,
        decimal? peso,
        decimal? notaMinima = null) =>
        DefinirEtapasAsync(
            processoId,
            new EtapaProcessoInput("Etapa", carater, tipoEtapaOrigemId, peso, notaMinima, 1));

    private async Task<Result<MutacaoAceita>> DefinirEtapasAsync(
        Guid processoId,
        params EtapaProcessoInput[] etapas)
    {
        DefinirEtapasCommand command = new(processoId, etapas, PrecondicaoIfMatch.Ausente);

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<MutacaoAceita>>(command);
    }
}

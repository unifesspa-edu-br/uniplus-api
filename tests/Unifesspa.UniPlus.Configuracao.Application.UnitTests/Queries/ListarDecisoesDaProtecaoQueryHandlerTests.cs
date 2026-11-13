namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// A lista das decisões do Representante do controlador confere os critérios antes de ler e entrega o registro sem a
/// identidade de quem agiu.
/// </summary>
public sealed class ListarDecisoesDaProtecaoQueryHandlerTests
{
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();

    public ListarDecisoesDaProtecaoQueryHandlerTests() =>
        _atos.ListarDecisoesAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(
            ((IReadOnlyList<AtoDaProtecaoDoCatalogo>)[], ((string, Guid)?)null, ((string, Guid)?)null));

    private static ListarDecisoesDaProtecaoQuery Consulta(
        string[]? atos = null, string? cadastro = null, DateOnly? de = null, DateOnly? ate = null, string? busca = null, bool minhas = false) =>
        new(atos ?? [], cadastro, de, ate, busca, minhas, null, null, 20, PaginationDirection.Next);

    private Task<Result<ListarDecisoesDaProtecaoResult>> ListarAsync(ListarDecisoesDaProtecaoQuery query) =>
        ListarDecisoesDaProtecaoQueryHandler.Handle(query, _atos, UsuarioIsabella(), CancellationToken.None);

    [Fact(DisplayName = "Os critérios chegam ao repositório já analisados")]
    public async Task Handle_RepassaOFiltroAnalisado()
    {
        await ListarAsync(Consulta(["REPROVACAO", "AJUSTE_RECOMENDADO"], "FATO", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), "renda"));

        await _atos.Received(1).ListarDecisoesAsync(
            Arg.Is<FiltroDasDecisoesDaProtecao>(f =>
                f.Atos.SequenceEqual(new[] { AtoDaProtecao.Reprovacao, AtoDaProtecao.AjusteRecomendado })
                && f.Cadastro == CadastroDoCatalogo.Fato
                && f.De == new DateOnly(2026, 11, 1)
                && f.Ate == new DateOnly(2026, 11, 2)
                && f.Busca == "renda"
                && f.Autor == null),
            null, null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Só as minhas restringe ao emissor e ao sub de quem consulta")]
    public async Task Handle_Minhas_RestringeAoAutor()
    {
        await ListarAsync(Consulta(minhas: true));

        await _atos.Received(1).ListarDecisoesAsync(
            Arg.Is<FiltroDasDecisoesDaProtecao>(f => f.Autor != null && f.Autor.Value.Emissor == Emissor && f.Autor.Value.Sub == Isabella),
            null, null, 20, PaginationDirection.Next, Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A solicitação, o aceite e o token fora do vocabulário não são decisões")]
    [InlineData("SOLICITACAO")]
    [InlineData("ACEITE")]
    [InlineData("TALVEZ")]
    public async Task Handle_AtoQueNaoEDecisao_Recusa(string ato)
    {
        Result<ListarDecisoesDaProtecaoResult> resultado = await ListarAsync(Consulta([ato]));

        resultado.Errors.Should().ContainSingle(e => e.Field == "ato").Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.AtoInvalido);
        await _atos.DidNotReceiveWithAnyArgs().ListarDecisoesAsync(default!, default, default, default, default, default);
    }

    [Fact(DisplayName = "O cadastro fora do vocabulário, o período invertido e a busca longa são recusados juntos")]
    public async Task Handle_CriteriosInvalidos_AcumulaRecusas()
    {
        Result<ListarDecisoesDaProtecaoResult> resultado = await ListarAsync(
            Consulta(cadastro: "MODALIDADE", de: new DateOnly(2026, 11, 2), ate: new DateOnly(2026, 11, 1), busca: new string('a', 201)));

        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo("cadastro", "de", "busca");
        resultado.Errors.Single(e => e.Field == "cadastro").Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.CadastroInvalido);
        resultado.Errors.Single(e => e.Field == "de").Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.PeriodoInvalido);
    }

    [Fact(DisplayName = "A decisão sai com o item e o nome de quem decidiu, sem o emissor nem o sub")]
    public async Task Handle_MapeiaADecisao()
    {
        TipoDocumento tipo = NovoTipo();
        AtoDaProtecaoDoCatalogo ato = AtoDaProtecaoDoCatalogo.RegistrarDecisao(
            Retrato(tipo), new DecisaoFundamentada(AtoDaProtecao.Reprovacao, null, Fundamentacao.Analisar("Dado desnecessário.")),
            new AtorDoAto(Emissor, "betina.neves", "Betina Marina Valentina das Neves"), Agora);
        _atos.ListarDecisoesAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(
            ([ato], ((string, Guid)?)null, ((string, Guid)?)("proximo", ato.Id)));

        Result<ListarDecisoesDaProtecaoResult> resultado = await ListarAsync(Consulta());

        resultado.Value!.Items.Should().ContainSingle().Which.Should().Be(new DecisaoDaProtecaoDto(
            "TIPO_DOCUMENTO", tipo.Id, "LAUDO_MEDICO_COMPLEMENTAR", "Laudo médico complementar", "REPROVACAO", Agora, "Betina Marina Valentina das Neves", 1,
            "INTERNO", "CUMPRIMENTO_OBRIGACAO_LEGAL", Finalidade, null, "Dado desnecessário.", false));
        resultado.Value.Proximo.Should().Be(("proximo", ato.Id));
        typeof(DecisaoDaProtecaoDto).GetProperties().Select(p => p.Name).Should().NotContain(["AtorSub", "AtorEmissor", "Sub", "Emissor"]);
    }
}

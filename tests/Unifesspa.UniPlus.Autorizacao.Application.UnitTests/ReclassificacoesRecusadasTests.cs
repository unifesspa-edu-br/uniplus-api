namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class ReclassificacoesRecusadasTests
{
    private const string ConsultarVersoes = "autorizacao:perfis:consultar-versoes";

    [Fact(DisplayName = "A fila de reclassificações recusa o token sem autor antes de consultar")]
    public async Task Fila_SemAutor_RecusaPeloToken()
    {
        IReclassificacaoRepository repositorio = Substitute.For<IReclassificacaoRepository>();

        IVerificadorDeAcesso verificador = SemAutor();

        Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>> fila = await ListarFila("gestao_de_acessos", verificador, repositorio);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        repositorio.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Theory(DisplayName = "A fila de reclassificações recusa quem não tem a permissão da etapa antes de consultar")]
    [InlineData("gestao_de_acessos", Gestao, Protecao)]
    [InlineData("protecao_de_dados", Protecao, Gestao)]
    public async Task Fila_SemPermissao_RecusaPelaEtapa(string etapa, string permissaoDaEtapa, string permissaoDeOutraEtapa)
    {
        IReclassificacaoRepository repositorio = Substitute.For<IReclassificacaoRepository>();
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [permissaoDeOutraEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>> fila = await ListarFila(etapa, verificador, repositorio);

        fila.Errors.Single().Error.Code.Should().Be(ReclassificacaoErrorCodes.SemPermissaoNaEtapa);
        repositorio.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila de reclassificações é listada a quem tem a permissão da etapa")]
    [InlineData("gestao_de_acessos", Gestao)]
    [InlineData("protecao_de_dados", Protecao)]
    public async Task Fila_ComPermissao_Lista(string etapa, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [permissaoDaEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>> fila = await ListarFila(etapa, verificador, new ReclassificacoesNaMemoria());

        fila.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "A fila de reclassificações recusa a etapa inválida sem avaliar o acesso nem consultar")]
    public async Task Fila_EtapaInvalida_RecusaSemAvaliarNemConsultar()
    {
        IReclassificacaoRepository repositorio = Substitute.For<IReclassificacaoRepository>();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();

        Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>> fila = await ListarFila("outra", verificador, repositorio);

        fila.Errors.Single().Error.Code.Should().Be(ReclassificacaoErrorCodes.EtapaInvalida);
        repositorio.ReceivedCalls().Should().BeEmpty();
        verificador.ReceivedCalls().Should().BeEmpty();
    }

    [Fact(DisplayName = "As divergências da governança recusam o token sem autor antes de consultar")]
    public async Task Divergencias_SemAutor_RecusaPeloToken()
    {
        IReclassificacaoRepository repositorio = Substitute.For<IReclassificacaoRepository>();

        IVerificadorDeAcesso verificador = SemAutor();

        Result<IReadOnlyList<DivergenciaDaGovernancaDto>> divergencias = await ListarDivergencias(verificador, repositorio);

        divergencias.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        repositorio.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    [Fact(DisplayName = "As divergências da governança recusam quem não mantém perfis, não aprova pela proteção de dados nem consulta as versões")]
    public async Task Divergencias_SemPermissao_Recusa()
    {
        IReclassificacaoRepository repositorio = Substitute.For<IReclassificacaoRepository>();

        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Gestao]);

        Result<IReadOnlyList<DivergenciaDaGovernancaDto>> divergencias = await ListarDivergencias(verificador, repositorio);

        divergencias.Errors.Single().Error.Code.Should().Be(ReclassificacaoErrorCodes.SemPermissaoNaEtapa);
        repositorio.ReceivedCalls().Should().BeEmpty();
        TrilhaDeAcesso.Do(verificador).Should().BeEquivalentTo([NaOperacao(Manter, ResultadoDoAcesso.Negado), NaOperacao(Protecao, ResultadoDoAcesso.Negado), NaOperacao(ConsultarVersoes, ResultadoDoAcesso.Negado)]);
    }

    [Fact(DisplayName = "As divergências da governança são listadas a quem aprova pela proteção de dados")]
    public async Task Divergencias_ComPermissao_Lista()
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.Sub, [Protecao]);

        Result<IReadOnlyList<DivergenciaDaGovernancaDto>> divergencias = await ListarDivergencias(verificador, new ReclassificacoesNaMemoria());

        divergencias.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(Protecao, ResultadoDoAcesso.Permitido));
    }

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestor.Sub, permissao, ResourceTipos.Operacao, null, resultado);

    private static IVerificadorDeAcesso SemAutor()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));
        return verificador;
    }

    private static Task<Result<IReadOnlyList<ItemDaFilaDeReclassificacoesDto>>> ListarFila(
        string etapa, IVerificadorDeAcesso verificador, IReclassificacaoRepository repositorio) =>
        ListarReclassificacoesQueryHandler.Handle(
            new ListarReclassificacoesQuery(etapa),
            verificador,
            new FilaDeReclassificacoes(repositorio, new ConcessoesDaReclassificacao(repositorio), TimeProvider.System),
            CancellationToken.None);

    private static Task<Result<IReadOnlyList<DivergenciaDaGovernancaDto>>> ListarDivergencias(
        IVerificadorDeAcesso verificador, IReclassificacaoRepository repositorio) =>
        ListarDivergenciasDaGovernancaQueryHandler.Handle(new ListarDivergenciasDaGovernancaQuery(), verificador, repositorio, CancellationToken.None);
}

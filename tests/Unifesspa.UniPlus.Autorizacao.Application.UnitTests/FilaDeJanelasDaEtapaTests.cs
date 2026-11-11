namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Autorizacao.Application.UnitTests.CenarioDaGovernancaDaVersao;

public sealed class FilaDeJanelasDaEtapaTests
{
    private const string EtapaGestao = "gestao_de_acessos";
    private const string EtapaProtecao = "protecao_de_dados";

    [Theory(DisplayName = "A fila recusa quem não tem a permissão da etapa sem consultar janela, concessão nem perfil")]
    [InlineData(EtapaGestao, Protecao)]
    [InlineData(EtapaProtecao, Gestao)]
    public async Task SemPermissaoDaEtapa_RecusaAntesDeQualquerLeitura(string etapa, string permissaoDeOutraEtapa)
    {
        IJanelaDeMigracaoRepository janelas = EspiaoDeConsultas<IJanelaDeMigracaoRepository>.Criar(out Func<int> consultasAJanelas);
        IPerfilRepository perfis = EspiaoDeConsultas<IPerfilRepository>.Criar(out Func<int> consultasAPerfis);

        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> fila = await ListarFilaDeJanelasQueryHandler.Handle(
            new ListarFilaDeJanelasQuery(etapa),
            new VerificadorDeTeste(PerfisDeTeste.Gestora.Sub, [permissaoDeOutraEtapa]),
            new FilaDeJanelasDaEtapa(janelas, perfis, new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestora),
            CancellationToken.None);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        consultasAJanelas().Should().Be(0);
        consultasAPerfis().Should().Be(0);
    }

    [Fact(DisplayName = "A etapa inválida é recusada sem consultar nada")]
    public async Task EtapaInvalida_RecusaSemConsultar()
    {
        IJanelaDeMigracaoRepository janelas = EspiaoDeConsultas<IJanelaDeMigracaoRepository>.Criar(out Func<int> consultas);

        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> fila = await ListarFilaDeJanelasQueryHandler.Handle(
            new ListarFilaDeJanelasQuery("outra"),
            new VerificadorDeTeste(PerfisDeTeste.Gestora.Sub, [Gestao]),
            new FilaDeJanelasDaEtapa(janelas, new DoublesDePerfis.PerfisNaMemoria(), new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestora),
            CancellationToken.None);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
        consultas().Should().Be(0);
    }

    [Theory(DisplayName = "A fila de janelas recusada registra a negativa da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao, Protecao)]
    [InlineData(EtapaProtecao, Protecao, Gestao)]
    public async Task SemPermissaoDaEtapa_RegistraANegativaDaEtapa(string etapa, string permissaoDaEtapa, string deOutraEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestora.Sub, [deOutraEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> fila = await Listar(etapa, verificador);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila de janelas permitida registra a decisão da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao)]
    [InlineData(EtapaProtecao, Protecao)]
    public async Task ComPermissaoDaEtapa_RegistraAPermitida(string etapa, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestora.Sub, [permissaoDaEtapa]);

        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> fila = await Listar(etapa, verificador);

        fila.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Permitido));
    }

    [Theory(DisplayName = "A fila de janelas recusa o token sem autor sem consultar janela nem perfil e sem registrar")]
    [InlineData(EtapaGestao)]
    [InlineData(EtapaProtecao)]
    public async Task SemAutor_RecusaPeloTokenSemConsultarNemRegistrar(string etapa)
    {
        IJanelaDeMigracaoRepository janelas = Substitute.For<IJanelaDeMigracaoRepository>();
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = SemAutor();

        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> fila = await Listar(etapa, verificador, janelas, perfis);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        janelas.ReceivedCalls().Should().BeEmpty();
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private static IVerificadorDeAcesso SemAutor()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));
        return verificador;
    }

    private static Task<Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>> Listar(
        string etapa, IVerificadorDeAcesso verificador, IJanelaDeMigracaoRepository janelas, IPerfilRepository perfis) =>
        ListarFilaDeJanelasQueryHandler.Handle(
            new ListarFilaDeJanelasQuery(etapa),
            verificador,
            new FilaDeJanelasDaEtapa(janelas, perfis, new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestora),
            CancellationToken.None);

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestora.Sub, permissao, ResourceTipos.Operacao, null, resultado);

    private static Task<Result<IReadOnlyList<ItemDaFilaDeJanelasDto>>> Listar(string etapa, VerificadorDeTeste verificador) =>
        ListarFilaDeJanelasQueryHandler.Handle(
            new ListarFilaDeJanelasQuery(etapa),
            verificador,
            new FilaDeJanelasDaEtapa(new DoublesDePerfis.JanelasNaMemoria(), new DoublesDePerfis.PerfisNaMemoria(), new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestora),
            CancellationToken.None);

    /// <summary>Repositório substituto que conta as chamadas e devolve o valor padrão do retorno.</summary>
    public class EspiaoDeConsultas<T> : DispatchProxy
        where T : class
    {
        private int _chamadas;

        public static T Criar(out Func<int> chamadas)
        {
            T proxy = Create<T, EspiaoDeConsultas<T>>();
            EspiaoDeConsultas<T> espiao = (EspiaoDeConsultas<T>)(object)proxy;
            chamadas = () => espiao._chamadas;

            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _chamadas++;

            throw new InvalidOperationException($"{targetMethod?.Name} não deveria ser consultado.");
        }
    }
}

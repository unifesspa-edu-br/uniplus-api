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

public sealed class FilaDeRevisoesDasConcessoesDaEtapaTests
{
    private const string EtapaGestao = "gestao_de_acessos";
    private const string EtapaProtecao = "protecao_de_dados";

    [Theory(DisplayName = "A fila recusa quem não tem a permissão da etapa sem consultar revisão, concessão nem perfil")]
    [InlineData(EtapaGestao, Protecao)]
    [InlineData(EtapaProtecao, Gestao)]
    public async Task SemPermissaoDaEtapa_RecusaAntesDeQualquerLeitura(string etapa, string permissaoDeOutraEtapa)
    {
        IRevisaoDasConcessoesRepository revisoes = EspiaoDeConsultas<IRevisaoDasConcessoesRepository>.Criar(out Func<int> consultasARevisoes);
        IPerfilRepository perfis = EspiaoDeConsultas<IPerfilRepository>.Criar(out Func<int> consultasAPerfis);

        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> fila = await ListarFilaDeRevisoesDasConcessoesQueryHandler.Handle(
            new ListarFilaDeRevisoesDasConcessoesQuery(etapa),
            new VerificadorDeTeste(PerfisDeTeste.Gestor.ContaId, [permissaoDeOutraEtapa]),
            new FilaDeRevisoesDasConcessoesDaEtapa(revisoes, perfis, new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestor),
            CancellationToken.None);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        consultasARevisoes().Should().Be(0);
        consultasAPerfis().Should().Be(0);
    }

    [Fact(DisplayName = "A etapa inválida é recusada sem consultar nada")]
    public async Task EtapaInvalida_RecusaSemConsultar()
    {
        IRevisaoDasConcessoesRepository revisoes = EspiaoDeConsultas<IRevisaoDasConcessoesRepository>.Criar(out Func<int> consultas);

        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> fila = await ListarFilaDeRevisoesDasConcessoesQueryHandler.Handle(
            new ListarFilaDeRevisoesDasConcessoesQuery("outra"),
            new VerificadorDeTeste(PerfisDeTeste.Gestor.ContaId, [Gestao]),
            new FilaDeRevisoesDasConcessoesDaEtapa(revisoes, new DoublesDePerfis.PerfisNaMemoria(), new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestor),
            CancellationToken.None);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.EtapaInvalida);
        consultas().Should().Be(0);
    }

    [Theory(DisplayName = "A fila de revisões recusada registra a negativa da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao, Protecao)]
    [InlineData(EtapaProtecao, Protecao, Gestao)]
    public async Task SemPermissaoDaEtapa_RegistraANegativaDaEtapa(string etapa, string permissaoDaEtapa, string deOutraEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [deOutraEtapa]);

        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> fila = await Listar(etapa, verificador);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.SemPermissaoNaEtapa);
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Negado));
    }

    [Theory(DisplayName = "A fila de revisões permitida registra a decisão da permissão da etapa na operação")]
    [InlineData(EtapaGestao, Gestao)]
    [InlineData(EtapaProtecao, Protecao)]
    public async Task ComPermissaoDaEtapa_RegistraAPermitida(string etapa, string permissaoDaEtapa)
    {
        VerificadorDeTeste verificador = new(PerfisDeTeste.Gestor.ContaId, [permissaoDaEtapa]);

        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> fila = await Listar(etapa, verificador);

        fila.IsSuccess.Should().BeTrue();
        TrilhaDeAcesso.Do(verificador).Should().Equal(NaOperacao(permissaoDaEtapa, ResultadoDoAcesso.Permitido));
    }

    [Theory(DisplayName = "A fila de revisões recusa o token sem autor sem consultar revisão nem perfil e sem registrar")]
    [InlineData(EtapaGestao)]
    [InlineData(EtapaProtecao)]
    public async Task SemAutor_RecusaPeloTokenSemConsultarNemRegistrar(string etapa)
    {
        IRevisaoDasConcessoesRepository revisoes = Substitute.For<IRevisaoDasConcessoesRepository>();
        IPerfilRepository perfis = Substitute.For<IPerfilRepository>();
        IVerificadorDeAcesso verificador = SemAutor();

        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> fila = await Listar(etapa, verificador, revisoes, perfis);

        fila.Errors.Single().Error.Code.Should().Be(PerfilErrorCodes.AutorNaoIdentificado);
        revisoes.ReceivedCalls().Should().BeEmpty();
        perfis.ReceivedCalls().Should().BeEmpty();
        await verificador.DidNotReceiveWithAnyArgs().RegistrarDesfechoAsync(default!, default, default);
    }

    private static IVerificadorDeAcesso SemAutor()
    {
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.AvaliarAsync(default!, default, default).ReturnsForAnyArgs(Task.FromResult<DecisaoDeAcesso>(SemSujeito.Instancia));
        return verificador;
    }

    private static Task<Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>> Listar(
        string etapa, IVerificadorDeAcesso verificador, IRevisaoDasConcessoesRepository revisoes, IPerfilRepository perfis) =>
        ListarFilaDeRevisoesDasConcessoesQueryHandler.Handle(
            new ListarFilaDeRevisoesDasConcessoesQuery(etapa),
            verificador,
            new FilaDeRevisoesDasConcessoesDaEtapa(revisoes, perfis, new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestor),
            CancellationToken.None);

    private static TrilhaDeAcesso NaOperacao(string permissao, ResultadoDoAcesso resultado) =>
        new(PerfisDeTeste.Gestor.ContaId, permissao, ResourceTipos.Operacao, null, resultado);

    private static Task<Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>> Listar(string etapa, VerificadorDeTeste verificador) =>
        ListarFilaDeRevisoesDasConcessoesQueryHandler.Handle(
            new ListarFilaDeRevisoesDasConcessoesQuery(etapa),
            verificador,
            new FilaDeRevisoesDasConcessoesDaEtapa(new DoublesDePerfis.RevisoesNaMemoria(), new DoublesDePerfis.PerfisNaMemoria(), new RelogioFixo(PerfisDeTeste.Instante)),
            new DoublesDePerfis.Usuario(PerfisDeTeste.Gestor),
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

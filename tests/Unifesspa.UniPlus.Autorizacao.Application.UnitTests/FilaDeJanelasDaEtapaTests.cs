namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using System.Reflection;

using AwesomeAssertions;

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

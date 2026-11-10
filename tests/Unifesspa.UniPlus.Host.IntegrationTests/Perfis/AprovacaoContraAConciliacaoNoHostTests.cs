namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A aprovação que põe uma versão em vigor contra a conciliação do catálogo novo, em transações reais e nas duas ordens.
/// A instância que aprova é a anterior à troca, que segue atendendo com o catálogo antigo gravado no banco; a conciliação
/// aplica o catálogo da instância nova. Se a aprovação confirma antes, a conciliação vê a versão em uso e grava a
/// reclassificação. Se a conciliação confirma antes, a aprovação que esperava a trava confere o catálogo depois dela e
/// falha fechada com 503, sem gravar nada; a instância nova aprova em seguida e a entrada em vigor grava a reclassificação.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class AprovacaoContraAConciliacaoNoHostTests : ReclassificacaoNoHostTestBase, IClassFixture<HostComCatalogoAnterior>
{
    private readonly HostComCatalogoAnterior _hostDaClasse;
    private bool _instanciaAnterior;

    public AprovacaoContraAConciliacaoNoHostTests(MonolitoDaInstalacaoFixture fixture, HostComCatalogoAnterior hostDaClasse)
        : base(fixture)
    {
        _hostDaClasse = hostDaClasse;
    }

    private WebApplicationFactory<HostAssemblyMarker> Anterior =>
        _hostDaClasse.Obter(Fixture, CatalogoAlternativoDeTeste.Com(Real, Sensivel, ClassificacaoDeProtecao.Interno));

    private protected override HttpClient NovoCliente() => _instanciaAnterior ? Anterior.CreateClient() : base.NovoCliente();

    [Fact(DisplayName = "Primeira vigência confirmada antes da conciliação: a conciliação vê a versão em uso e grava a reclassificação")]
    public async Task PrimeiraVigencia_AprovacaoPrimeiro()
    {
        Envio envio = await PrimeiraVersaoVerificadaAsync();

        (HttpStatusCode status, string corpo) = await AprovarEmFilaAsync(envio, aprovacaoPrimeiro: true);

        status.Should().Be(HttpStatusCode.OK, corpo);
        await ConferirAsync(envio.PerfilId);
    }

    [Fact(DisplayName = "Conciliação confirmada antes da primeira vigência: a aprovação da instância anterior falha fechada e a instância nova põe em vigor e grava a reclassificação")]
    public async Task PrimeiraVigencia_ConciliacaoPrimeiro()
    {
        Envio envio = await PrimeiraVersaoVerificadaAsync();

        (HttpStatusCode status, string corpo) = await AprovarEmFilaAsync(envio, aprovacaoPrimeiro: false);

        status.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        (await ReclassificacoesAsync(envio.PerfilId)).Should().Be(0);
        await AprovarPelaInstanciaNovaAsync(envio);
        await ConferirAsync(envio.PerfilId);
    }

    [Fact(DisplayName = "Aprovação final confirmada antes da conciliação: a conciliação vê a versão em uso e grava a reclassificação")]
    public async Task AprovacaoFinal_AprovacaoPrimeiro()
    {
        Envio nova = await VersaoPendenteVerificadaAsync();

        (HttpStatusCode status, string corpo) = await AprovarEmFilaAsync(nova, aprovacaoPrimeiro: true);

        status.Should().Be(HttpStatusCode.OK, corpo);
        await ConferirAsync(nova.PerfilId);
    }

    [Fact(DisplayName = "Conciliação confirmada antes da aprovação final: a aprovação da instância anterior falha fechada e a instância nova grava a reclassificação na entrada em vigor")]
    public async Task AprovacaoFinal_ConciliacaoPrimeiro()
    {
        Envio nova = await VersaoPendenteVerificadaAsync();

        (HttpStatusCode status, string corpo) = await AprovarEmFilaAsync(nova, aprovacaoPrimeiro: false);

        status.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        (await ReclassificacoesAsync(nova.PerfilId)).Should().Be(0);
        await AprovarPelaInstanciaNovaAsync(nova);
        await ConferirAsync(nova.PerfilId);
    }

    [Theory(DisplayName = "O lote da janela de migração e a conciliação, em qualquer ordem, deixam a concessão migrada com a permissão suspensa")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoteDaJanela_ContraAConciliacao(bool lotePrimeiro)
    {
        (Envio envio, Guid concessao) = await JanelaAbertaAsync();
        using HttpClient gestao = await GestaoAsync();
        (HttpStatusCode Status, string Corpo) lote = default;
        Func<Task> migrar = async () => lote = await LerRespostaAsync(EnviarAsync(
            gestao, HttpMethod.Post, $"{RotaPerfis}/{envio.PerfilId}/janela/lote",
            new { decisoes = new[] { new { concessaoId = concessao, decisao = "migrar" } }, confirmacaoDeExclusao = false }));

        await EmFilaAsync(envio.PerfilId, Ordenar(migrar, lotePrimeiro));

        _instanciaAnterior = false;
        lote.Status.Should().Be(HttpStatusCode.OK, lote.Corpo);
        (await ReclassificacoesAsync(envio.PerfilId)).Should().Be(1);
        (await OrigemDaBaseAsync(concessao, Consultar)).Should().Be("migracao");
        (await PermissoesAsync(Leandro, envio.PerfilId)).Should().Equal(Consultar);
    }

    [Fact(DisplayName = "A instância com catálogo diferente do banco recusa a aprovação final com 503, e depois da conciliação ela aprova")]
    public async Task InstanciaAntiga_RecusaComServicoIndisponivel()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
        using HttpClient gestao = await GestaoAsync();
        await AprovarAsync(gestao, nova, EtapaGestao);
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);
        using HttpClient protecao = await ProtecaoAsync();

        (HttpStatusCode status, string corpo) = await LerRespostaAsync(AprovarBrutoAsync(protecao, nova, EtapaProtecao));
        await ImplantarRealAsync();
        await AprovarAsync(protecao, nova, EtapaProtecao);

        status.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        (await TemVigenteComVersaoAsync(gestor, vigente.PerfilId, nova.VersaoId)).Should().BeTrue();
    }

    private async Task<Envio> PrimeiraVersaoVerificadaAsync()
    {
        await EntrarNaInstanciaAnteriorAsync();
        using HttpClient gestor = await GestorAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar, Sensivel);
        await VerificarAsync(gestor, envio);

        return envio;
    }

    private async Task<Envio> VersaoPendenteVerificadaAsync()
    {
        await EntrarNaInstanciaAnteriorAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);

        return await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
    }

    // Com o catálogo anterior no banco a permissão ainda não é protegida, e a gestão de acessos decide sozinha a versão.
    private async Task<(HttpStatusCode Status, string Corpo)> AprovarEmFilaAsync(Envio envio, bool aprovacaoPrimeiro)
    {
        using HttpClient gestao = await GestaoAsync();
        (HttpStatusCode Status, string Corpo) aprovacao = default;
        Func<Task> aprovar = async () => aprovacao = await LerRespostaAsync(AprovarBrutoAsync(gestao, envio, EtapaGestao));

        await EmFilaAsync(envio.PerfilId, Ordenar(aprovar, aprovacaoPrimeiro));

        _instanciaAnterior = false;

        return aprovacao;
    }

    // Com a permissão já protegida no catálogo novo, a versão exige a proteção de dados e as aprovações dadas são descartadas.
    private async Task AprovarPelaInstanciaNovaAsync(Envio envio)
    {
        using HttpClient gestao = await GestaoAsync();
        using HttpResponseMessage descartadas = await AprovarBrutoAsync(gestao, envio, EtapaGestao);
        await ConferirRecusaDoPerfilAsync(descartadas, HttpStatusCode.Conflict, "aprovacoes_descartadas");
        await AprovarAsync(gestao, envio, EtapaGestao);
        using HttpClient protecao = await ProtecaoAsync();
        await AprovarAsync(protecao, envio, EtapaProtecao);
    }

    // A operação e a conciliação na ordem em que o banco as enfileira atrás da trava do perfil.
    private Func<Task>[] Ordenar(Func<Task> operacao, bool operacaoPrimeiro) =>
        operacaoPrimeiro ? [operacao, ImplantarRealAsync] : [ImplantarRealAsync, operacao];

    private async Task ConferirAsync(Guid perfil)
    {
        Guid reclassificacao = await ReclassificacaoAsync(perfil, Sensivel);
        (await ReclassificacoesAsync(perfil)).Should().Be(1);
        (await ConsultarTextoAsync(
            """SELECT classificacao_anterior || '>' || classificacao_nova AS "Value" FROM autorizacao.reclassificacoes_da_permissao WHERE id = {0}""",
            reclassificacao)).Should().Be("interno>sensivel");
        (await BasesDaOrigemAsync(perfil, "conciliacao")).Should().Be(0);
        Guid unidade = await SemearUnidadeAsync();
        await ConcederDepoisAsync(new Cenario(perfil, unidade, new Dictionary<Persona, Guid>()), Betina);
        (await PermissoesAsync(Betina, perfil)).Should().Equal(Consultar);
        (await FilaDaProtecaoAsync()).Should().Contain(reclassificacao.ToString());
    }

    private async Task EntrarNaInstanciaAnteriorAsync()
    {
        _ = Anterior.Services;
        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);
        _instanciaAnterior = true;
    }

    private async Task VerificarAsync(HttpClient criador, Envio envio)
    {
        await ConfirmarEnvioAsync(criador, envio);
        await InjetarResultadoAsync(envio.DocumentoId, VeredictoDaVerificacao.Verificado);
        await AguardarAsync(async () => await SituacaoDoDocumentoAsync(criador, envio.PerfilId) == "verificado", "o documento não foi verificado");
    }

    private async Task<(Envio Envio, Guid Concessao)> JanelaAbertaAsync()
    {
        await EntrarNaInstanciaAnteriorAsync();
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Guid unidade = await SemearUnidadeAsync();
        Guid concessao = await ConcederDepoisAsync(new Cenario(vigente.PerfilId, unidade, new Dictionary<Persona, Guid>()), Leandro);
        Envio nova = await CriarVersaoAsync(gestor, vigente.PerfilId, Nome(), Consultar, Sensivel);
        await EnviarEVerificarAsync(gestor, nova);

        return (vigente, concessao);
    }
}

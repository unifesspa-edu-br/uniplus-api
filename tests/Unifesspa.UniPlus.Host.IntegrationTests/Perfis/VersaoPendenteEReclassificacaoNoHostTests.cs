namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A reclassificação com versões e perfis em andamento pelo host real: a versão pendente que entra em vigor depois da
/// troca de catálogo, a versão de remoção, a governança, que só ganha divergência, e a retomada do perfil suspenso.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class VersaoPendenteEReclassificacaoNoHostTests : ReclassificacaoNoHostTestBase
{
    public VersaoPendenteEReclassificacaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A versão criada antes da troca de catálogo e posta em vigor depois grava a reclassificação e suspende a permissão")]
    public async Task VersaoPendente_EntraEmVigorComAReclassificacao()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);
        await RebaixarRetratoAsync(vigente.PerfilId, 2, Sensivel, "interno");

        await AprovarEPorEmVigorAsync(gestor, nova);

        (await ReclassificacoesAsync(vigente.PerfilId)).Should().Be(1);
        await ConcessoesDeTeste.ConcederAsync(
            Fixture.Factory.Services, Betina.Sub.ToString(), vigente.PerfilId, EscopoDaConcessao.DaUnidade(await SemearUnidadeAsync()), email: Betina.Email);
        (await PermissoesAsync(Betina, vigente.PerfilId)).Should().Equal(Consultar);
        (await FilaDaProtecaoAsync()).Should().Contain((await ReclassificacaoAsync(vigente.PerfilId, Sensivel)).ToString());
    }

    [Fact(DisplayName = "A versão de remoção depois da troca de catálogo copia o retrato antigo e grava a reclassificação ao entrar em vigor")]
    public async Task VersaoDeRemocao_GravaAReclassificacao()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await RebaixarRetratoAsync(perfil.Perfil, 1, Sensivel, "interno");
        using HttpClient gestor = await GestorAsync();

        using HttpResponseMessage remocao = await RemoverBrutoAsync(gestor, perfil.Perfil, [Consultar]);

        remocao.StatusCode.Should().Be(HttpStatusCode.OK, await remocao.Content.ReadAsStringAsync());
        (await ReclassificacoesAsync(perfil.Perfil)).Should().Be(1);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A versão com o retrato igual ao catálogo entra em vigor sem gravar reclassificação")]
    public async Task VersaoComRetratoAtual_NaoGravaReclassificacao()
    {
        using HttpClient gestor = await GestorAsync();
        Envio vigente = await PerfilVigenteAsync(gestor, Consultar);
        Envio nova = await PendenteVerificadaAsync(gestor, vigente.PerfilId, Consultar, Sensivel);

        await AprovarEPorEmVigorAsync(gestor, nova);

        (await ReclassificacoesAsync(vigente.PerfilId)).Should().Be(0);
    }

    [Fact(DisplayName = "A governança nunca é suspensa pela reclassificação: gera divergência e aviso e a pessoa mantém a permissão")]
    public async Task Governanca_SoGeraDivergenciaEAviso()
    {
        using HttpClient protecao = await ProtecaoAsync();
        long divergencias = await DivergenciasAsync();
        long avisos = await AvisosDeDivergenciaAsync();

        await SubirAClasseAsync(AprovarProtecaoDeDados);
        await ProcessarAvisosAsync();

        (await DivergenciasAsync()).Should().BeGreaterThan(divergencias);
        (await AvisosDeDivergenciaAsync()).Should().BeGreaterThan(avisos);
        (await PermissoesAsync(Marlene, PerfisDeSistema.RepresentanteDoControladorId)).Should().Contain(AprovarProtecaoDeDados);
    }

    [Fact(DisplayName = "A reclassificação de permissão dos perfis de governança não grava reclassificação nem base para eles")]
    public async Task Governanca_NaoGravaReclassificacaoNemBase()
    {
        using HttpClient protecao = await ProtecaoAsync();
        using HttpClient gestor = await GestorAsync();
        string codigo = CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Codigo;
        long divergencias = await DivergenciasDeClasseAsync(codigo);

        await SubirAClasseAsync(codigo);

        (await DivergenciasDeClasseAsync(codigo)).Should().BeGreaterThan(divergencias, "a troca de catálogo precisa ter sido processada");
        foreach (Guid perfil in new[] { PerfisDeSistema.GestorDeAcessosId, PerfisDeSistema.RepresentanteDoControladorId })
        {
            (await ReclassificacoesAsync(perfil)).Should().Be(0);
            (await BasesAsync(perfil)).Should().Be(0);
        }
    }

    [Fact(DisplayName = "O perfil suspenso é retomado pela reclassificação quando a classe muda e as etapas decididas bastam")]
    public async Task Retomada_PelaReclassificacao()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        using HttpClient gestao = await GestaoAsync();
        using HttpClient outraGestao = await ClienteComPermissoesAsync(Betina, AprovarFuncional);
        await ConferirAsync(await ChamadasDaContencao.SuspenderAsync(gestao, perfil.Perfil), HttpStatusCode.NoContent);
        await ConferirAsync(await ChamadasDaContencao.RetomarAsync(outraGestao, perfil.Perfil, EtapaGestao), HttpStatusCode.OK);
        (await PermissoesAsync(Leandro, perfil.Perfil)).Should().BeEmpty();

        await ImplantarAsync(Sensivel, ClassificacaoDeProtecao.Interno);

        (await OrigemDaRetomadaAsync(perfil.Perfil)).Should().Be(OrigemDaRetomadaCodigo.Reclassificacao);
    }

    private static async Task ConferirAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        using (resposta)
        {
            resposta.StatusCode.Should().Be(esperado, await resposta.Content.ReadAsStringAsync());
        }
    }

    private async Task AprovarEPorEmVigorAsync(HttpClient gestor, Envio envio)
    {
        using HttpResponseMessage resposta = await AprovarUltimaEtapaBrutoAsync(gestor, envio);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        if (!await TemVigenteComVersaoAsync(gestor, envio.PerfilId, envio.VersaoId))
        {
            await AprovarAteAVigenciaAsync(gestor, envio);
        }
    }

    private Task<long> BasesAsync(Guid perfil) =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.bases_da_permissao_na_concessao WHERE perfil_id = {0}""", perfil);

    private Task<long> DivergenciasDeClasseAsync(string codigo) =>
        ContarAsync(
            """SELECT count(*) AS "Value" FROM autorizacao.divergencias_da_governanca WHERE codigo = {0} AND tipo = {1}""",
            codigo,
            TipoDeDivergenciaCodigo.Classificacao);

    private Task<long> DivergenciasAsync() =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.divergencias_da_governanca""");

    private Task<long> AvisosDeDivergenciaAsync() =>
        ContarAsync("""SELECT count(*) AS "Value" FROM autorizacao.avisos_da_reclassificacao WHERE origem = 'divergencia'""");

    private Task<string?> OrigemDaRetomadaAsync(Guid perfil) =>
        ConsultarTextoAsync(
            """SELECT origem AS "Value" FROM autorizacao.eventos_da_contencao WHERE perfil_id = {0} AND tipo = 'retomada' ORDER BY ordem_na_trilha DESC LIMIT 1""",
            perfil);
}

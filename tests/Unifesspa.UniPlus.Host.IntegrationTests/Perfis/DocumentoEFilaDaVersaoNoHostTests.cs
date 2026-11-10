namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>A leitura do documento pela proteção de dados, a fila de cada etapa e a idempotência da decisão.</summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class DocumentoEFilaDaVersaoNoHostTests : AprovacaoDaVersaoNoHostTestBase
{
    private const string RotaDaFila = "/api/autorizacao/versoes-de-perfil";

    public DocumentoEFilaDaVersaoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A proteção de dados lê o documento da versão pendente, a leitura entra na trilha, e quem não tem permissão recebe 403")]
    public async Task ProtecaoDeDados_LeODocumentoComTrilha()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient marlene = await ProtecaoAsync();
        using HttpClient leandro = Cliente(Leandro);
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);
        string rota = $"{RotaPerfis}/{versao.PerfilId}/versoes/{versao.VersaoId}/documento";

        using HttpResponseMessage lida = await marlene.GetAsync(new Uri(rota, UriKind.Relative));
        using HttpResponseMessage negada = await leandro.GetAsync(new Uri(rota, UriKind.Relative));

        lida.StatusCode.Should().Be(HttpStatusCode.OK, await lida.Content.ReadAsStringAsync());
        negada.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LeiturasAsync(versao.DocumentoId)).Should().Be(1);
    }

    [Fact(DisplayName = "Cada fila lista as versões que aguardam a sua etapa, e a vencida não aparece")]
    public async Task Fila_ListaPorEtapaEEscondeAVencida()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient marlene = await ProtecaoAsync();
        Envio protegido = await PendenteDeUmPerfilNovoAsync(diogo, Consultar, Sensivel);
        Envio vencido = await PendenteDeUmPerfilNovoAsync(diogo, Consultar, Sensivel);
        await ExecutarSqlAsync("UPDATE autorizacao.versoes_do_perfil SET expira_em = now() - interval '1 second' WHERE id = {0}", vencido.VersaoId);

        (await FilaAsync(isabella, EtapaGestao)).Should().Contain(protegido.PerfilId).And.NotContain(vencido.PerfilId);
        (await FilaAsync(marlene, EtapaProtecao)).Should().NotContain(protegido.PerfilId);
        await AprovarAsync(isabella, protegido, EtapaGestao);

        (await FilaAsync(marlene, EtapaProtecao)).Should().Contain(protegido.PerfilId);
        (await FilaAsync(isabella, EtapaGestao)).Should().NotContain(protegido.PerfilId);
    }

    [Fact(DisplayName = "A fila e o perfil dizem só ao autor que a versão pendente é dele")]
    public async Task Fila_ApontaAVersaoProposta_SoParaOAutor()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio proposta = await PendenteDeUmPerfilNovoAsync(diogo, Consultar);

        JsonElement daFilaDoAutor = await ItemDaFilaAsync(diogo, proposta.PerfilId);
        JsonElement daFilaDeOutraPessoa = await ItemDaFilaAsync(isabella, proposta.PerfilId);

        daFilaDoAutor.GetProperty("propostaPorQuemLe").GetBoolean().Should().BeTrue();
        daFilaDoAutor.GetProperty("versao").GetProperty("propostaPorQuemLe").GetBoolean().Should().BeTrue();
        daFilaDeOutraPessoa.GetProperty("propostaPorQuemLe").GetBoolean().Should().BeFalse();
        daFilaDeOutraPessoa.GetProperty("versao").GetProperty("propostaPorQuemLe").GetBoolean().Should().BeFalse();
        (await PerfilAsync(diogo, proposta.PerfilId)).GetProperty("versaoPendente").GetProperty("propostaPorQuemLe").GetBoolean().Should().BeTrue();
        (await PerfilAsync(isabella, proposta.PerfilId)).GetProperty("versaoPendente").GetProperty("propostaPorQuemLe").GetBoolean().Should().BeFalse();
        daFilaDoAutor.GetRawText().Should().NotContain(Diogo.Sub.ToString());
    }

    [Fact(DisplayName = "A fila recusa quem não tem a permissão da etapa e a etapa que não existe")]
    public async Task Fila_RecusaSemPermissaoEEtapaInvalida()
    {
        await ResetarAsync();
        using HttpClient isabella = await GestaoAsync();
        using HttpClient leandro = Cliente(Leandro);

        using HttpResponseMessage semPermissao = await leandro.GetAsync(new Uri($"{RotaDaFila}?fila={EtapaGestao}", UriKind.Relative));
        using HttpResponseMessage invalida = await isabella.GetAsync(new Uri($"{RotaDaFila}?fila=outra", UriKind.Relative));

        await ConferirRecusaDoPerfilAsync(semPermissao, HttpStatusCode.Forbidden, "sem_permissao_na_etapa");
        await ConferirRecusaDoPerfilAsync(invalida, HttpStatusCode.UnprocessableEntity, "etapa_invalida");
    }

    [Fact(DisplayName = "Repetir a aprovação com a mesma chave devolve a mesma resposta e grava uma decisão só")]
    public async Task Aprovacao_ReplayNaoDuplica()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar, Sensivel);
        string chave = Guid.NewGuid().ToString();

        using HttpResponseMessage primeira = await AprovarBrutoAsync(isabella, versao, EtapaGestao, chave);
        using HttpResponseMessage repeticao = await AprovarBrutoAsync(isabella, versao, EtapaGestao, chave);

        repeticao.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
        (await CorpoAsync(repeticao, HttpStatusCode.OK)).GetRawText().Should().Be((await CorpoAsync(primeira, HttpStatusCode.OK)).GetRawText());
        (await DecisoesAsync(versao.VersaoId)).Should().ContainSingle();
    }

    [Fact(DisplayName = "Recusar de novo a etapa já recusada recebe conflito e não duplica a decisão")]
    public async Task Recusa_SegundaVezRecebeConflito()
    {
        await ResetarAsync();
        using HttpClient diogo = await GestorAsync();
        using HttpClient isabella = await GestaoAsync();
        Envio vigente = await PerfilVigenteAsync(diogo, Consultar);
        Envio versao = await PendenteVerificadaAsync(diogo, vigente.PerfilId, Consultar);

        using HttpResponseMessage primeira = await RecusarBrutoAsync(isabella, versao, EtapaGestao);
        using HttpResponseMessage segunda = await RecusarBrutoAsync(isabella, versao, EtapaGestao);

        primeira.StatusCode.Should().Be(HttpStatusCode.OK);
        await ConferirRecusaDoPerfilAsync(segunda, HttpStatusCode.Conflict, "etapa_ja_decidida");
        (await DecisoesAsync(versao.VersaoId)).Should().ContainSingle();
    }

    private async Task<Envio> PendenteDeUmPerfilNovoAsync(HttpClient criador, params string[] permissoes)
    {
        Envio vigente = await PerfilVigenteAsync(criador, Consultar);

        return await PendenteVerificadaAsync(criador, vigente.PerfilId, permissoes);
    }

    private static async Task<JsonElement> ItemDaFilaAsync(HttpClient cliente, Guid perfilId)
    {
        JsonElement fila = await LerAsync(cliente, $"{RotaDaFila}?fila={EtapaGestao}");

        return fila.EnumerateArray().Single(i => i.GetProperty("perfilId").GetGuid() == perfilId);
    }

    private static async Task<Guid[]> FilaAsync(HttpClient cliente, string etapa)
    {
        JsonElement fila = await LerAsync(cliente, $"{RotaDaFila}?fila={etapa}");

        return [.. fila.EnumerateArray().Select(i => i.GetProperty("perfilId").GetGuid())];
    }
}

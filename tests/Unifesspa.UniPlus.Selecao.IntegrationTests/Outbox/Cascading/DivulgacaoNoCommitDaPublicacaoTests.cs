namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using Application.Commands.ProcessosSeletivos;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Wolverine;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// A divulgação do certame nasce no mesmo commit da versão e do ato, e a cópia ao acervo, efeito posterior, é o que
/// registra o endereço dos documentos. O acervo de teste fica indisponível enquanto o cenário quer ver a divulgação
/// antes da cópia.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class DivulgacaoNoCommitDaPublicacaoTests
{
    private readonly CascadingFixture _fixture;

    public DivulgacaoNoCommitDaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Publicar grava a versão e a divulgação no mesmo commit, com o instante do ato, e a divulgação já existe quando a resposta volta")]
    public async Task Publicar_GravaAVersaoEADivulgacaoNoMesmoCommit()
    {
        CascadingApiFactory api = _fixture.Factory;
        using AcervoIndisponivel acervoFora = AcervoIndisponivel.Ligar(api);
        using ProcessoPublicado publicado = await PublicarNovoProcessoAsync(api);

        (VersaoConfiguracao versao, AtoDoProcesso ato, CertameDivulgado divulgado) = await LerAsync(api, publicado.ProcessoId);

        divulgado.AtoCriadorId.Should().Be(versao.AtoCriadorId);
        divulgado.DivulgadoEm.Should().Be(versao.VigenteAPartirDe).And.Be(ato.PublicadoEm);
        (await TransacaoDaVersaoAsync(api, publicado.ProcessoId)).Should().Be(
            await TransacaoDaDivulgacaoAsync(api, publicado.ProcessoId), "a versão e a divulgação foram gravadas pela mesma transação");
        divulgado.AtosDivulgados.Should().ContainSingle().Which.Should().Be(AtoDivulgado.SemEndereco(versao.AtoCriadorId, 1));
    }

    [Fact(DisplayName = "Retificar avança a divulgação no mesmo commit da versão seguinte, com a entrada do novo ato sem endereço")]
    public async Task Retificar_AvancaADivulgacaoNoMesmoCommit()
    {
        CascadingApiFactory api = _fixture.Factory;
        using AcervoIndisponivel acervoFora = AcervoIndisponivel.Ligar(api);
        using ProcessoPublicado publicado = await PublicarNovoProcessoAsync(api);

        await RetificarSemCopiarAsync(api, publicado);

        CertameDivulgado divulgado = await LerDivulgacaoAsync(api, publicado.ProcessoId);
        VersaoConfiguracao segunda = await LerVersaoAsync(api, publicado.ProcessoId, numero: 2);
        divulgado.NumeroVersao.Should().Be(2);
        divulgado.AtoCriadorId.Should().Be(segunda.AtoCriadorId);
        divulgado.DivulgadoEm.Should().Be(segunda.VigenteAPartirDe);
        divulgado.AtosDivulgados.Should().HaveCount(2).And.OnlyContain(e => e.UrlDownload == null);
    }

    [Fact(DisplayName = "Identificador legível em uso recusa a publicação com 409, sem versão, ato, divulgação nem envelope, e preserva o rascunho")]
    public async Task IdentificadorLegivelEmUso_Recusa409SemResiduo()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, nameof(IdentificadorLegivelEmUso_Recusa409SemResiduo));
        string identificador = await IdentificadorDoProcessoAsync(api, processoId);
        await DivulgarOutroProcessoComOIdentificadorAsync(api, identificador);
        await SemearRascunhoAsync(api, processoId);

        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.identificador_legivel_em_uso");
        await ConferirSemResiduoAsync(api, processoId);
    }

    [Fact(DisplayName = "Com o acervo indisponível nada é registrado, a mensagem de cópia fica reagendada, e quando o acervo volta o endereço chega e o selo da resposta muda")]
    public async Task AcervoIndisponivel_NadaRegistrado_MensagemReagendada_EnderecoChegaDepois()
    {
        CascadingApiFactory api = _fixture.Factory;
        AcervoPublicoEmMemoria acervo = AcervoDaFabrica(api);
        using AcervoIndisponivel acervoFora = AcervoIndisponivel.Ligar(api);
        using ProcessoPublicado publicado = await PublicarNovoProcessoAsync(api);
        await AguardarFalhasAsync(acervo, 1);

        string selo = await ConferirEntradaSemEnderecoAsync(api, publicado);

        acervoFora.Restabelecer();
        await ReentregarACopiaAsync(api, publicado);
        await ConferirEnderecoComNovoSeloAsync(api, publicado, selo);
    }

    [Fact(DisplayName = "A cópia da versão anterior depois da seguinte preenche só a entrada dela e não recua a divulgação")]
    public async Task CopiaDaVersaoAnterior_DepoisDaSeguinte_PreencheSoAEntradaDela()
    {
        CascadingApiFactory api = _fixture.Factory;
        AcervoPublicoEmMemoria acervo = AcervoDaFabrica(api);
        using AcervoIndisponivel acervoFora = AcervoIndisponivel.Ligar(api);
        using ProcessoPublicado publicado = await PublicarNovoProcessoAsync(api);
        await RetificarSemCopiarAsync(api, publicado);
        await AguardarFalhasAsync(acervo, 2);
        acervoFora.Restabelecer();

        await CopiarAoAcervoAsync(api, publicado.ProcessoId, publicado.AtoId);

        await ConferirSoAAberturaCopiadaAsync(api, publicado);
    }

    private static AcervoPublicoEmMemoria AcervoDaFabrica(CascadingApiFactory api) =>
        (AcervoPublicoEmMemoria)api.Services.GetRequiredService<Selecao.Application.Abstractions.IAcervoPublico>();

    private static async Task<ProcessoPublicado> PublicarNovoProcessoAsync(CascadingApiFactory api)
    {
        HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(api, $"divulgacao-no-commit-{Guid.CreateVersion7()}");
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return new ProcessoPublicado(processoId, await ObterAtoIdAsync(api, processoId), client);
    }

    private static async Task RetificarSemCopiarAsync(CascadingApiFactory api, ProcessoPublicado publicado)
    {
        Guid documento = await SemearDocumentoConfirmadoAsync(api, publicado.ProcessoId);

        (await RetificarAsync(publicado.Client, publicado.ProcessoId, documento)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<string> ConferirEntradaSemEnderecoAsync(CascadingApiFactory api, ProcessoPublicado publicado)
    {
        (string selo, JsonElement corpo) = await LerCertameAsync(publicado.Client, publicado.ProcessoId);

        corpo.GetProperty("documentoEdital").GetProperty("urlDownload").ValueKind.Should().Be(JsonValueKind.Null);
        (await LerDivulgacaoAsync(api, publicado.ProcessoId)).CopiouOsDocumentosDoAto(publicado.AtoId).Should().BeFalse();
        (await MensagensDaCopiaNaFilaAsync(api, publicado.AtoId)).Should().Be(1, "a cópia falhou e foi reagendada, não perdida nem enviada à fila morta");
        (await MensagensDaCopiaNaFilaMortaAsync(api, publicado.AtoId)).Should().Be(0);

        return selo;
    }

    private static async Task ReentregarACopiaAsync(CascadingApiFactory api, ProcessoPublicado publicado)
    {
        await CopiarAoAcervoAsync(api, publicado.ProcessoId, publicado.AtoId);
        await CopiarAoAcervoAsync(api, publicado.ProcessoId, publicado.AtoId);
    }

    private static async Task ConferirEnderecoComNovoSeloAsync(CascadingApiFactory api, ProcessoPublicado publicado, string seloAnterior)
    {
        (await LerDivulgacaoAsync(api, publicado.ProcessoId)).AtosDivulgados.Should().ContainSingle(
            "a reentrega da cópia encontra a entrada preenchida e não cria outra");
        (string selo, JsonElement corpo) = await LerCertameAsync(publicado.Client, publicado.ProcessoId);

        selo.Should().NotBe(seloAnterior);
        corpo.GetProperty("documentoEdital").GetProperty("urlDownload").GetString().Should().NotBeNullOrEmpty();
    }

    private static async Task ConferirSoAAberturaCopiadaAsync(CascadingApiFactory api, ProcessoPublicado publicado)
    {
        Guid retificacao = (await LerVersaoAsync(api, publicado.ProcessoId, numero: 2)).AtoCriadorId;
        CertameDivulgado divulgado = await LerDivulgacaoAsync(api, publicado.ProcessoId);

        divulgado.CopiouOsDocumentosDoAto(publicado.AtoId).Should().BeTrue();
        divulgado.CopiouOsDocumentosDoAto(retificacao).Should().BeFalse("a cópia da versão anterior não preenche a entrada da seguinte");
        divulgado.NumeroVersao.Should().Be(2);
        divulgado.AtoCriadorId.Should().Be(retificacao);
    }

    private static async Task AguardarFalhasAsync(AcervoPublicoEmMemoria acervo, int esperadas)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (acervo.Falhas < esperadas)
        {
            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a cópia ao acervo deveria ter sido tentada e falhado");
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    private static async Task CopiarAoAcervoAsync(CascadingApiFactory api, Guid processoId, Guid atoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CopiarDocumentosDoAtoAoAcervo(processoId, atoId));
    }

    private static async Task<(string Selo, JsonElement Corpo)> LerCertameAsync(HttpClient client, Guid processoId)
    {
        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative), CancellationToken.None);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        string selo = resposta.Headers.ETag!.Tag;
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return (selo, documento.RootElement.Clone());
    }

    private static async Task<(VersaoConfiguracao Versao, AtoDoProcesso Ato, CertameDivulgado Divulgado)> LerAsync(
        CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == processoId);
        AtoDoProcesso ato = await db.AtosDoProcesso.AsNoTracking().SingleAsync(a => a.ProcessoSeletivoId == processoId);
        CertameDivulgado divulgado = await db.CertamesDivulgados.AsNoTracking().SingleAsync(c => c.Id == processoId);

        return (versao, ato, divulgado);
    }

    private static async Task<CertameDivulgado> LerDivulgacaoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().CertamesDivulgados.AsNoTracking()
            .SingleAsync(c => c.Id == processoId);
    }

    private static async Task<VersaoConfiguracao> LerVersaoAsync(CascadingApiFactory api, Guid processoId, int numero)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().VersoesConfiguracao.AsNoTracking()
            .SingleAsync(v => v.ProcessoSeletivoId == processoId && v.NumeroVersao == numero);
    }

    private static Task<long> TransacaoDaVersaoAsync(CascadingApiFactory api, Guid processoId) =>
        ConsultarAsync(api, db => db.Database
            .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.versoes_configuracao WHERE processo_seletivo_id = {processoId}")
            .SingleAsync());

    private static Task<long> TransacaoDaDivulgacaoAsync(CascadingApiFactory api, Guid processoId) =>
        ConsultarAsync(api, db => db.Database
            .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM selecao.certames_divulgados WHERE id = {processoId}")
            .SingleAsync());

    private static Task<string> IdentificadorDoProcessoAsync(CascadingApiFactory api, Guid processoId) =>
        ConsultarAsync(api, async db => (await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == processoId)).IdentificadorLegivel!.Value.Valor);

    private static async Task DivulgarOutroProcessoComOIdentificadorAsync(CascadingApiFactory api, string identificador)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        db.CertamesDivulgados.Add(CertameDivulgado.Criar(
            Guid.CreateVersion7(), 1, Guid.CreateVersion7(), new string('a', 64), "1",
            new FacetasDoCertameDivulgado(identificador, "Outro certame", "001/2026", ["AC"], agora.AddDays(-1), agora.AddDays(10)),
            """{"nome":"documento"}""", agora));
        await db.SaveChangesAsync();
    }

    private static async Task SemearRascunhoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.RascunhosDePublicacao.Add(RascunhoDePublicacao.Criar(
            processoId, "operador-do-rascunho", """{"ato":{"assinante":"Fulano de Tal"}}""", 1, DateTimeOffset.UtcNow, RascunhoDePublicacao.Prazo).Value!);
        await db.SaveChangesAsync();
    }

    private static async Task ConferirSemResiduoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (await db.VersoesConfiguracao.AsNoTracking().AnyAsync(v => v.ProcessoSeletivoId == processoId)).Should().BeFalse();
        (await db.AtosDoProcesso.AsNoTracking().AnyAsync(a => a.ProcessoSeletivoId == processoId)).Should().BeFalse();
        (await db.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processoId)).Should().BeFalse();
        (await db.RascunhosDePublicacao.AsNoTracking().AnyAsync(r => r.ProcessoSeletivoId == processoId)).Should().BeTrue(
            "a publicação foi recusada, e o operador precisa reencontrar a transcrição que fez");
        (await EnvelopesDoProcessoAsync(api, processoId)).Should().Be(0, "nenhum evento nem mensagem de cópia saiu para um commit que não houve");
    }

    private static Task<int> EnvelopesDoProcessoAsync(CascadingApiFactory api, Guid processoId) =>
        ConsultarAsync(api, db => db.Database.SqlQuery<int>($"""
            SELECT (SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes WHERE position(convert_to({processoId.ToString("D")}, 'UTF8') in body) > 0)
                 + (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE position(convert_to({processoId.ToString("D")}, 'UTF8') in body) > 0) AS "Value"
            """).SingleAsync());

    private static Task<int> MensagensDaCopiaNaFilaAsync(CascadingApiFactory api, Guid atoId) =>
        ConsultarAsync(api, db => db.Database.SqlQuery<int>($"""
            SELECT (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE position(convert_to({atoId.ToString("D")}, 'UTF8') in body) > 0)
                 + (SELECT count(*) FROM wolverine_queues.wolverine_queue_selecao_copia_ao_acervo WHERE position(convert_to({atoId.ToString("D")}, 'UTF8') in body) > 0) AS "Value"
            """).SingleAsync());

    private static Task<int> MensagensDaCopiaNaFilaMortaAsync(CascadingApiFactory api, Guid atoId) =>
        ConsultarAsync(api, db => db.Database.SqlQuery<int>(
            $"SELECT count(*)::int AS \"Value\" FROM wolverine.wolverine_dead_letters WHERE position(convert_to({atoId.ToString("D")}, 'UTF8') in body) > 0")
            .SingleAsync());

    private static async Task<T> ConsultarAsync<T>(CascadingApiFactory api, Func<UniPlusDbContext, Task<T>> consulta)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();

        return await consulta(escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>());
    }

    /// <summary>O processo publicado pelo endpoint, com o ato que criou a versão 1 e o cliente que o publicou.</summary>
    private sealed record ProcessoPublicado(Guid ProcessoId, Guid AtoId, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    /// <summary>O acervo de teste fora do ar enquanto o cenário dura; sai do ar de novo ao descartar, para não vazar para outro teste.</summary>
    private sealed class AcervoIndisponivel : IDisposable
    {
        private readonly AcervoPublicoEmMemoria _acervo;

        private AcervoIndisponivel(AcervoPublicoEmMemoria acervo) => _acervo = acervo;

        public static AcervoIndisponivel Ligar(CascadingApiFactory api)
        {
            AcervoPublicoEmMemoria acervo = AcervoDaFabrica(api);
            acervo.Indisponivel = true;

            return new AcervoIndisponivel(acervo);
        }

        public void Restabelecer() => _acervo.Indisponivel = false;

        public void Dispose() => _acervo.Indisponivel = false;
    }
}

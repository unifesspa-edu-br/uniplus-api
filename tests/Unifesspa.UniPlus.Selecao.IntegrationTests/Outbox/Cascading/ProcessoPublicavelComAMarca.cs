namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Um processo pronto para publicar, no host real, cujo cronograma tem a fase de resultado final do semeador e as fases que o teste
/// acrescenta, com a marca "no mesmo momento da anterior" onde o teste a pede. Publica, retifica e consulta o painel de conformidade
/// pelos mesmos endpoints que o operador usa, e altera a fase direto no banco para montar o estado que a gravação pelo HTTP recusaria.
/// </summary>
internal sealed class ProcessoPublicavelComAMarca
{
    internal sealed record ItemDoPainel(string Codigo, string Dimensao, string Mensagem, bool Ok);

    internal const string ItemDaPrecedenciaNoPainel = "cronograma_precedencia_entre_fases";

    private readonly CascadingFixture _fixture;

    private ProcessoPublicavelComAMarca(CascadingFixture fixture, Guid processoId, Guid documentoId)
    {
        _fixture = fixture;
        ProcessoId = processoId;
        DocumentoId = documentoId;
    }

    internal Guid ProcessoId { get; }

    internal Guid DocumentoId { get; }

    /// <summary>Semeia o processo com a fase de resultado final na ordem pedida, mais as fases informadas.</summary>
    internal static async Task<ProcessoPublicavelComAMarca> SemearAsync(
        CascadingFixture fixture, string nome, int ordemDoResultadoFinal, params FaseCronograma[] fasesAdicionais)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, Opcoes(nome, ordemDoResultadoFinal, fasesAdicionais));

        return new ProcessoPublicavelComAMarca(fixture, processo.Id, documento.Id);
    }

    private static OpcoesDoProcessoSeletivoPublicavel Opcoes(string nome, int ordemDoResultadoFinal, FaseCronograma[] fasesAdicionais) => new()
    {
        Nome = $"{nome} {Guid.CreateVersion7()}",
        OrdemDaFaseDeResultado = ordemDoResultadoFinal,
        FasesAdicionais = fasesAdicionais,
    };

    internal async Task<HttpResponseMessage> PublicarAsync()
    {
        using HttpClient cliente = _fixture.Factory.CreateClient();

        return await PublicacaoPeloEndpointDeTeste.PublicarAsync(cliente, ProcessoId, DocumentoId);
    }

    /// <summary>Publica por um cliente de outro host sobre o mesmo banco, o do teste que instala um interceptador.</summary>
    internal Task<HttpResponseMessage> PublicarPeloClienteAsync(HttpClient cliente) =>
        PublicacaoPeloEndpointDeTeste.PublicarAsync(cliente, ProcessoId, DocumentoId);

    /// <summary>Retifica com um documento novo, sem pedir nada além de congelar o que o processo tem.</summary>
    internal async Task<HttpResponseMessage> RetificarAsync()
    {
        Guid documento = await PublicacaoPeloEndpointDeTeste.SemearDocumentoConfirmadoAsync(_fixture.Factory, ProcessoId);
        using HttpClient cliente = _fixture.Factory.CreateClient();

        return await PublicacaoPeloEndpointDeTeste.RetificarAsync(cliente, ProcessoId, documento);
    }

    internal async Task<int> VersoesPublicadasAsync() =>
        (await EstadoDaRetificacaoNoBanco.VersoesAsync(_fixture.Factory.Services, ProcessoId)).Count;

    /// <summary>A versão mais recente do registro publicado, como o banco a guarda.</summary>
    internal async Task<VersaoConfiguracao> UltimaVersaoPublicadaAsync() =>
        (await EstadoDaRetificacaoNoBanco.VersoesAsync(_fixture.Factory.Services, ProcessoId))[^1];

    /// <summary>
    /// Acrescenta ao registro uma versão sucessora cuja marca da primeira fase não é verdadeiro nem falso, como um registro que o
    /// banco guardaria fora da forma: é essa a versão que a retificação seguinte lê.
    /// </summary>
    internal async Task SucederComORegistroForaDaFormaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao anterior = await db.VersoesConfiguracao.Where(v => v.ProcessoSeletivoId == ProcessoId).OrderByDescending(v => v.NumeroVersao).FirstAsync();
        JsonObject registro = JsonNode.Parse(anterior.ConfiguracaoCongelada)!.AsObject();
        registro["cronogramaFases"]!["fases"]![0]!["noMesmoMomentoDaAnterior"] = "sim";
        DocumentoCongelado congelado = new(PerfilCanonicoV1.Instancia.Serializar(registro), anterior.SchemaVersion, anterior.AlgoritmoHash, anterior.AtoCriadorHash);
        db.VersoesConfiguracao.Add(VersaoConfiguracao.Suceder(anterior, congelado, Guid.CreateVersion7(), "plataforma-admin", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>Se o painel de conformidade dá por conforme o item da precedência entre fases.</summary>
    internal async Task<bool> PrecedenciaConformeNoPainelAsync() =>
        (await ItensDoPainelAsync()).Single(static item => item.Codigo == ItemDaPrecedenciaNoPainel).Ok;

    /// <summary>Os itens que o painel de conformidade devolve; o painel responde 200 em qualquer estado do processo.</summary>
    internal async Task<IReadOnlyList<ItemDoPainel>> ItensDoPainelAsync()
    {
        using HttpClient cliente = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await ApoioDaPublicacaoDoProcessoSeletivo.GetConformidadeAsync(cliente, ProcessoId);
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument painel = JsonDocument.Parse(corpo);

        return [.. painel.RootElement.GetProperty("itens").EnumerateArray().Select(static item => new ItemDoPainel(
            item.GetProperty("codigo").GetString()!, item.GetProperty("dimensao").GetString()!, item.GetProperty("mensagem").GetString()!, item.GetProperty("ok").GetBoolean()))];
    }

    /// <summary>Põe ou tira a marca da fase direto no banco, como se o cronograma tivesse sido gravado assim.</summary>
    internal async Task MarcarAFaseNoBancoAsync(string codigoDaFase, bool marca)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE selecao.fases_cronograma SET no_mesmo_momento_da_anterior = {marca} WHERE processo_seletivo_id = {ProcessoId} AND codigo = {codigoDaFase}");
    }
}

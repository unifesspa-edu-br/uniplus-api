namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.RegistroPelaPortaDeTeste;

/// <summary>
/// A recusa do registro central chega à publicação como 409 nomeado, na mesma transação: nada fica gravado, nem versão,
/// nem ato, nem divulgação, e o rascunho da transcrição sobrevive para o operador tentar de novo.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed partial class RecusasDoRegistroNaPublicacaoTests
{
    private const string VagaOcupada = "uniplus.publicacoes.ato_normativo.objeto_ja_tem_ato_vivo_do_tipo";
    private const string AtoJaRetificado = "uniplus.publicacoes.ato_normativo.ato_ja_retificado";

    private readonly CascadingFixture _fixture;

    public RecusasDoRegistroNaPublicacaoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "vaga do certame ocupada por outra linhagem recusa a publicação com 409, citando o ato que a ocupa, e nada é gravado")]
    public async Task VagaOcupadaPorOutraLinhagem_RecusaAPublicacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoId) = await PrepararAsync(api, nameof(VagaOcupadaPorOutraLinhagem_RecusaAPublicacao));
        await RegistrarEGravarAsync(api, AtoDoProcesso(processoId, DadosDoAtoDeTeste.TipoAbertura, "555/2026", new AtributosDoTipoAto(true, true, false, null)));

        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        await ExigirRecusaAsync(resposta, VagaOcupada, citando: "555/2026");
        await ExigirNadaGravadoAsync(api, processoId, atosDeOutroCaminho: 1);
    }

    [Fact(DisplayName = "conflito só no histórico, de ato registrado antes de o tipo virar único, também recusa a publicação com 409")]
    public async Task ConflitoApenasNoHistorico_RecusaAPublicacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoId) = await PrepararAsync(api, nameof(ConflitoApenasNoHistorico_RecusaAPublicacao));
        await RegistrarEGravarAsync(api, AtoDoProcesso(processoId, DadosDoAtoDeTeste.TipoAbertura, "444/2026", new AtributosDoTipoAto(true, false, false, null)));
        (await VagasDoObjetoAsync(api, processoId)).Should().Be(0, "pré-condição: o tipo não era único, e não há vaga reservada");

        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        await ExigirRecusaAsync(resposta, VagaOcupada, citando: "444/2026");
        await ExigirNadaGravadoAsync(api, processoId, atosDeOutroCaminho: 1);
    }

    [Fact(DisplayName = "ato já retificado por outro caminho recusa a retificação com 409, e nada é gravado")]
    public async Task AtoJaRetificadoPorOutroCaminho_RecusaARetificacao()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        (Guid processoId, Guid documentoAbertura) = await PrepararAsync(api, nameof(AtoJaRetificadoPorOutroCaminho_RecusaARetificacao));
        (await PublicarAsync(client, processoId, documentoAbertura)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid abertura = await ObterAtoIdAsync(api, processoId);
        await RegistrarEGravarAsync(api, RetificacaoPorOutroCaminho(abertura, processoId));

        using HttpResponseMessage resposta = await RetificarAsync(client, processoId, await SemearDocumentoConfirmadoAsync(api, processoId));

        await ExigirRecusaAsync(resposta, AtoJaRetificado, citando: "EDITAL");
        await ExigirSoAVersaoDaAberturaAsync(api, processoId, abertura);
    }

    private static AtoNormativoPublicado RetificacaoPorOutroCaminho(Guid abertura, Guid processoId) =>
        AtoDoProcesso(processoId, DadosDoAtoDeTeste.TipoRetificacao, "333/2026", new AtributosDoTipoAto(true, false, false, null)) with
        {
            Retificacao = new RetificacaoDoAto(abertura, "Retificação registrada fora do fluxo da Seleção"),
            Vinculos = [],
        };

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> PrepararAsync(CascadingApiFactory api, string nome)
    {
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        return await SemearProcessoAsync(api, nome);
    }

    private static async Task ExigirRecusaAsync(HttpResponseMessage resposta, string codigo, string citando)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict, corpo);
        JsonElement problema = JsonSerializer.Deserialize<JsonElement>(corpo);
        problema.GetProperty("code").GetString().Should().Be(codigo);
        string detalhe = problema.GetProperty("detail").GetString()!;
        detalhe.Should().Contain(citando);
        GuidNoTexto().IsMatch(detalhe).Should().BeFalse("o detalhe cita série, número e ano, e não identificadores internos");
    }

    private static async Task ExigirSoAVersaoDaAberturaAsync(CascadingApiFactory api, Guid processoId, Guid abertura)
    {
        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        UniPlusDbContext db = conferencia.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        (await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == processoId)).Should().Be(1, "só a versão da abertura existe");
        (await db.Set<AtoNormativo>().AsNoTracking().CountAsync(a => a.AtoRetificadoId == abertura)).Should().Be(1, "só a retificação feita por fora existe");
    }

    private static async Task<int> VagasDoObjetoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        return await conferencia.ServiceProvider.GetRequiredService<UniPlusDbContext>().Set<VagaDeLinhagem>().AsNoTracking()
            .CountAsync(l => l.EntidadeId == processoId);
    }

    private static async Task ExigirNadaGravadoAsync(CascadingApiFactory api, Guid processoId, int atosDeOutroCaminho)
    {
        await using AsyncServiceScope conferencia = api.Services.CreateAsyncScope();
        UniPlusDbContext db = conferencia.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        (await db.VersoesConfiguracao.AsNoTracking().AnyAsync(v => v.ProcessoSeletivoId == processoId)).Should().BeFalse("não há versão");
        (await db.AtosDoProcesso.AsNoTracking().AnyAsync(a => a.ProcessoSeletivoId == processoId)).Should().BeFalse("não há ato do processo");
        (await db.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == processoId)).Should().BeFalse("não há divulgação");
        (await db.Set<VinculoAtoEntidade>().AsNoTracking().CountAsync(v => v.EntidadeId == processoId))
            .Should().Be(atosDeOutroCaminho, "só o ato semeado por outro caminho existe, e a recusa não deixou ato no registro central");
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GuidNoTexto();
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A cadeia completa do envelope fechado (issue #1045, story-pai #40): um processo RICO,
/// publicado pelo caminho HTTP com todas as dimensões que a cascata #1059/#1067/#1068/#563/#1069
/// entregou, o ato confirmado em Publicações — não só enfileirado —, uma sessão editorial que
/// altera e fecha, o segundo ato confirmado retificando o primeiro, e a versão vigente refletindo
/// o fechamento.
/// </summary>
/// <remarks>
/// <para>
/// Nenhum cenário isolado aqui é novo: cada elo já tem dono (§2 do plano da issue —
/// <c>RegistroDeAtoPorFilaDuravelTests</c>, <c>CicloDaRetificacaoEndpointTests</c>,
/// <c>EnvelopeCodecRoundTripTests</c>, <c>PoliticaDeOrdenacaoTests</c>). O que não existia em teste
/// algum era a <b>corrente inteira</b>, pelo caminho HTTP, com um processo que exercita fatos
/// coletados, cascata de remanejamento, divulgação e formulário — o corpus rico só era exercitado
/// por chamada direta ao domínio (<c>CorpusEnvelope</c>), nunca publicado por HTTP.
/// </para>
/// <para>
/// A golden fixture de <c>CorpusEnvelope</c> <b>não é oráculo</b> deste teste: a API de cronograma
/// resolve <c>FaseCanonicaId</c>/<c>TiposBancaIds</c> contra o catálogo vivo de Configuração, e o
/// envelope produzido por HTTP é semanticamente equivalente ao do corpus de domínio, não idêntico
/// byte a byte. Os catálogos que os handlers resolvem (fases canônicas, tipos de banca, ofertas de
/// curso, referência de reserva demográfica, unidade administradora) não têm <c>HasData</c> — são
/// semeados aqui, diretamente pelo <c>DbContext</c> de cada módulo, sob o mesmo Postgres
/// compartilhado que a <see cref="CascadingFixture"/> já provisiona para o host inteiro. Falta de
/// seed de catálogo é falha de arranjo, nunca evidência de endpoint ausente.
/// </para>
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed partial class EnvelopeFechadoE2ETests
{
    /// <summary>
    /// Os códigos dos atos que a fase de resultado publica — o preliminar, que abre o ciclo
    /// recursal, e o definitivo, que o conclui. Nenhum congela configuração (são resultado,
    /// não edital/retificação) e nenhum é único por objeto (um certame tem mais de um
    /// resultado ao longo do ciclo).
    /// </summary>
    private const string CodigoAtoResultado = "RESULTADO_CLASSIFICACAO";

    private const string TituloDoCiclo =
        "Ciclo completo pelo HTTP: criar → configurar todas as dimensões → GET /conformidade verde → " +
        "publicar (ato V1 confirmado em Publicações) → abrir/alterar/fechar retificação (ato V2 confirmado, " +
        "retificando V1) → GET /snapshot-vigente reflete V2, V1 permanece intacta";

    private readonly CascadingFixture _fixture;

    public EnvelopeFechadoE2ETests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = TituloDoCiclo)]
    public async Task CicloCompleto_DoHttpAoAtoConfirmadoEmPublicacoes()
    {
        Cenario cenario = await PrepararAsync();

        await ConfigurarTodasAsDimensoesAsync(cenario);
        await ConferirChecklistVerdeAsync(cenario);
        await PublicarEConfirmarV1Async(cenario);
        await RetificarSobSessaoEditorialAsync(cenario);
        await ConferirIdentidadeEAtoV2Async(cenario);
        await ConferirSnapshotVigenteAsync(cenario);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Checkpoint — valida a resposta IMEDIATAMENTE, nomeando fase/rota/status/ProblemDetails
    // ══════════════════════════════════════════════════════════════════════════════

    private static async Task<HttpResponseMessage> ExecutarPassoAsync(
        Func<Task<HttpResponseMessage>> acao, HttpStatusCode esperado, string passo, string rota)
    {
        HttpResponseMessage resposta = await acao().ConfigureAwait(false);
        if (resposta.StatusCode != esperado)
        {
            string corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
            resposta.StatusCode.Should().Be(
                esperado,
                $"passo '{passo}' ({rota}) esperava {(int)esperado} {esperado} e recebeu " +
                $"{(int)resposta.StatusCode} {resposta.StatusCode}. ProblemDetails: {corpo}");
        }

        return resposta;
    }

    private static string LerETag(HttpResponseMessage resposta, string passo)
    {
        resposta.Headers.ETag.Should().NotBeNull($"passo '{passo}': a resposta de uma sessão editorial carrega o ETag");
        return resposta.Headers.ETag!.ToString();
    }

    /// <summary>
    /// Localiza, dentro do bloco <c>etapas</c> do envelope congelado, o item cujo <c>id</c>
    /// bate com <paramref name="etapaId"/> — casamento por identidade, nunca por posição no
    /// array (<c>SerializacaoDasEtapas.SerializarEtapas</c> reordena por conteúdo
    /// quando duas etapas empatam em <c>Ordem</c>).
    /// </summary>
    private static JsonObject LocalizarEtapaPorId(JsonNode configuracao, Guid etapaId) =>
        configuracao["etapas"]!.AsArray()
            .Select(static no => no!.AsObject())
            .Single(etapa => etapa["id"]!.GetValue<Guid>() == etapaId);
}

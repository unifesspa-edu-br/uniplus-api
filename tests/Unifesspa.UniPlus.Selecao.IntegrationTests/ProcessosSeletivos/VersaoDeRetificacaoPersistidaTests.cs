namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) da retificação
/// (congelamento dos parâmetros na publicação, UNI-REQ-0019, ADR-0101/0103/0104): a retificação sucede a versão corrente com um ato que
/// emenda o ato criador dela, e o novo snapshot acrescenta o bloco de retificação
/// preservando integralmente os blocos da abertura; o snapshot da abertura permanece imutável.
/// </summary>
[Collection(RetificacaoPersistenciaColecao.Nome)]
public sealed class VersaoDeRetificacaoPersistidaTests
{
    private readonly CenarioDeRetificacaoPersistida _cenario;

    public VersaoDeRetificacaoPersistidaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeRetificacaoPersistida(fixture);
    }

    [Fact(DisplayName = "A retificação persiste uma segunda versão, criada por um ato que emenda o ato criador da primeira")]
    public async Task Retificacao_PersisteVersaoQueEmendaAAnterior()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao abertura, VersaoConfiguracao retificacao) =
            await _cenario.PublicarERetificarAsync(nameof(Retificacao_PersisteVersaoQueEmendaAAnterior));

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processo.Id)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(CancellationToken.None);

        ConferirLinhagemDaRetificacao(versoes, abertura, retificacao);
    }

    private static void ConferirLinhagemDaRetificacao(
        List<VersaoConfiguracao> versoes, VersaoConfiguracao abertura, VersaoConfiguracao retificacao)
    {
        versoes.Should().HaveCount(2);
        versoes[0].AtoCriadorId.Should().Be(abertura.AtoCriadorId);
        versoes[0].AtoCriadorRetificaId.Should().BeNull("a raiz da cadeia não emenda ninguém");

        versoes[1].AtoCriadorId.Should().Be(retificacao.AtoCriadorId);
        versoes[1].AtoCriadorRetificaId.Should().Be(
            abertura.AtoCriadorId,
            "a linhagem sobrevive à ida e volta do banco: a versão 2 foi criada por um ato que emenda o ato criador da versão 1");

        // O motivo não se perdeu com a tabela de editais: ele está congelado nos bytes
        // canônicos (ADR-0100) e viaja para Publicações na mensagem durável (ADR-0108).
        JsonNode.Parse(versoes[1].ConfiguracaoCongelada)!["retificacao"]!["motivo"]!
            .GetValue<string>().Should().Be("Correção do prazo de inscrição");
    }

    [Fact(DisplayName = "Snapshot de retificação carrega os blocos da abertura mais o bloco retificacao; o snapshot da abertura permanece imutável")]
    public async Task Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel()
    {
        (_, VersaoConfiguracao versaoAbertura, VersaoConfiguracao versaoRetificacao) =
            await _cenario.PublicarERetificarAsync(nameof(Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel));

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();

        VersaoConfiguracao aberturaLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoAbertura.Id);
        JsonObject payloadAbertura = JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!.AsObject();

        VersaoConfiguracao retificacaoLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoRetificacao.Id);
        JsonObject payloadRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!.AsObject();

        ConferirBlocosDaRetificacao(payloadAbertura, payloadRetificacao);
        ConferirAberturaImutavel(aberturaLida, versaoAbertura, payloadAbertura);
    }

    private static void ConferirBlocosDaRetificacao(JsonObject payloadAbertura, JsonObject payloadRetificacao)
    {
        // Os blocos esperados na retificação são DERIVADOS do snapshot da abertura, não uma
        // lista literal escrita à mão: são exatamente os blocos da abertura mais o bloco
        // `retificacao` (ADR-0101). Assim, se a abertura ganhar um bloco novo e a retificação
        // não o preservar — qualquer um deles, não só os que alguém lembrou de listar —, esta
        // asserção falha sozinha, sem que ninguém precise atualizar um número.
        IEnumerable<string> blocosEsperados = [.. payloadAbertura.Select(static kvp => kvp.Key), "retificacao"];
        payloadRetificacao.Select(static kvp => kvp.Key).Should().BeEquivalentTo(blocosEsperados,
            "a retificação preserva integralmente os blocos da abertura e acrescenta o bloco retificacao");

        payloadRetificacao["retificacao"]!["motivo"]!.GetValue<string>().Should().Be("Correção do prazo de inscrição");
    }

    private static void ConferirAberturaImutavel(
        VersaoConfiguracao aberturaLida, VersaoConfiguracao versaoAbertura, JsonObject payloadAbertura)
    {
        // O snapshot da abertura permanece byte-a-byte idêntico (append-only).
        aberturaLida.HashConfiguracao.Should().Be(versaoAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);
        payloadAbertura.Should().NotContainKey("retificacao",
            "o snapshot da abertura nunca carrega o bloco de retificação");
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco (retificação) — re-hashear os bytes lidos do banco bate com o hash persistido")]
    public async Task Retificacao_HashConfereAppEBanco()
    {
        (_, _, VersaoConfiguracao versaoRetificacao) =
            await _cenario.PublicarERetificarAsync(nameof(Retificacao_HashConfereAppEBanco));

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao lida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoRetificacao.Id);

        HashCanonicalComputer.ComputeSha256Hex(lida.ConfiguracaoCongeladaCanonica)
            .Should().Be(lida.HashConfiguracao);
    }
}

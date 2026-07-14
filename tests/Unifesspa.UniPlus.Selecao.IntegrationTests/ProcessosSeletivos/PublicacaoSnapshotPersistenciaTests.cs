namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cobertura de integração (Postgres real via Testcontainers) do
/// congelamento do snapshot de publicação (RN08, ADR-0100, Story #759 T4
/// #785). Mapa de testes de #759: <c>Snapshot_HashConfereAppEBanco</c>
/// (re-hashear os bytes lidos de volta do banco bate com o hash persistido
/// pela app) e <c>Snapshot_ContemBlocosCanonicos</c> (os 17 blocos — 11
/// reais + 6 stubs <c>nao_construido</c> — estão presentes).
/// </summary>
public sealed class PublicacaoSnapshotPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public PublicacaoSnapshotPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar[0], 64)).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021").Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private async Task<(Guid ProcessoId, Guid EditalId, Guid SnapshotId)> PublicarAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        Result confirmarResult = documento.Confirmar(1024, HashFixo, TimeProvider.System);
        confirmarResult.IsSuccess.Should().BeTrue();

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id);
        dadosResult.IsSuccess.Should().BeTrue();

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosResult.Value!, documento.HashSha256!));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(publicarResult.Value!, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        return (processo.Id, publicarResult.Value!.AtoCriadorId, publicarResult.Value!.Id);
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco — re-hashear os bytes lidos do banco bate com o hash persistido pela app")]
    public async Task Snapshot_HashConfereAppEBanco()
    {
        (_, _, Guid snapshotId) = await PublicarAsync(nameof(Snapshot_HashConfereAppEBanco));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        string hashRecalculado = HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica);

        hashRecalculado.Should().Be(versao.HashConfiguracao,
            "ADR-0100 §Confirmação: re-hashear os bytes persistidos deve bater com o hash calculado pela aplicação na publicação");
    }

    [Fact(DisplayName = "Snapshot_ContemBlocosCanonicos — os 17 blocos (10 reais + 7 stubs) estão presentes")]
    public async Task Snapshot_ContemBlocosCanonicos()
    {
        (_, _, Guid snapshotId) = await PublicarAsync(nameof(Snapshot_ContemBlocosCanonicos));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        JsonNode payload = JsonNode.Parse(versao.ConfiguracaoCongelada)!;

        string[] blocosEsperados =
        [
            "periodo", "etapas", "vagas", "distribuicao", "modalidades", "ofertas",
            "atendimento", "bonusRegional", "criteriosDesempate", "classificacao", "hashesEdital",
            "documentosExigidos", "formulario", "cascataRemanejamento", "divulgacao",
            "cronogramaFases", "identidadesUnidade",
        ];
        blocosEsperados.Should().HaveCount(17, "pré-condição do próprio teste — o envelope tem 17 chaves");

        JsonObject objeto = payload.AsObject();
        foreach (string bloco in blocosEsperados)
        {
            objeto.Should().ContainKey(bloco, $"o bloco canônico '{bloco}' deve estar presente no envelope");
        }

        // CA-07: a contagem é DERIVADA do envelope, não escrita à mão. Um bloco
        // que sai de stub sem que este teste seja atualizado faz a contagem
        // divergir — que é exatamente o sinal que se quer.
        objeto.Should().HaveCount(17, "o envelope de abertura tem exatamente 17 chaves — nem mais, nem menos");

        string[] stubs = [.. objeto
            .Where(static kvp => kvp.Value is JsonObject bloco
                && bloco.TryGetPropertyValue("status", out JsonNode? status)
                && status?.GetValue<string>() == "nao_construido")
            .Select(static kvp => kvp.Key)
            .Order(StringComparer.Ordinal)];

        stubs.Should().BeEquivalentTo(
            [
                "cascataRemanejamento", "cronogramaFases", "divulgacao",
                "documentosExigidos", "formulario", "identidadesUnidade", "vagas",
            ],
            "são exatamente as 7 dimensões da Feature #40 ainda sem dono — e os 10 restantes são reais");

        // D8 — nenhum bloco REAL emite `nao_construido`. Atendimento e classificação
        // são dimensões obrigatórias: a ausência é pendência de conformidade, não um
        // stub silencioso.
        objeto["atendimento"]!.AsObject().Should().NotContainKey("status");
        objeto["classificacao"]!.AsObject().Should().NotContainKey("status");

        // Blocos reais carregam dado de negócio, não o marcador de stub.
        objeto["etapas"]!.AsArray().Should().NotBeEmpty();
        objeto["bonusRegional"]!["presente"]!.GetValue<bool>().Should().BeFalse();
    }
}

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
/// Cobertura de integração (Postgres real via Testcontainers) da retificação
/// (RN08, ADR-0101, Story #759 T5 #786): a retificação emite um novo Edital
/// vinculado ao vigente + novo snapshot com o bloco de retificação preservando
/// os 17 blocos anteriores; o snapshot da abertura permanece imutável. Mapa de
/// testes de #759: <c>Retificacao_NovoEditalSnapshotMotivo</c>.
/// </summary>
public sealed class RetificacaoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public RetificacaoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
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
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021").Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), null, [modalidade]).Value!])
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"), 1, []).Value!).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static DocumentoEdital DocumentoConfirmado(Guid processoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static DadosEdital NovosDados(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), documentoId).Value!;

    /// <summary>
    /// Publica um processo e o retifica em seguida — persistindo os dois
    /// Editais e os dois snapshots. Usa um relógio manual para dar instantes
    /// distintos à abertura e à retificação (ux_editais_processo_data_publicacao).
    /// </summary>
    private async Task<(ProcessoSeletivo Processo, Edital Abertura, SnapshotPublicacao SnapshotAbertura, Edital Retificacao, SnapshotPublicacao SnapshotRetificacao)>
        PublicarERetificarAsync(string nome)
    {
        RelogioManual clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(processo, dadosAbertura, docAbertura.HashSha256!);
        Result<PublicacaoResultado> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", clock);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarSnapshotPublicacaoAsync(publicar.Value!.Snapshot, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        clock.Avancar(TimeSpan.FromDays(1));

        // Recarrega o agregado tracked (com a cadeia de Editais) — como o handler faz.
        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);
        SnapshotPublicacao snapshotRetificacao;
        Edital retificacaoEdital;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(
                carregado, dadosRetificacao, docRetificacao.HashSha256!,
                new RetificacaoInfo(carregado.EditalVigente!.Id, "Correção do prazo de inscrição"));
            Result<PublicacaoResultado> retificar = carregado.Retificar(
                dadosRetificacao, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion, canonicoRetificacao.AlgoritmoHash,
                docRetificacao.HashSha256!, "integration-test-user", "Correção do prazo de inscrição", clock);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            snapshotRetificacao = retificar.Value!.Snapshot;
            retificacaoEdital = retificar.Value!.Edital;

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarSnapshotPublicacaoAsync(snapshotRetificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo, publicar.Value!.Edital, publicar.Value!.Snapshot, retificacaoEdital, snapshotRetificacao);
    }

    [Fact(DisplayName = "Retificacao persiste um segundo Edital (retificação) vinculado ao vigente + segundo snapshot")]
    public async Task Retificacao_PersisteNovoEditalESnapshot()
    {
        (ProcessoSeletivo processo, Edital abertura, _, Edital retificacao, _) =
            await PublicarERetificarAsync(nameof(Retificacao_PersisteNovoEditalESnapshot));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        List<Edital> editais = await readContext.Set<Edital>()
            .AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processo.Id)
            .ToListAsync(CancellationToken.None);

        editais.Should().HaveCount(2);
        editais.Should().ContainSingle(e => e.Natureza == NaturezaEdital.Abertura && e.Id == abertura.Id);
        Edital retificacaoPersistida = editais.Single(e => e.Natureza == NaturezaEdital.Retificacao);
        retificacaoPersistida.Id.Should().Be(retificacao.Id);
        retificacaoPersistida.EditalRetificadoId.Should().Be(abertura.Id);
        retificacaoPersistida.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");

        int totalSnapshots = await readContext.SnapshotsPublicacao
            .AsNoTracking()
            .CountAsync(s => s.EditalId == abertura.Id || s.EditalId == retificacao.Id, CancellationToken.None);
        totalSnapshots.Should().Be(2);
    }

    [Fact(DisplayName = "Snapshot de retificação carrega o bloco retificacao + os 17 blocos; o snapshot da abertura permanece imutável")]
    public async Task Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel()
    {
        (_, _, SnapshotPublicacao snapshotAbertura, _, SnapshotPublicacao snapshotRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();

        SnapshotPublicacao retificacaoLida = await readContext.SnapshotsPublicacao
            .AsNoTracking().FirstAsync(s => s.Id == snapshotRetificacao.Id, CancellationToken.None);
        JsonObject payloadRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!.AsObject();

        // Os 17 blocos canônicos continuam presentes...
        foreach (string bloco in new[]
        {
            "periodo", "etapas", "vagas", "distribuicao", "modalidades", "ofertas", "atendimento",
            "bonusRegional", "criteriosDesempate", "classificacao", "hashesEdital", "documentosExigidos",
            "formulario", "cascataRemanejamento", "divulgacao", "cronogramaFases", "identidadesUnidade",
        })
        {
            payloadRetificacao.Should().ContainKey(bloco);
        }

        // ...e o 18º bloco de retificação foi acrescentado (ADR-0101).
        payloadRetificacao.Should().ContainKey("retificacao");
        payloadRetificacao["retificacao"]!["motivo"]!.GetValue<string>().Should().Be("Correção do prazo de inscrição");

        // O snapshot da abertura permanece byte-a-byte idêntico (append-only).
        SnapshotPublicacao aberturaLida = await readContext.SnapshotsPublicacao
            .AsNoTracking().FirstAsync(s => s.Id == snapshotAbertura.Id, CancellationToken.None);
        aberturaLida.HashConfiguracao.Should().Be(snapshotAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(snapshotAbertura.ConfiguracaoCongeladaCanonica);
        JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!.AsObject().Should().NotContainKey("retificacao",
            "o snapshot da abertura nunca carrega o bloco de retificação");
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco (retificação) — re-hashear os bytes lidos do banco bate com o hash persistido")]
    public async Task Retificacao_HashConfereAppEBanco()
    {
        (_, _, _, _, SnapshotPublicacao snapshotRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_HashConfereAppEBanco));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        SnapshotPublicacao lida = await readContext.SnapshotsPublicacao
            .AsNoTracking().FirstAsync(s => s.Id == snapshotRetificacao.Id, CancellationToken.None);

        HashCanonicalComputer.ComputeSha256Hex(lida.ConfiguracaoCongeladaCanonica)
            .Should().Be(lida.HashConfiguracao);
    }

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}

namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

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
/// Cobertura de integração (Postgres real via Testcontainers) do seletor de
/// snapshot vigente (RN08, Story #759 T6 #787, ADR-0075/0076):
/// <c>ObterSnapshotVigenteAsync</c> resolve o Edital publicado de MAIOR
/// <c>data_publicacao</c> ≤ o instante. Com abertura (T0) + retificação (T0+1d),
/// um instante posterior resolve a retificação; um instante em T0 resolve a
/// abertura; um instante anterior a T0 não resolve nada.
/// </summary>
public sealed class SnapshotVigentePersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public SnapshotVigentePersistenciaTests(ProcessoSeletivoDbFixture fixture)
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
    /// Publica a abertura em T0 e retifica em T0+1 dia, persistindo os dois
    /// Editais e snapshots com <c>data_publicacao</c> distintas (relógio manual).
    /// </summary>
    private async Task<(Guid ProcessoId, SnapshotPublicacao SnapshotAbertura, SnapshotPublicacao SnapshotRetificacao)>
        PublicarERetificarAsync(string nome)
    {
        RelogioManual clock = new(T0);

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

        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);
        SnapshotPublicacao snapshotRetificacao;
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

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarSnapshotPublicacaoAsync(snapshotRetificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo.Id, publicar.Value!.Snapshot, snapshotRetificacao);
    }

    [Fact(DisplayName = "Seletor resolve o snapshot de maior data_publicacao ≤ instante — retificação após ela, abertura em T0 (CA-08)")]
    public async Task ObterSnapshotVigente_ResolveMaiorDataAntesOuIgualAoInstante()
    {
        (Guid processoId, SnapshotPublicacao snapshotAbertura, SnapshotPublicacao snapshotRetificacao) =
            await PublicarERetificarAsync(nameof(ObterSnapshotVigente_ResolveMaiorDataAntesOuIgualAoInstante));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        // Instante posterior à retificação → resolve a retificação (maior data).
        (Edital Edital, SnapshotPublicacao Snapshot)? posterior = await repository
            .ObterSnapshotVigenteAsync(processoId, T0.AddDays(2), CancellationToken.None);
        posterior.Should().NotBeNull();
        posterior!.Value.Edital.Natureza.Should().Be(NaturezaEdital.Retificacao);
        posterior.Value.Snapshot.HashConfiguracao.Should().Be(snapshotRetificacao.HashConfiguracao);

        // Instante exatamente em T0 → resolve a abertura (retificação, em T0+1d, é excluída).
        (Edital Edital, SnapshotPublicacao Snapshot)? emT0 = await repository
            .ObterSnapshotVigenteAsync(processoId, T0, CancellationToken.None);
        emT0.Should().NotBeNull();
        emT0!.Value.Edital.Natureza.Should().Be(NaturezaEdital.Abertura);
        emT0.Value.Snapshot.HashConfiguracao.Should().Be(snapshotAbertura.HashConfiguracao);

        // Mesmo instante de T0, expresso em offset não-UTC (-03:00) — Npgsql
        // exige UTC para timestamptz; o seletor normaliza e resolve o mesmo.
        (Edital Edital, SnapshotPublicacao Snapshot)? emT0OffsetNaoUtc = await repository
            .ObterSnapshotVigenteAsync(processoId, T0.ToOffset(TimeSpan.FromHours(-3)), CancellationToken.None);
        emT0OffsetNaoUtc.Should().NotBeNull();
        emT0OffsetNaoUtc!.Value.Snapshot.HashConfiguracao.Should().Be(snapshotAbertura.HashConfiguracao);
    }

    [Fact(DisplayName = "Seletor não resolve nada quando o instante antecede toda publicação (base do 422)")]
    public async Task ObterSnapshotVigente_InstanteAntesDaAbertura_RetornaNull()
    {
        (Guid processoId, _, _) =
            await PublicarERetificarAsync(nameof(ObterSnapshotVigente_InstanteAntesDaAbertura_RetornaNull));

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        (Edital Edital, SnapshotPublicacao Snapshot)? vigente = await repository
            .ObterSnapshotVigenteAsync(processoId, T0.AddSeconds(-1), CancellationToken.None);

        vigente.Should().BeNull();
    }

    [Fact(DisplayName = "Seletor não resolve snapshot de processo soft-deleted — honra o filtro global de exclusão lógica (base do 404)")]
    public async Task ObterSnapshotVigente_ProcessoSoftDeleted_RetornaNull()
    {
        (Guid processoId, _, _) =
            await PublicarERetificarAsync(nameof(ObterSnapshotVigente_ProcessoSoftDeleted_RetornaNull));

        await using (SelecaoDbContext deleteContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = await deleteContext.ProcessosSeletivos
                .SingleAsync(p => p.Id == processoId, CancellationToken.None);
            processo.MarkAsDeleted("integration-test-user", T0.AddDays(3));
            await deleteContext.SaveChangesAsync(CancellationToken.None);
        }

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        // Edital não é soft-deletable, mas o EXISTS através de ProcessosSeletivos
        // herda o filtro global — o snapshot de um processo excluído não vaza.
        (Edital Edital, SnapshotPublicacao Snapshot)? vigente = await repository
            .ObterSnapshotVigenteAsync(processoId, T0.AddDays(5), CancellationToken.None);

        vigente.Should().BeNull("um processo excluído logicamente cai no mesmo 404 do resto da API");
        (await repository.ExisteAsync(processoId, CancellationToken.None)).Should().BeFalse();
    }

    [Fact(DisplayName = "Seletor não resolve nada para processo inexistente (base do 404)")]
    public async Task ObterSnapshotVigente_ProcessoInexistente_RetornaNull()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);

        (Edital Edital, SnapshotPublicacao Snapshot)? vigente = await repository
            .ObterSnapshotVigenteAsync(Guid.CreateVersion7(), T0.AddDays(5), CancellationToken.None);

        vigente.Should().BeNull();
        (await repository.ExisteAsync(Guid.CreateVersion7(), CancellationToken.None)).Should().BeFalse();
    }

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan delta) => _agora = _agora.Add(delta);
    }
}

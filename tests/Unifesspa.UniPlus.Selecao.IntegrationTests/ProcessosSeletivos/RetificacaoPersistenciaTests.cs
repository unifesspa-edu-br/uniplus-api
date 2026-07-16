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
/// (RN08, ADR-0101/0103/0104): a retificação sucede a versão corrente com um ato que
/// emenda o ato criador dela, e o novo snapshot acrescenta o bloco de retificação
/// preservando os 17 anteriores; o snapshot da abertura permanece imutável.
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, [], null, "Res. Unifesspa 532/2021").Value!;
        processo.DefinirDistribuicaoVagas([ConfiguracaoDistribuicaoVagas.Criar(
            Guid.CreateVersion7(), 40, 1m, Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"), null, [modalidade]).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"), 1, []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: Guid.CreateVersion7(),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    private static DocumentoEdital DocumentoConfirmado(Guid processoId)
    {
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static DadosEdital NovosDados(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), documentoId).Value!;

    /// <summary>
    /// Publica um processo e o retifica em seguida — persistindo as duas versões da
    /// configuração. Usa um relógio manual para dar instantes distintos à abertura e à
    /// retificação, como o certame real faz; a ordem, porém, vem da cadeia de versões,
    /// não desses instantes.
    /// </summary>
    private async Task<(ProcessoSeletivo Processo, VersaoConfiguracao VersaoAbertura, VersaoConfiguracao VersaoRetificacao)>
        PublicarERetificarAsync(string nome)
    {
        RelogioManual clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

        ProcessoSeletivo processo = NovoProcessoConforme(nome);
        DocumentoEdital docAbertura = DocumentoConfirmado(processo.Id);
        DadosEdital dadosAbertura = NovosDados(docAbertura.Id);
        SnapshotCanonico canonicoAbertura = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dadosAbertura, docAbertura.HashSha256!));
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            dadosAbertura, canonicoAbertura.Bytes, canonicoAbertura.SchemaVersion, canonicoAbertura.AlgoritmoHash,
            docAbertura.HashSha256!, "integration-test-user", clock);
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);

        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            await repository.AdicionarAsync(processo, CancellationToken.None);
            await writeContext.DocumentosEdital.AddAsync(docAbertura, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(publicar.Value!, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        clock.Avancar(TimeSpan.FromDays(1));

        // Recarrega o agregado tracked — como o handler faz.
        DocumentoEdital docRetificacao = DocumentoConfirmado(processo.Id);
        VersaoConfiguracao versaoRetificacao;
        await using (SelecaoDbContext writeContext = _fixture.CreateDbContext())
        {
            ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
            ProcessoSeletivo carregado = (await repository.ObterComConfiguracaoAsync(processo.Id, CancellationToken.None))!;
            VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processo.Id, CancellationToken.None))!;
            DadosEdital dadosRetificacao = NovosDados(docRetificacao.Id);
            SnapshotCanonico canonicoRetificacao = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(
                carregado, dadosRetificacao, docRetificacao.HashSha256!,
                // O alvo da retificação é o ato que criou a versão corrente — o
                // topo da cadeia de CONFIGURAÇÃO —, como o handler faz.
                new RetificacaoInfo(versaoAtual.AtoCriadorId, "Correção do prazo de inscrição")));
            Result<VersaoConfiguracao> retificar = carregado.Retificar(
                dadosRetificacao, versaoAtual, canonicoRetificacao.Bytes, canonicoRetificacao.SchemaVersion, canonicoRetificacao.AlgoritmoHash,
                docRetificacao.HashSha256!, "integration-test-user", "Correção do prazo de inscrição", clock);
            retificar.IsSuccess.Should().BeTrue(retificar.Error?.Message);
            versaoRetificacao = retificar.Value!;

            await writeContext.DocumentosEdital.AddAsync(docRetificacao, CancellationToken.None);
            await repository.AdicionarVersaoConfiguracaoAsync(versaoRetificacao, CancellationToken.None);
            await writeContext.SaveChangesAsync(CancellationToken.None);
        }

        return (processo, publicar.Value!, versaoRetificacao);
    }

    [Fact(DisplayName = "A retificação persiste uma segunda versão, criada por um ato que emenda o ato criador da primeira")]
    public async Task Retificacao_PersisteVersaoQueEmendaAAnterior()
    {
        (ProcessoSeletivo processo, VersaoConfiguracao abertura, VersaoConfiguracao retificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_PersisteVersaoQueEmendaAAnterior));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao
            .AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processo.Id)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync(CancellationToken.None);

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

    [Fact(DisplayName = "Snapshot de retificação carrega o bloco retificacao + os 17 blocos; o snapshot da abertura permanece imutável")]
    public async Task Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel()
    {
        (_, VersaoConfiguracao versaoAbertura, VersaoConfiguracao versaoRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_SnapshotComBlocoRetificacao_AnteriorImutavel));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();

        VersaoConfiguracao retificacaoLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);
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
        VersaoConfiguracao aberturaLida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoAbertura.Id, CancellationToken.None);
        aberturaLida.HashConfiguracao.Should().Be(versaoAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);
        JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!.AsObject().Should().NotContainKey("retificacao",
            "o snapshot da abertura nunca carrega o bloco de retificação");
    }

    [Fact(DisplayName = "Snapshot_HashConfereAppEBanco (retificação) — re-hashear os bytes lidos do banco bate com o hash persistido")]
    public async Task Retificacao_HashConfereAppEBanco()
    {
        (_, _, VersaoConfiguracao versaoRetificacao) =
            await PublicarERetificarAsync(nameof(Retificacao_HashConfereAppEBanco));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao lida = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == versaoRetificacao.Id, CancellationToken.None);

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

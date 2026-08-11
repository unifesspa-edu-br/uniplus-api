namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Semeia um Processo Seletivo conforme e o publica pelo caminho real do
/// agregado (Publicar → <see cref="VersaoConfiguracao"/>), persistindo tudo numa
/// transação. É o ponto de partida dos testes que precisam de um certame já
/// publicado para exercitar o que vem DEPOIS — em especial os que atacam a tabela
/// de versões com SQL cru.
/// </summary>
internal static class ProcessoSeletivoPublicacaoSeeder
{
    private static readonly string HashDocumento = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    /// <summary>
    /// O que a publicação produz: o certame, o id do ATO que criou a versão (decidido pela
    /// raiz; o ato em si é registrado depois, em Publicações — ADR-0108) e a versão congelada.
    /// </summary>
    internal sealed record Resultado(Guid ProcessoId, Guid AtoId, Guid VersaoId);

    private static ReferenciaRegra Regra(string codigo, char hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar, 64)).Value!;

    /// <summary>
    /// Processo com as quatro dimensões estruturalmente obrigatórias
    /// preenchidas (etapas, atendimento, distribuição de vagas, classificação)
    /// — o mínimo que <c>AvaliarConformidade</c> exige para publicar.
    /// </summary>
    public static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1),
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
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
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
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Processo com a oferta federal completa (Lei 12.711: as 8 modalidades federais +
    /// AC, referência demográfica, regra de ajuste) e, por padrão, a cascata de
    /// remanejamento que a cobre — a matriz legal completa (8×7, fallback AC), a mesma
    /// forma semeada em <c>REMANEJ-CASCATA-LEI-12711 v1</c>. É o processo mínimo que
    /// <c>PendenciaDaCascata</c> considera conforme quando a oferta usa o regime federal
    /// (Story #575) — as demais quatro dimensões estruturais (etapas, atendimento,
    /// classificação, cronograma) são as mesmas de <see cref="NovoProcessoConforme"/>.
    /// </summary>
    /// <param name="comCascata">
    /// <see langword="false"/> devolve o processo SEM a cascata — a oferta federal continua
    /// exigindo-a (as 8 modalidades são <c>SegueCascata</c>), então este processo não é
    /// publicável até que a cascata seja definida à parte. Usado pelos testes de
    /// concorrência que racionam <c>DefinirCascataRemanejamentoCommand</c> contra
    /// <c>PublicarProcessoSeletivoCommand</c> — o padrão <see langword="true"/> preserva o
    /// comportamento anterior para os demais chamadores.
    /// </param>
    public static ProcessoSeletivo NovoProcessoComOfertaFederalECascata(string nome, bool comCascata = true)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                "Prova Objetiva", CaraterEtapa.Classificatoria,
                TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!,
                peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([OfertaFederalCompleta()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
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
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (comCascata)
        {
            processo.DefinirCascataRemanejamento(CascataLegalCompleta(), PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    private static ConfiguracaoDistribuicaoVagas OfertaFederalCompleta()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", "Ampla concorrência",
                NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                criteriosCumulativos: [], acaoQuandoIndeferido: null,
                baseLegal: "Lei 12.711/2012 art. 1º").Value!,
        ];

        foreach (string codigo in ModalidadesFederaisLei12711.Codigos)
        {
            modalidades.Add(ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), codigo, $"Reserva {codigo}",
                NaturezaLegalModalidade.CotaReservada, ComposicaoVagasModalidade.DentroDoVr, null,
                RegraRemanejamentoModalidade.SegueCascata, null, null, null,
                criteriosCumulativos: [], acaoQuandoIndeferido: "RECLASSIFICAR_AC",
                baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!);
        }

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 50,
            pr: 0.5m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Lei12711, 'd'),
            regraAjuste: Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, 'e'),
            referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(
                Guid.CreateVersion7(), "Censo IBGE 2022", 78.55m, 1.20m, 8.40m, "Lei 12.711/2012 art. 3º").Value!,
            modalidades: modalidades).Value!;
    }

    /// <summary>A matriz legal completa (8×7), fallback AC — mesma forma semeada em REMANEJ-CASCATA-LEI-12711 v1.</summary>
    internal static ConfiguracaoCascataRemanejamento CascataLegalCompleta(char semente = 'f')
    {
        IReadOnlyList<string> origens = ModalidadesFederaisLei12711.Codigos;
        List<DestinoRemanejamento> destinos = [];
        foreach (string origem in origens)
        {
            string[] destinosDaOrigem = [.. origens.Where(o => o != origem)];
            for (int i = 0; i < destinosDaOrigem.Length; i++)
            {
                destinos.Add(DestinoRemanejamento.Criar(origem, i + 1, destinosDaOrigem[i]).Value!);
            }
        }

        return ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, semente), ModalidadesFederaisLei12711.Ac, destinos).Value!;
    }

    /// <summary>
    /// Publica o processo pelo agregado e persiste raiz, documento e versão 1
    /// da configuração — o mesmo caminho do handler, sem a pipeline HTTP.
    /// </summary>
    public static async Task<Resultado> PublicarAsync(ProcessoSeletivoDbFixture fixture, string nome)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ProcessoSeletivo processo = NovoProcessoConforme(nome);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(
            processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashDocumento, TimeProvider.System).IsSuccess.Should().BeTrue();

        DadosEdital dados = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: new DateOnly(2026, 1, 1),
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id).Value!;

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(new EntradaCanonicalizacao(processo, dados, documento.HashSha256!));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dados,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        await using SelecaoDbContext context = fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(context, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await context.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(publicarResult.Value!, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Resultado(
            processo.Id,
            publicarResult.Value!.AtoCriadorId,
            publicarResult.Value!.Id);
    }
}

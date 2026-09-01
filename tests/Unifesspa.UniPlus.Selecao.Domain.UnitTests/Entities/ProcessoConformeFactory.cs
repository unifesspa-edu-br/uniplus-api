namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Constrói o processo mínimo que passa no checklist de conformidade, para os testes que
/// precisam de um agregado publicável sem que a montagem seja o assunto deles.
/// </summary>
/// <remarks>
/// Compartilhado entre os testes de gate: cada gate recusa por uma razão, e provar isso exige
/// que todas as demais dimensões estejam satisfeitas — senão a recusa observada pode vir de
/// outro lugar. Manter uma única montagem evita que os arquivos divirjam quando o checklist
/// ganhar uma dimensão nova.
/// </remarks>
internal static class ProcessoConformeFactory
{
    internal static ReferenciaRegra Regra(string codigo, string hashSeed) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashSeed[0], 64)).Value!;

    /// <param name="declararTaxa">
    /// Falso deixa a taxa por declarar, único jeito de exercitar a recusa correspondente.
    /// </param>
    /// <param name="fase">Fase do cronograma; a omissão usa a mínima e conforme, sem recurso.</param>
    internal static ProcessoSeletivo Criar(bool declararTaxa = true, FaseCronograma? fase = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Gate", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva").Value!, peso: 1m, ordem: 1).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([
            ConfiguracaoDistribuicaoVagas.Criar(
                ofertaCursoOrigemId: Guid.CreateVersion7(),
                voBase: 40,
                pr: 1m,
                regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
                regraAjuste: null,
                referenciaDemografica: null,
                modalidades: [
                    ModalidadeSelecionada.Criar(
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
                        quantidadeDeclarada: 40).Value!,
                ]).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: [], baseadoEmEnem: false).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([fase ?? FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        if (declararTaxa)
        {
            processo.DefinirTaxaInscricao(
                ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        }

        return processo;
    }

    /// <summary>
    /// Fase mínima e conforme: agrupa etapas (há uma), produz resultado e coleta inscrição (há
    /// vagas e a origem é inscrição própria).
    /// </summary>
    internal static FaseCronograma FaseConforme(RegraRecursoFase? regraRecurso = null)
    {
        Result<FaseCronograma> fase = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: regraRecurso is null,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: regraRecurso);

        // Sem esta asserção, uma fase recusada viraria null e o teste seguiria com um
        // cronograma vazio — passando por não exercitar nada, que é o pior modo de falhar.
        fase.IsSuccess.Should().BeTrue(fase.Error?.Message);
        return fase.Value!;
    }

    internal static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;
}

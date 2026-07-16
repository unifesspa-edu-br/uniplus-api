namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do gate de publicação para as pendências do cronograma que só afloram no
/// congelamento (<c>ProcessoSeletivo.PendenciaDoCronograma</c>, Story #851 §3.4/§3.5):
/// piso mínimo derivado da origem dos candidatos (CA-11), indistinguibilidade por tipo
/// (CA-12), vagas sem fase que produz resultado (CA-13), a direção lazy da
/// bicondicional fase×etapa (CA-14) e o divisor da média sob fórmula local (CA-15).
/// </summary>
public sealed class ConformidadeCronogramaTests
{
    private static readonly string HashFixo = new('a', 64);

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    private static ConfiguracaoDistribuicaoVagas Distribuicao(int voBase) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: voBase,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla,
                    ComposicaoVagasModalidade.ResidualDoVo, null, RegraRemanejamentoModalidade.Nenhuma,
                    null, null, null, [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: voBase).Value!,
            ]).Value!;

    private static Result<FaseCronograma> Fase(
        int ordem,
        string codigo,
        bool agrupaEtapas = false,
        bool produzResultado = false,
        bool coletaInscricao = false) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Delegada,
            agrupaEtapas, permiteComplementacao: false, produzResultado,
            resultadoDefinitivo: produzResultado, coletaInscricao,
            inicio: null, fim: null,
            atoProduzidoCodigo: produzResultado ? codigo : null,
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [], regraRecurso: null);

    private static DadosEdital Dados() => DadosEdital.Criar(
        "001/2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.CreateVersion7()).Value!;

    private static Result<VersaoConfiguracao> Publicar(ProcessoSeletivo processo) => processo.Publicar(
        Dados(), "{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1", HashFixo, "teste", TimeProvider.System);

    // ── CA-11 — piso mínimo de InscricaoPropria ──

    [Fact(DisplayName = "CA-11: origem InscricaoPropria sem NENHUMA fase que colete inscrição reprova a publicação")]
    public void InscricaoPropria_SemFaseDeColeta_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta");
    }

    // ── CA-12 — indistinguibilidade por tipo ──

    [Theory(DisplayName = "CA-12: ImportacaoExterna publica sem inscrição/etapa/fórmula local — o veredicto NÃO muda com o Tipo (indistinguibilidade)")]
    [InlineData(TipoProcesso.SiSU)]
    [InlineData(TipoProcesso.PSIQ)]
    public void ImportacaoExterna_MesmaConfiguracao_TiposDiferentes_MesmoVeredicto(TipoProcesso tipo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", tipo, OrigemCandidatos.ImportacaoExterna);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases(
            [Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    // ── CA-13 — vagas sem fase que produz resultado ──

    [Fact(DisplayName = "CA-13: havendo vagas ofertadas e NENHUMA fase que produz resultado, a publicação é recusada")]
    public void ComVagas_SemFaseQueProduzResultado_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, []).Value!, PrecondicaoIfMatch.Ausente);
        // Só a fase de matrícula — nenhuma produz resultado.
        processo.DefinirCronogramaFases([Fase(1, "MATRICULA").Value!], [], PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.VagasSemFaseQueProduzResultado");
    }

    // ── CA-14 — direção lazy: etapa sem fase de avaliação ──

    [Fact(DisplayName = "CA-14: etapa pontuada declarada e NENHUMA fase agrupa etapas reprova a publicação")]
    public void Etapa_SemFaseDeAvaliacao_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        processo.DefinirEtapas(
            [EtapaProcesso.Criar("Prova", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1)], PrecondicaoIfMatch.Ausente);
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'b'),
            Regra(RegraArredondamentoCodigo.PrecisaoTruncar, 'd'), 2,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, []).Value!, PrecondicaoIfMatch.Ausente);
        // Fase que produz resultado, mas NÃO agrupa etapas.
        processo.DefinirCronogramaFases([Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.EtapaSemFaseDeAvaliacao");
    }

    // ── CA-15 — DefinirEtapas([]) aceito; divisor > 0 só sob fórmula local ──

    [Fact(DisplayName = "CA-15 (regressão): processo SEM etapa, com CLASSIFICACAO-IMPORTADA, publica — hoje um processo sem etapa não publicava (o teste falha sem o fix)")]
    public void SemEtapa_ClassificacaoImportada_Publica()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS SiSU", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        processo.DefinirEtapas([], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue(
            "Story #851 §3.5: lista vazia é estado válido — um processo sem prova (SiSU) não tem etapa");
        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirDistribuicaoVagas([Distribuicao(40)], PrecondicaoIfMatch.Ausente);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'), null, null,
            Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, 'c'), 1, []).Value!, PrecondicaoIfMatch.Ausente);
        processo.DefinirCronogramaFases([Fase(1, "RESULTADO_FINAL", produzResultado: true).Value!], [], PrecondicaoIfMatch.Ausente);

        Result<VersaoConfiguracao> resultado = Publicar(processo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "CA-15: sob FORMULA-MEDIA-PONDERADA, divisor ZERO reprova a publicação (etapas só eliminatórias, sem peso)")]
    public void FormulaLocal_DivisorZero_Reprova()
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);
        // Etapa eliminatória pura — NÃO compõe a nota (ComponeNota exige classificatória/ambas + peso).
        // O guard "NenhumaEtapaComponeNota" recusaria isso ANTES de chegarmos ao divisor — o
        // cenário aqui prova a MESMA classe de defeito por outro caminho: guard "vivo" via
        // reflection para simular um estado que hoje só seria alcançável por dado legado.
        Result definirEtapas = processo.DefinirEtapas(
            [EtapaProcesso.Criar("Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 5m, ordem: 1)],
            PrecondicaoIfMatch.Ausente);
        definirEtapas.IsFailure.Should().BeTrue(
            "pré-condição: DefinirEtapas já recusa uma lista sem NENHUMA etapa que componha a nota — " +
            "prova que o guard estrutural do domínio intercepta o divisor zero antes mesmo do gate de publicação");
        definirEtapas.Error!.Code.Should().Be("ProcessoSeletivo.NenhumaEtapaComponeNota");
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Processos de duas ofertas de curso com conjuntos de modalidades diferentes: a ampla (<c>AC</c>)
/// vale para todos, e cada modalidade de ação afirmativa, para quem responde o fato dado com o valor
/// dado. Monta o grafo congelado direto, sem passar pela configuração do processo.
/// </summary>
internal static class OfertasDeTeste
{
    public const string ModalidadeDaAmpla = "AC";

    /// <summary>O fato e o valor que fazem valer as modalidades de ação afirmativa dos testes.</summary>
    public const string FatoDaAcaoAfirmativa = "SEXO";

    public const string ValorDaAcaoAfirmativa = "FEMININO";

    public static ConfiguracaoDistribuicaoVagas Distribuicao(Guid ofertaCursoId, params string[] codigos) =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoId, voBase: 10 * codigos.Length, pr: 1m,
            ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null, referenciaDemografica: null, [.. codigos.Select(Modalidade)],
            ofertaCurso: OfertaCursoSnapshot.Criar("Medicina", "Bacharelado", "Campus Universitário de Marabá", ["MATUTINO"]).Value!).Value!;

    /// <summary>O mesmo grafo, com a distribuição de vagas dada no lugar da que ele tinha.</summary>
    public static GrafoConfiguracao ComDistribuicao(GrafoConfiguracao grafo, params ConfiguracaoDistribuicaoVagas[] distribuicao) =>
        Reconstruir(grafo, distribuicao, grafo.NumeroDeOpcoesDeCurso);

    /// <summary>O mesmo grafo, declarando quantas opções de curso a inscrição admite.</summary>
    public static GrafoConfiguracao ComOpcoesDeCurso(GrafoConfiguracao grafo, int numeroDeOpcoesDeCurso) =>
        Reconstruir(grafo, [.. grafo.DistribuicaoVagas], numeroDeOpcoesDeCurso);

    private static GrafoConfiguracao Reconstruir(
        GrafoConfiguracao grafo, ConfiguracaoDistribuicaoVagas[] distribuicao, int? numeroDeOpcoesDeCurso) =>
        new(
            grafo.Etapas, grafo.OfertaAtendimento, distribuicao,
            grafo.BonusRegional, grafo.CriteriosDesempate, grafo.Classificacao,
            grafo.CronogramaFases, grafo.DocumentosExigidos, grafo.NosExigencia, grafo.ReferenciaTemporalFatos,
            fatosColetados: grafo.FatosColetados,
            formularios: grafo.Formularios,
            termosExigidos: grafo.TermosExigidos,
            gruposColetados: grafo.GruposColetados,
            numeroDeOpcoesDeCurso: numeroDeOpcoesDeCurso);

    private static ModalidadeSelecionada Modalidade(string codigo) =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), codigo, null,
            codigo == ModalidadeDaAmpla ? NaturezaLegalModalidade.Ampla : NaturezaLegalModalidade.AcaoAfirmativa,
            ComposicaoVagasModalidade.SuplementarAoTotal, null, RegraRemanejamentoModalidade.Nenhuma,
            null, null, null,
            codigo == ModalidadeDaAmpla
                ? PredicadoDnf.SemClausula
                : PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(
                    FatoDaAcaoAfirmativa, Operador.Igual, JsonSerializer.SerializeToElement(ValorDaAcaoAfirmativa)).Value!)]).Value!,
            null, "base legal", quantidadeDeclarada: 10).Value!;
}

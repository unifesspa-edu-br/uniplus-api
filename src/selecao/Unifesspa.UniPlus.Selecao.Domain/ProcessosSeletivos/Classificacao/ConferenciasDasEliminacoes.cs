namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As recusas da lista de eliminações de uma classificação local: a que depende do ENEM, a que
/// repete uma declaração que não elimina ninguém a mais e o corte em área contra o quadro de
/// pesos por área.
/// </summary>
internal static class ConferenciasDasEliminacoes
{
    internal static List<FieldError> Conferir(
        IReadOnlyList<RegraEliminacao> regras,
        ReferenciaRegra regraCalculo,
        bool baseadoEmEnem,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro)
    {
        List<FieldError> erros = ConferirDependenciaDoEnem(regras, baseadoEmEnem);

        // Na classificação importada toda eliminação tem de sair, e a recusa de um item seria
        // só consequência.
        if (!ConferenciasDoCalculoDaNota.EhImportada(regraCalculo))
        {
            erros.AddRange(ConferirFaltaEmDiaDeProvaRepetida(regras));
            erros.AddRange(ConferirAusenciaRepetida(regras));
            erros.AddRange(ConferirCortesEmArea(regras, QuadroQueOsCortesConferem(regraCalculo, baseadoEmEnem, quadro)));
        }

        return erros;
    }

    // Eixo ortogonal a RegraCalculo (que só distingue cálculo local de classificação importada):
    // a regra que só tem sentido sobre os dados do ENEM do candidato exige a classificação
    // baseada em ENEM.
    private static List<FieldError> ConferirDependenciaDoEnem(IReadOnlyList<RegraEliminacao> regras, bool baseadoEmEnem) =>
        regras.Any(static regra => regra.Parametros.ExigeEnem()) && !baseadoEmEnem
            ? [new("regrasEliminacao", new DomainError(
                "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem",
                "Uma ou mais regras de eliminação só se aplicam quando a classificação está configurada como baseada em ENEM."))]
            : [];

    private static IReadOnlyList<GrupoPesoAreaEnemCongelado>? QuadroQueOsCortesConferem(
        ReferenciaRegra regraCalculo,
        bool baseadoEmEnem,
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro) =>
        ConferenciasDoQuadroDePesosPorArea.Exige(regraCalculo, baseadoEmEnem) && quadro.Count > 0 ? quadro : null;

    // Sem parâmetros, uma segunda declaração da falta em dia de prova repete a primeira e não elimina
    // ninguém a mais: cada repetição é recusada no próprio item. A recusa do ENEM desmarcado não
    // suprime: o operador pode corrigi-la marcando o ENEM, e a repetição continua sendo violação
    // independente (ADR-0125).
    private static List<FieldError> ConferirFaltaEmDiaDeProvaRepetida(IReadOnlyList<RegraEliminacao> regras) =>
        regras
            .Select(static (regra, indice) => (regra, indice))
            .Where(static item => item.regra.Parametros is ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem)
            .Skip(1)
            .Select(static item => new FieldError($"regrasEliminacao[{item.indice}]", new DomainError(
                "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida",
                "A eliminação por falta em dia de prova do ENEM só pode ser declarada uma vez.")))
            .ToList();

    // A ausência é por etapa: duas etapas distintas são válidas, a mesma duas vezes não elimina
    // ninguém a mais.
    private static List<FieldError> ConferirAusenciaRepetida(IReadOnlyList<RegraEliminacao> regras)
    {
        List<FieldError> erros = [];
        HashSet<Guid> etapasComAusenciaDeclarada = [];
        for (int indice = 0; indice < regras.Count; indice++)
        {
            if (regras[indice].Parametros is ParametrosEliminacaoPorAusenciaNaEtapa ausencia
                && !etapasComAusenciaDeclarada.Add(ausencia.EtapaRef))
            {
                erros.Add(new($"regrasEliminacao[{indice}]", new DomainError(
                    "ConfiguracaoClassificacao.AusenciaNaEtapaRepetida",
                    "A eliminação por ausência só pode ser declarada uma vez para cada etapa.")));
            }
        }

        return erros;
    }

    // O corte em área cita a área pelo código do quadro de pesos por área. Com dois mínimos na
    // mesma área, o menor não eliminaria ninguém que o maior já não eliminasse: cada repetição
    // é recusada no próprio item. Com quadro congelado, a área tem de estar em todo grupo dele,
    // que é a lista de áreas do processo; a mesma lista vale para o desempate. O quadro é nulo
    // quando a classificação não congela quadro (fora da média ponderada do ENEM, ou com o
    // quadro já recusado): a área não tem contra o que ser conferida.
    private static List<FieldError> ConferirCortesEmArea(
        IReadOnlyList<RegraEliminacao> regras,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro)
    {
        List<FieldError> erros = [];
        HashSet<string>? aceitas = quadro is null ? null : ConferenciasDoQuadroDePesosPorArea.AreasEmTodosOsGrupos(quadro);
        HashSet<string> comCorte = new(StringComparer.Ordinal);
        for (int indice = 0; indice < regras.Count; indice++)
        {
            if (regras[indice].Parametros is ParametrosEliminacaoPorCorteEmAreaDoEnem corte
                && RecusaDoCorte(indice, corte, comCorte, quadro, aceitas) is { } recusa)
            {
                erros.Add(recusa);
            }
        }

        return erros;
    }

    private static FieldError? RecusaDoCorte(
        int indice,
        ParametrosEliminacaoPorCorteEmAreaDoEnem corte,
        HashSet<string> comCorte,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro,
        HashSet<string>? aceitas) =>
        comCorte.Add(corte.AreaCodigo)
            ? RecusaDaAreaForaDoQuadro(indice, corte, quadro, aceitas)
            : new($"regrasEliminacao[{indice}]", new DomainError(
                "ConfiguracaoClassificacao.CorteEmAreaRepetido",
                $"A área {corte.AreaCodigo} já tem corte; declare no máximo um corte por área."));

    private static FieldError? RecusaDaAreaForaDoQuadro(
        int indice,
        ParametrosEliminacaoPorCorteEmAreaDoEnem corte,
        IReadOnlyList<GrupoPesoAreaEnemCongelado>? quadro,
        HashSet<string>? aceitas) =>
        aceitas is null || aceitas.Contains(corte.AreaCodigo)
            ? null
            : new($"regrasEliminacao[{indice}].areaCodigo", new DomainError(
                "ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro",
                $"A área {corte.AreaCodigo} do corte não está em todos os grupos do quadro de pesos por área da classificação. Áreas aceitas: {ConferenciasDoQuadroDePesosPorArea.ListarAreas(quadro!, aceitas)}."));
}

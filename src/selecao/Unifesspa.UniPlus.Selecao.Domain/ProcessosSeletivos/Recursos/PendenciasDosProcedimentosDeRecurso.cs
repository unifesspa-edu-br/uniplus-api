namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que falta nos procedimentos de recurso para o processo ser congelado: toda janela de recurso, de etapa ou de fase,
/// declara procedimento para cada tipo de decisão que o lugar produz, e o que a decisão recorrida cobre fica inteiro
/// com procedimento. Sem procedimento, o recurso chegaria sem dizer como será revisado.
/// </summary>
internal static class PendenciasDosProcedimentosDeRecurso
{
    internal static DomainError? PendenciaDosProcedimentosDeRecurso(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        bool processoTemNotaDoEnem = estado.Etapas.Any(static etapa => etapa.DeclaraNotaDoEnem);

        return DasEtapas(estado, processoTemNotaDoEnem).Concat(DasFases(estado, processoTemNotaDoEnem)).FirstOrDefault();
    }

    private static IEnumerable<DomainError> DasEtapas(EstadoDaConfiguracao estado, bool processoTemNotaDoEnem) =>
        estado.Etapas.SelectMany(etapa => etapa.Recursos.SelectMany(recurso => Ausentes(
            recurso.Procedimentos,
            new ContextoDosProcedimentosDeRecurso(
                TiposDoLugar(etapa.TiposDeDecisao), etapa.ItensDeAvaliacao.Select(static item => item.Id).ToHashSet(), processoTemNotaDoEnem),
            CodigosDoProcedimentoDeRecurso.DaEtapa,
            $"A etapa '{etapa.Nome}'")));

    private static IEnumerable<DomainError> DasFases(EstadoDaConfiguracao estado, bool processoTemNotaDoEnem) =>
        estado.CronogramaFases
            .Where(static fase => fase.RegraRecurso is not null)
            .SelectMany(fase => Ausentes(
                fase.RegraRecurso!.Procedimentos,
                new ContextoDosProcedimentosDeRecurso(TiposDoLugar(fase.TiposDeDecisao), new HashSet<Guid>(), processoTemNotaDoEnem),
                CodigosDoProcedimentoDeRecurso.DaFase,
                $"A fase '{fase.Codigo}'"));

    private static HashSet<TipoMotivoDeDecisao> TiposDoLugar(IEnumerable<string> codigos) =>
        [.. codigos.Select(TipoMotivoDeDecisaoCodigo.FromCodigo)];

    private static IEnumerable<DomainError> Ausentes(
        ProcedimentosDeRecurso declarados, ContextoDosProcedimentosDeRecurso contexto, CodigosDoProcedimentoDeRecurso codigos, string lugar) =>
        CoberturaDosProcedimentosDeRecurso.TiposSemProcedimento(declarados, contexto).Select(tipo => new DomainError(
            codigos.Ausente,
            $"{lugar} tem janela de recurso sem procedimento para a decisão do tipo '{tipo.ToCodigo()}'; declare como o recurso será revisado."));
}

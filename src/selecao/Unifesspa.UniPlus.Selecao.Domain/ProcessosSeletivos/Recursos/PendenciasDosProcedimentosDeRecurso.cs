namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Tudo o que falta nos procedimentos de recurso para o processo ser congelado, uma causa por tipo de decisão sem procedimento, nas etapas (<c>etapas[i].recursos</c>) e depois nas fases (<c>fases[i].regraRecurso</c>), em posição de entrada: toda janela de recurso, de etapa ou de fase,
/// declara procedimento para cada tipo de decisão que o lugar produz, e o que a decisão recorrida cobre fica inteiro
/// com procedimento. Sem procedimento, o recurso chegaria sem dizer como será revisado.
/// </summary>
internal static class PendenciasDosProcedimentosDeRecurso
{
    internal static IReadOnlyList<FieldError> CausasDosProcedimentosDeRecurso(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        bool processoTemNotaDoEnem = configuracao.Avaliacao.Etapas.Any(static etapa => etapa.DeclaraNotaDoEnem);

        return [.. DasEtapas(configuracao, processoTemNotaDoEnem), .. DasFases(configuracao, processoTemNotaDoEnem)];
    }

    private static IEnumerable<FieldError> DasEtapas(ConfiguracaoDeclarada configuracao, bool processoTemNotaDoEnem) =>
        configuracao.Avaliacao.Etapas.SelectMany((etapa, posicao) => etapa.Recursos
            .SelectMany(recurso => Ausentes(
                recurso.Procedimentos,
                new ContextoDosProcedimentosDeRecurso(
                    TiposDoLugar(etapa.TiposDeDecisao), etapa.ItensDeAvaliacao.Select(static item => item.Id).ToHashSet(), processoTemNotaDoEnem),
                CodigosDoProcedimentoDeRecurso.DaEtapa,
                $"A etapa '{etapa.Nome}'"))
            .Select(erro => new FieldError($"etapas[{posicao}].recursos", erro)));

    private static IEnumerable<FieldError> DasFases(ConfiguracaoDeclarada configuracao, bool processoTemNotaDoEnem) =>
        configuracao.Cronograma.CronogramaFases.SelectMany((fase, posicao) => fase.RegraRecurso is null
            ? []
            : Ausentes(
                fase.RegraRecurso.Procedimentos,
                new ContextoDosProcedimentosDeRecurso(TiposDoLugar(fase.TiposDeDecisao), new HashSet<Guid>(), processoTemNotaDoEnem),
                CodigosDoProcedimentoDeRecurso.DaFase,
                $"A fase '{fase.Codigo}'")
                .Select(erro => new FieldError($"fases[{posicao}].regraRecurso", erro)));

    private static HashSet<TipoMotivoDeDecisao> TiposDoLugar(IEnumerable<string> codigos) =>
        [.. codigos.Select(TipoMotivoDeDecisaoCodigo.FromCodigo)];

    private static IEnumerable<DomainError> Ausentes(
        ProcedimentosDeRecurso declarados, ContextoDosProcedimentosDeRecurso contexto, CodigosDoProcedimentoDeRecurso codigos, string lugar) =>
        CoberturaDosProcedimentosDeRecurso.TiposSemProcedimento(declarados, contexto).Select(tipo => new DomainError(
            codigos.Ausente,
            $"{lugar} tem janela de recurso sem procedimento para a decisão do tipo '{tipo.ToCodigo()}'; declare como o recurso será revisado."));
}

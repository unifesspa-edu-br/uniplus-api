namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
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
            : DaFase(configuracao, fase, posicao, processoTemNotaDoEnem));

    // A fase conferida pelas mesmas faltas da declaração (coerência, tipo, alvo). A cobertura não se avalia com procedimento que a leitura recusou:
    // não se sabe se o recusado cobria o tipo, e a causa é a da leitura.
    private static IEnumerable<FieldError> DaFase(ConfiguracaoDeclarada configuracao, FaseCronograma fase, int posicao, bool processoTemNotaDoEnem)
    {
        ContextoDosProcedimentosDeRecurso contexto = new(TiposDoLugar(fase.TiposDeDecisao), new HashSet<Guid>(), processoTemNotaDoEnem);
        ProcedimentosDeRecurso declarados = fase.RegraRecurso!.Procedimentos;
        IReadOnlyList<FieldError> faltas = ConferenciaDosProcedimentosDeRecurso.Faltas(
            declarados.Itens, contexto, CodigosDoProcedimentoDeRecurso.DaFase, $"fases[{posicao}].{FaseDeclarada.ListaDosProcedimentosDeRecurso}");
        IEnumerable<FieldError> ausentes = configuracao.Origem.TemParteRecusadaEm($"fases[{posicao}].{FaseDeclarada.ListaDosProcedimentosDeRecurso}")
            ? []
            : Ausentes(declarados, contexto, CodigosDoProcedimentoDeRecurso.DaFase, "A fase")
                .Select(erro => new FieldError($"fases[{posicao}].{FaseDeclarada.CampoDaRegraDeRecurso}", erro));

        return [.. faltas, .. ausentes];
    }

    /// <summary>
    /// Se a leitura recusou a regra de recurso de alguma fase ou a lista dos procedimentos dela: o que falta nos procedimentos não se
    /// pode afirmar nem negar, e o item do painel fica pendente em vez de aprovado.
    /// </summary>
    internal static bool RecusadosPelaLeitura(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return configuracao.Origem.TemParteRecusadaEm(CronogramaDoProcesso.ListaDasFases)
            || Enumerable.Range(0, configuracao.Cronograma.CronogramaFases.Count).Any(posicao =>
                configuracao.Origem.TemParteRecusadaEm($"fases[{posicao}].{FaseDeclarada.CampoDaRegraDeRecurso}")
                || configuracao.Origem.TemParteRecusadaEm($"fases[{posicao}].{FaseDeclarada.ListaDosProcedimentosDeRecurso}"));
    }

    private static HashSet<TipoMotivoDeDecisao> TiposDoLugar(IEnumerable<string> codigos) =>
        [.. codigos.Select(TipoMotivoDeDecisaoCodigo.FromCodigo)];

    private static IEnumerable<DomainError> Ausentes(
        ProcedimentosDeRecurso declarados, ContextoDosProcedimentosDeRecurso contexto, CodigosDoProcedimentoDeRecurso codigos, string lugar) =>
        CoberturaDosProcedimentosDeRecurso.TiposSemProcedimento(declarados, contexto).Select(_ => new DomainError(
            codigos.Ausente,
            $"{lugar} tem uma decisão sem procedimento de recurso; declare como o recurso será revisado."));
}

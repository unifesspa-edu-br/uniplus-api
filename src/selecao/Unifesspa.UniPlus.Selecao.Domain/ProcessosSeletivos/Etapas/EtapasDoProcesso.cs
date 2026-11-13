namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As invariantes de um conjunto de etapas, as que valem em qualquer conjunto: na edição e na restauração.
/// </summary>
/// <remarks>
/// É uma vista: a lista é copiada, mas as etapas são as entidades vivas, de modo que o resultado
/// de cada consulta vale para o estado delas no momento da chamada.
/// </remarks>
internal sealed class EtapasDoProcesso
{
    private readonly List<EtapaProcesso> _etapas;

    internal EtapasDoProcesso(IEnumerable<EtapaProcesso> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);

        _etapas = [.. etapas];
    }

    internal bool Contem(Guid etapaId) => _etapas.Exists(e => e.Id == etapaId);

    /// <summary>
    /// A recusa do conjunto, na ordem em que orienta quem edita: ordem repetida, etapa de nota do ENEM e
    /// nenhuma etapa que componha a nota. Uma lista vazia é válida: o processo sem prova (classificação
    /// importada) não tem etapa.
    /// </summary>
    /// <param name="classificacao">
    /// <see langword="null"/> enquanto a classificação não foi gravada: a coerência com ela ainda não é conferível.
    /// </param>
    internal DomainError? RecusaDaColecao(ConfiguracaoClassificacao? classificacao) =>
        OrdemRepetida()
        ?? RecusaDaEtapaDoEnem(classificacao)
        ?? NenhumaEtapaComponeNota();

    /// <summary>
    /// A etapa que declara nota de origem no ENEM (<see cref="EtapaProcesso.DeclaraNotaDoEnem"/>):
    /// é no máximo uma por processo, porque a nota do ENEM é uma só por candidato; compõe a
    /// média, porque é para gerar a nota final que o processo baseado em ENEM a declara; não
    /// tem banca, porque ninguém atribui a nota e quem julga o recurso é declarado na fase de
    /// recursos; e não declara produto nem recurso ancorado em ato, porque o resultado é
    /// publicado pela fase e o recurso coletivo sobre a lista geral é o da regra de recurso da
    /// fase — uma janela por fato. O parecer individual é admitido, não exigido: nesta etapa
    /// ele é o espelho do cálculo gerado pelo sistema (notas por área, pesos do grupo
    /// congelado, média), e é dele que corre o recurso individual ancorado na ciência. Só
    /// convive com classificação baseada em ENEM <b>e</b> calculada pela média ponderada,
    /// que é a fórmula em que ela entra como componente. A coerência com a classificação sai primeiro
    /// porque é ela que decide se a etapa pode existir no processo: recusar antes por outra causa
    /// levaria o operador a corrigir uma etapa que, sob esta classificação, ele teria de remover de todo modo.
    /// </summary>
    /// <param name="classificacao">
    /// <see langword="null"/> enquanto a classificação não foi gravada: a coerência com ela ainda não é conferível.
    /// </param>
    internal DomainError? RecusaDaEtapaDoEnem(ConfiguracaoClassificacao? classificacao)
    {
        List<EtapaProcesso> etapasDoEnem = [.. _etapas.Where(static e => e.DeclaraNotaDoEnem)];
        if (etapasDoEnem.Count == 0)
        {
            return null;
        }

        EtapaProcesso etapaDoEnem = etapasDoEnem[0];
        return ClassificacaoSemNotaDoEnem(etapaDoEnem, classificacao)
            ?? MaisDeUmaEtapaDoEnem(etapasDoEnem)
            ?? EtapaDoEnemForaDaMedia(etapaDoEnem)
            ?? EtapaDoEnemComBanca(etapaDoEnem)
            ?? EtapaDoEnemComProdutoOuRecursoEmAto(etapaDoEnem);
    }

    /// <summary>
    /// As violações do fechamento do ciclo recursal nas etapas, uma por matéria deixada aberta.
    /// </summary>
    /// <remarks>
    /// A etapa não tem fase concluinte: o ciclo dela fecha dentro dela mesma. O preliminar de um
    /// tipo de ato exige o definitivo do mesmo tipo na mesma etapa, pelo mesmo pareamento que as
    /// fases usam (<see cref="CicloRecursal"/>).
    /// </remarks>
    internal List<FieldError> ViolacoesDoCiclo()
    {
        List<FieldError> violacoes = [];

        for (int indice = 0; indice < _etapas.Count; indice++)
        {
            EtapaProcesso etapa = _etapas[indice];
            violacoes.AddRange(etapa.TiposComCicloAberto().Select(tipo => new FieldError($"etapas[{indice}].produtos", new DomainError(
                ProcessoSeletivoErrorCodes.EtapaComPreliminarSemDefinitivo,
                $"A etapa '{etapa.Nome}' publica o resultado preliminar do ato '{tipo}' e não publica o definitivo do mesmo tipo — a etapa não tem fase concluinte, e o ciclo dela fecha dentro dela."))));
        }

        return violacoes;
    }

    /// <summary>
    /// A ausência à etapa do ENEM já é a falta em dia de prova do ENEM: a regra por etapa só
    /// vale para as demais.
    /// </summary>
    internal DomainError? RecusaDaAusenciaNaEtapaDoEnem(ArgsRegraEliminacao args) =>
        args is ArgsElimAusenciaNaEtapa ausencia && _etapas.Any(e => e.Id == ausencia.EtapaRef && e.DeclaraNotaDoEnem)
            ? new DomainError(
                "ProcessoSeletivo.AusenciaNaEtapaDoEnem",
                "A eliminação por ausência não se aplica à etapa de nota do ENEM, cuja falta é tratada pela eliminação por falta em dia de prova do ENEM.")
            : null;

    private DomainError? OrdemRepetida()
    {
        List<int> ordensInformadas = [.. _etapas.Where(e => e.Ordem.HasValue).Select(e => e.Ordem!.Value)];
        return ordensInformadas.Distinct().Count() != ordensInformadas.Count
            ? new DomainError(
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "Cada etapa deve ter uma ordem única dentro do processo.")
            : null;
    }

    // Sem etapa que componha a nota o divisor da média é zero, e a nota final dividiria por zero.
    private DomainError? NenhumaEtapaComponeNota() =>
        _etapas.Count > 0 && !_etapas.Any(e => e.ComponeNota)
            ? new DomainError(
                "ProcessoSeletivo.NenhumaEtapaComponeNota",
                "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.")
            : null;

    private static DomainError? ClassificacaoSemNotaDoEnem(EtapaProcesso etapaDoEnem, ConfiguracaoClassificacao? classificacao) =>
        classificacao is null || classificacao.CalculaPelosPesosPorAreaDoEnem
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemSemClassificacaoEnem",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e exige classificação baseada em ENEM, calculada pela média ponderada.");

    private static DomainError? MaisDeUmaEtapaDoEnem(List<EtapaProcesso> etapasDoEnem) =>
        etapasDoEnem.Count > 1
            ? new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemDuplicada",
                $"O processo admite uma única etapa de nota do ENEM, e declara {etapasDoEnem.Count}: {string.Join(", ", etapasDoEnem.Select(static e => $"\"{e.Nome}\""))}.")
            : null;

    private static DomainError? EtapaDoEnemForaDaMedia(EtapaProcesso etapaDoEnem) =>
        etapaDoEnem.ComponeNota
            ? null
            : new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e precisa compor a média: declare caráter classificatória ou ambas e um peso.");

    private static DomainError? EtapaDoEnemComBanca(EtapaProcesso etapaDoEnem) =>
        etapaDoEnem.Bancas.Count > 0
            ? new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemComBanca",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e não admite banca: ninguém atribui a nota, e quem julga o recurso é declarado na fase de recursos.")
            : null;

    private static DomainError? EtapaDoEnemComProdutoOuRecursoEmAto(EtapaProcesso etapaDoEnem) =>
        etapaDoEnem.Produtos.Count > 0 || etapaDoEnem.Recursos.Any(static r => r.Ancora == AncoraDoRecurso.AtoPublicado)
            ? new DomainError(
                "ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto",
                $"A etapa \"{etapaDoEnem.Nome}\" tem a nota vinda do ENEM e não declara produto nem recurso ancorado em ato: o resultado é publicado pela fase, e o recurso coletivo sobre a lista geral é o da regra de recurso da fase. Na etapa cabe só o recurso individual, ancorado na ciência do candidato.")
            : null;
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O que cada origem de candidatos exige da fase que homologa as inscrições (UNI-REQ-0264, 0340 e 0342).
/// A fase é achada pela capacidade que o edital configurou nela, nunca pelo código canônico, pelo tipo de
/// processo, pelo uso do exame por nome, pela taxa ou pela existência de etapa: quem chama diz quais fases
/// têm a capacidade.
/// </summary>
internal static class ExigenciaDaHomologacaoPorOrigem
{
    /// <summary>
    /// A primeira recusa da exigência, ou nulo quando a origem está atendida. A falta da fase vem antes da
    /// falta do resultado, e a falta da fase de coleta é conferida antes desta exigência.
    /// </summary>
    internal static DomainError? Pendencia(
        OrigemCandidatos origem,
        bool usaNotaImportadaDeExame,
        IReadOnlyCollection<FaseCronograma> fases,
        Func<FaseCronograma, bool> fasePodeHomologar)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(fasePodeHomologar);

        FaseCronograma[] homologadoras = [.. fases.Where(fasePodeHomologar).OrderBy(static fase => fase.Ordem)];

        return origem switch
        {
            OrigemCandidatos.ImportacaoExterna => PendenciaDaImportacaoExterna(homologadoras),
            _ when homologadoras.Length == 0 => FaseAusenteDaInscricaoPropria(usaNotaImportadaDeExame),
            _ => null,
        };
    }

    private static DomainError FaseAusenteDaInscricaoPropria(bool usaNotaImportadaDeExame) => usaNotaImportadaDeExame
        ? new DomainError(
            ProcessoSeletivoErrorCodes.ImportacaoDeExameSemHomologacaoDasInscricoes,
            "O processo importa a nota de um exame, e nenhuma fase do cronograma homologa as inscrições.")
        : new DomainError(
            ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes,
            "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma homologa as inscrições.");

    // Basta uma fase completa; sem nenhuma, a recusa é a da primeira na ordem do cronograma, para o veredicto não depender da ordem da coleção.
    private static DomainError? PendenciaDaImportacaoExterna(FaseCronograma[] homologadoras)
    {
        if (homologadoras.Length == 0)
        {
            return new DomainError(
                ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes,
                "O processo importa candidatos de fora, e nenhuma fase do cronograma homologa as inscrições.");
        }

        DomainError?[] pendencias = [.. homologadoras.Select(PendenciaDoCicloDaFase)];

        return pendencias.Contains(null) ? null : pendencias[0];
    }

    private static DomainError? PendenciaDoCicloDaFase(FaseCronograma fase)
    {
        string[] resultadosFechados = [.. fase.TiposFechadosNaPropriaFase()];
        if (resultadosFechados.Length == 0)
        {
            return new DomainError(
                HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado,
                $"A fase '{fase.Codigo}' homologa as inscrições e precisa declarar o resultado preliminar e o definitivo do mesmo resultado.");
        }

        return JanelaAncoradaEmResultadoFechado(fase, resultadosFechados)
            ? null
            : new DomainError(
                ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso,
                $"A fase '{fase.Codigo}' homologa as inscrições e precisa da janela de recurso entre o resultado preliminar e o definitivo.");
    }

    private static bool JanelaAncoradaEmResultadoFechado(FaseCronograma fase, string[] resultadosFechados) =>
        fase.RegraRecurso is { } regra
        && fase.Produtos.FirstOrDefault(produto => produto.Id == regra.ProdutoAncoraId) is { } ancora
        && resultadosFechados.Contains(ancora.AtoCodigo, StringComparer.Ordinal);
}

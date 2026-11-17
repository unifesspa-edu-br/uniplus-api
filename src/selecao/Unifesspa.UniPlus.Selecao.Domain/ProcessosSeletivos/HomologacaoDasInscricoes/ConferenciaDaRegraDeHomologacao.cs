namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// A regra de homologação que a fase declara, conferida contra a própria fase e o cronograma. Nada aqui lê o
/// código canônico da fase: a fase que homologa é a que declara o tipo de decisão e a regra. O prefixo do
/// caminho da fase é de quem chama.
/// </summary>
internal static class ConferenciaDaRegraDeHomologacao
{
    /// <summary>
    /// As faltas da regra, na ordem tipo de decisão declarado, resultado e confirmação do INEP; vazio quando
    /// a regra está em ordem. O enquadramento no vocabulário de fatos e os motivos de cada condição dependem
    /// de cadastros e são conferidos fora do domínio.
    /// </summary>
    internal static IReadOnlyList<FieldError> Faltas(
        RegraDeHomologacaoDasInscricoes regra,
        FaseCronograma fase,
        IReadOnlyCollection<FaseCronograma> cronograma,
        bool usaNotaImportadaDeExame)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(fase);
        ArgumentNullException.ThrowIfNull(cronograma);

        return
        [
            .. FaltaDoTipo(fase),
            .. FaltaDoResultado(regra, fase, cronograma),
            .. FaltaDaConfirmacaoDoInep(regra, usaNotaImportadaDeExame),
        ];
    }

    private static IEnumerable<FieldError> FaltaDoTipo(FaseCronograma fase)
    {
        if (!fase.TiposDeDecisao.Contains(TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes, StringComparer.Ordinal))
        {
            yield return TipoNaoDeclarado();
        }
    }

    private static IEnumerable<FieldError> FaltaDoResultado(
        RegraDeHomologacaoDasInscricoes regra, FaseCronograma fase, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        if (!ResultadoTemDefinitivo(regra, fase, cronograma))
        {
            yield return ResultadoNaoDeclarado();
        }
    }

    private static IEnumerable<FieldError> FaltaDaConfirmacaoDoInep(RegraDeHomologacaoDasInscricoes regra, bool usaNotaImportadaDeExame)
    {
        if (usaNotaImportadaDeExame && !TodaAlternativaTemConfirmacaoDoInep(regra))
        {
            yield return ConfirmacaoDoInepAusente();
        }
    }

    // Toda alternativa precisa da confirmação do INEP aplicada a todos os candidatos: uma alternativa sem ela, ou
    // com ela restrita por enquadramento, homologaria quem usa o exame e não tem resultado no retorno. Se a
    // leitura do edital for "basta uma", muda só este predicado.
    private static bool TodaAlternativaTemConfirmacaoDoInep(RegraDeHomologacaoDasInscricoes regra) =>
        regra.Alternativas.Count > 0
        && regra.Alternativas.All(static alternativa => alternativa.Condicoes.Any(ConfirmaParaTodos));

    private static bool ConfirmaParaTodos(CondicaoDeHomologacao condicao) =>
        condicao.Tipo == TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep && condicao.Enquadramento is null;

    private static bool ResultadoTemDefinitivo(
        RegraDeHomologacaoDasInscricoes regra, FaseCronograma fase, IReadOnlyCollection<FaseCronograma> cronograma) =>
        regra.ResultadoAtoCodigo is { } resultado
        && fase.Produtos.Any(produto => produto.Papel is not null && produto.AtoCodigo == resultado)
        && (fase.PublicaResultadoDefinitivoDe(resultado) || ConcluinteTemDefinitivo(resultado, fase, cronograma));

    private static bool ConcluinteTemDefinitivo(
        string resultado, FaseCronograma fase, IReadOnlyCollection<FaseCronograma> cronograma) =>
        fase.FaseConcluinteCodigo is { } concluinte
        && cronograma.Any(outra => string.Equals(outra.Codigo, concluinte, StringComparison.Ordinal)
            && outra.PublicaResultadoDefinitivoDe(resultado));

    private static FieldError TipoNaoDeclarado() => new(FaseDeclarada.CampoDaRegraDeHomologacao, new DomainError(
        MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
        "A fase declara uma regra de homologação das inscrições, mas não declara esse tipo de decisão."));

    private static FieldError ResultadoNaoDeclarado() => new($"{FaseDeclarada.CampoDaRegraDeHomologacao}.resultadoAtoCodigo", new DomainError(
        HomologacaoDasInscricoesErrorCodes.ResultadoNaoDeclarado,
        "O resultado indicado pela regra deve ser publicado nesta fase, com resultado definitivo nela ou na fase que a conclui."));

    private static FieldError ConfirmacaoDoInepAusente() => new($"{FaseDeclarada.CampoDaRegraDeHomologacao}.alternativas", new DomainError(
        HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente,
        "O processo importa a nota de um exame, e toda alternativa da regra precisa da condição de resultado no retorno do INEP."));
}

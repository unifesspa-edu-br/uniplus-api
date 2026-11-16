namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A regra de homologação que a fase declara, conferida contra a própria fase e o cronograma. Nada aqui lê o
/// código canônico da fase: a fase que homologa é a que declara o tipo de decisão e a regra. O prefixo do
/// caminho da fase é de quem chama.
/// </summary>
internal static class ConferenciaDaRegraDeHomologacao
{
    private const string CampoDaRegra = "regraDeHomologacao";

    /// <summary>
    /// As faltas da regra, na ordem tipo de decisão declarado, resultado e confirmação do INEP; vazio quando
    /// a regra está em ordem. O enquadramento no vocabulário de fatos e os motivos de cada condição dependem
    /// de cadastros e são conferidos fora do domínio.
    /// </summary>
    internal static IReadOnlyList<FieldError> Faltas(
        RegraDeHomologacaoDasInscricoes regra,
        FaseCronograma fase,
        IReadOnlyCollection<string> tiposDeDecisaoDaFase,
        IReadOnlyCollection<FaseCronograma> cronograma,
        bool usaNotaImportadaDeExame)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(fase);
        ArgumentNullException.ThrowIfNull(tiposDeDecisaoDaFase);
        ArgumentNullException.ThrowIfNull(cronograma);

        return
        [
            .. FaltaDoTipo(tiposDeDecisaoDaFase, fase),
            .. FaltaDoResultado(regra, fase, cronograma),
            .. FaltaDaConfirmacaoDoInep(regra, usaNotaImportadaDeExame),
        ];
    }

    private static IEnumerable<FieldError> FaltaDoTipo(IReadOnlyCollection<string> tiposDeDecisaoDaFase, FaseCronograma fase)
    {
        if (!tiposDeDecisaoDaFase.Contains(TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes, StringComparer.Ordinal))
        {
            yield return TipoNaoDeclarado(fase);
        }
    }

    private static IEnumerable<FieldError> FaltaDoResultado(
        RegraDeHomologacaoDasInscricoes regra, FaseCronograma fase, IReadOnlyCollection<FaseCronograma> cronograma)
    {
        if (!ResultadoTemDefinitivo(regra, fase, cronograma))
        {
            yield return ResultadoNaoDeclarado(fase, regra.ResultadoAtoCodigo);
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
        fase.Produtos.Any(produto => produto.Papel is not null && produto.AtoCodigo == regra.ResultadoAtoCodigo)
        && (fase.PublicaResultadoDefinitivoDe(regra.ResultadoAtoCodigo) || ConcluinteTemDefinitivo(regra, fase, cronograma));

    private static bool ConcluinteTemDefinitivo(
        RegraDeHomologacaoDasInscricoes regra, FaseCronograma fase, IReadOnlyCollection<FaseCronograma> cronograma) =>
        fase.FaseConcluinteCodigo is { } concluinte
        && cronograma.Any(outra => string.Equals(outra.Codigo, concluinte, StringComparison.Ordinal)
            && outra.PublicaResultadoDefinitivoDe(regra.ResultadoAtoCodigo));

    private static FieldError TipoNaoDeclarado(FaseCronograma fase) => new(CampoDaRegra, new DomainError(
        MotivoDeDecisaoErrorCodes.TipoDeDecisaoNaoDeclarado,
        $"A fase '{fase.Codigo}' declara a regra de homologação das inscrições, mas não declara o tipo de decisão {TipoMotivoDeDecisaoCodigo.HomologacaoDasInscricoes}."));

    private static FieldError ResultadoNaoDeclarado(FaseCronograma fase, string resultado) => new($"{CampoDaRegra}.resultadoAtoCodigo", new DomainError(
        HomologacaoInscricaoErrorCodes.ResultadoNaoDeclarado,
        $"O resultado '{resultado}' que a regra aponta precisa ser produto da fase '{fase.Codigo}', com definitivo nela ou na fase que a conclui."));

    private static FieldError ConfirmacaoDoInepAusente() => new($"{CampoDaRegra}.alternativas", new DomainError(
        HomologacaoInscricaoErrorCodes.ConfirmacaoDoInepAusente,
        "O processo importa a nota de um exame, e toda alternativa da regra precisa da condição de resultado no retorno do INEP."));
}

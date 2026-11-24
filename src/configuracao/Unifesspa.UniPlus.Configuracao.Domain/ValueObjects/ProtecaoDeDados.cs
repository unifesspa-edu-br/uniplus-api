namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A proteção que o tratamento de um dado exige: classificação na escala única, finalidade e
/// hipótese legal da LGPD (ADR-0081, ADR-0136). É a mesma regra do fato do candidato e do tipo de
/// documento: o mesmo vocabulário, o mesmo limite de finalidade e a mesma coerência entre a
/// classificação e o artigo da LGPD em que a hipótese cabe.
/// </summary>
public readonly record struct ProtecaoDeDados(
    ClassificacaoDeProtecao Classificacao, string Finalidade, HipoteseLegalTratamento Hipotese)
{
    public const int FinalidadeMaxLength = 500;

    /// <summary>
    /// Confere os três eixos e acumula cada recusa (ADR-0125). A hipótese só é conferida contra a
    /// classificação quando as duas são válidas. <paramref name="restricaoDaClassificacao"/> é a
    /// exigência que só o dono do dado conhece (o fato, pelo domínio dele) e entra na recusa da
    /// classificação.
    /// </summary>
    public static Result<ProtecaoDeDados> Validar(
        CodigoInformado<ClassificacaoDeProtecao> classificacao,
        string? finalidade,
        CodigoInformado<HipoteseLegalTratamento> hipotese,
        RecusasDaProtecao recusas,
        Func<ClassificacaoDeProtecao, DomainError?>? restricaoDaClassificacao = null)
    {
        ArgumentNullException.ThrowIfNull(recusas);

        string finalidadeNormalizada = finalidade?.Trim() ?? string.Empty;
        FieldError?[] conferencias =
        [
            RecusaDaClassificacao(classificacao, recusas, restricaoDaClassificacao),
            RecusaDaFinalidade(finalidadeNormalizada, recusas),
            RecusaDaHipotese(hipotese, classificacao, recusas),
        ];
        List<FieldError> erros = [.. conferencias.OfType<FieldError>()];
        return erros.Count > 0
            ? Result<ProtecaoDeDados>.ValidationFailure(erros)
            : Result<ProtecaoDeDados>.Success(new ProtecaoDeDados(classificacao.Valor!.Value, finalidadeNormalizada, hipotese.Valor!.Value));
    }

    private static FieldError? RecusaDaClassificacao(
        CodigoInformado<ClassificacaoDeProtecao> classificacao, RecusasDaProtecao recusas,
        Func<ClassificacaoDeProtecao, DomainError?>? restricao)
    {
        DomainError? erro = classificacao switch
        {
            { Informado: false } => new(recusas.ClassificacaoObrigatoria, $"Classificação de proteção de dados {recusas.Sujeito} é obrigatória."),
            { Valor: null } => new(recusas.ClassificacaoInvalida, $"Classificação de proteção de dados {recusas.Sujeito} fora do vocabulário fechado."),
            { Valor: { } conhecida } => restricao?.Invoke(conhecida),
        };
        return erro is null ? null : new FieldError("classificacaoProtecao", erro);
    }

    private static FieldError? RecusaDaFinalidade(string finalidade, RecusasDaProtecao recusas)
    {
        DomainError? erro = finalidade.Length == 0
            ? new(recusas.FinalidadeObrigatoria, $"Finalidade do tratamento {recusas.Sujeito} é obrigatória.")
            : finalidade.Length > FinalidadeMaxLength
                ? new(recusas.FinalidadeTamanho, $"Finalidade do tratamento deve ter no máximo {FinalidadeMaxLength} caracteres.")
                : null;
        return erro is null ? null : new FieldError("finalidadeTratamento", erro);
    }

    private static FieldError? RecusaDaHipotese(
        CodigoInformado<HipoteseLegalTratamento> hipotese, CodigoInformado<ClassificacaoDeProtecao> classificacao, RecusasDaProtecao recusas)
    {
        DomainError? erro = hipotese switch
        {
            { Informado: false } => new(recusas.HipoteseObrigatoria, $"Hipótese legal de tratamento {recusas.Sujeito} é obrigatória."),
            { Valor: null } => new(recusas.HipoteseInvalida, $"Hipótese legal de tratamento {recusas.Sujeito} fora do vocabulário fechado."),
            { Valor: { } conhecida } => RecusaDaHipoteseNaClassificacao(conhecida, classificacao, recusas),
        };
        return erro is null ? null : new FieldError("hipoteseLegal", erro);
    }

    private static DomainError? RecusaDaHipoteseNaClassificacao(
        HipoteseLegalTratamento hipotese, CodigoInformado<ClassificacaoDeProtecao> classificacao, RecusasDaProtecao recusas) =>
        classificacao.Valor is { } escala && !HipotesesLegaisTratamento.AdmiteClassificacao(hipotese, escala)
            ? new(recusas.HipoteseIncompativel, "A hipótese legal não cabe na classificação: dado sensível usa as hipóteses do art. 11 da LGPD, e os demais as do art. 7º.")
            : null;
}

/// <summary>
/// Os códigos de erro que cada dono da proteção dá à recusa de cada eixo, e o sujeito que entra na
/// mensagem ("do fato", "do tipo de documento").
/// </summary>
public sealed record RecusasDaProtecao(
    string Sujeito,
    string ClassificacaoObrigatoria,
    string ClassificacaoInvalida,
    string FinalidadeObrigatoria,
    string FinalidadeTamanho,
    string HipoteseObrigatoria,
    string HipoteseInvalida,
    string HipoteseIncompativel);

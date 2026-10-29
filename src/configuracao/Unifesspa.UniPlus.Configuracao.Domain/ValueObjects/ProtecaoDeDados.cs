namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

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
        ClassificacaoDeProtecao classificacao,
        string? finalidade,
        HipoteseLegalTratamento hipotese,
        RecusasDaProtecao recusas,
        Func<ClassificacaoDeProtecao, DomainError?>? restricaoDaClassificacao = null)
    {
        ArgumentNullException.ThrowIfNull(recusas);

        List<FieldError> erros = [];
        void Recusar(string campo, string codigo, string mensagem) => erros.Add(new(campo, new DomainError(codigo, mensagem)));

        bool classificacaoValida = classificacao != ClassificacaoDeProtecao.Nenhuma && Enum.IsDefined(classificacao);
        if (classificacao == ClassificacaoDeProtecao.Nenhuma)
        {
            Recusar("classificacaoProtecao", recusas.ClassificacaoObrigatoria,
                $"Classificação de proteção de dados {recusas.Sujeito} é obrigatória.");
        }
        else if (!classificacaoValida)
        {
            Recusar("classificacaoProtecao", recusas.ClassificacaoInvalida,
                $"Classificação de proteção de dados {recusas.Sujeito} fora do vocabulário fechado.");
        }
        else if (restricaoDaClassificacao?.Invoke(classificacao) is { } restricao)
        {
            Recusar("classificacaoProtecao", restricao.Code, restricao.Message);
        }

        string finalidadeNormalizada = finalidade?.Trim() ?? string.Empty;
        if (finalidadeNormalizada.Length == 0)
        {
            Recusar("finalidadeTratamento", recusas.FinalidadeObrigatoria,
                $"Finalidade do tratamento {recusas.Sujeito} é obrigatória.");
        }
        else if (finalidadeNormalizada.Length > FinalidadeMaxLength)
        {
            Recusar("finalidadeTratamento", recusas.FinalidadeTamanho,
                $"Finalidade do tratamento deve ter no máximo {FinalidadeMaxLength} caracteres.");
        }

        if (hipotese == HipoteseLegalTratamento.Nenhuma)
        {
            Recusar("hipoteseLegal", recusas.HipoteseObrigatoria,
                $"Hipótese legal de tratamento {recusas.Sujeito} é obrigatória.");
        }
        else if (!Enum.IsDefined(hipotese))
        {
            Recusar("hipoteseLegal", recusas.HipoteseInvalida,
                $"Hipótese legal de tratamento {recusas.Sujeito} fora do vocabulário fechado.");
        }
        else if (classificacaoValida && !HipotesesLegaisTratamento.AdmiteClassificacao(hipotese, classificacao))
        {
            Recusar("hipoteseLegal", recusas.HipoteseIncompativel,
                "A hipótese legal não cabe na classificação: dado sensível usa as hipóteses do art. 11 da LGPD, e os demais as do art. 7º.");
        }

        return erros.Count > 0
            ? Result<ProtecaoDeDados>.ValidationFailure(erros)
            : Result<ProtecaoDeDados>.Success(new ProtecaoDeDados(classificacao, finalidadeNormalizada, hipotese));
    }
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

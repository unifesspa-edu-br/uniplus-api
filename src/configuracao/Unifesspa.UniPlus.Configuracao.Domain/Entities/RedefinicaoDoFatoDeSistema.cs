namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.Entities.ConferenciaDaDefinicaoDoFato;

/// <summary>
/// No fato de sistema só a proteção de dados muda pela tela: a definição que o sistema calcula ou lê é
/// travada, e a alteração que a toca é recusada inteira. Os eixos não informados são ignorados; os
/// informados precisam ser os que o fato já tem.
/// </summary>
internal static class RedefinicaoDoFatoDeSistema
{
    internal static readonly DomainError RecusaDaDefinicao = new(
        FatoCandidatoErrorCodes.DefinicaoDoFatoDeSistemaTravada,
        "Fato de sistema só tem editáveis o nome, a descrição, o componente de exibição e a proteção de dados (classificação, finalidade e hipótese legal). "
        + "Código, domínio, cardinalidade, origem, fonte e formato dos valores, valores de domínio, regras padrão, ponto de resolução, "
        + "escopo e ativação continuam travados, porque o sistema calcula ou lê a definição.");

    /// <summary>
    /// A recusa que a alteração merece antes de tocar na proteção — o token fora do vocabulário, ou a
    /// definição travada —, ou <see langword="null"/> quando só a proteção muda.
    /// </summary>
    internal static Result? Recusa(DefinicaoDoFato definicao, EixosDaDefinicao atuais)
    {
        List<FieldError> foraDoVocabulario = [.. ForaDoVocabulario(definicao)];
        if (foraDoVocabulario.Count > 0)
        {
            return Result.ValidationFailure(foraDoVocabulario);
        }

        return TocaNaDefinicao(definicao, atuais) ? Result.Failure(RecusaDaDefinicao) : null;
    }

    // O token que o vocabulário não conhece é erro de vocabulário no campo, e não troca de definição travada.
    private static IEnumerable<FieldError> ForaDoVocabulario(DefinicaoDoFato definicao) => Presentes(
        ForaDoVocabularioFechado(definicao.Dominio, "dominio", FatoCandidatoErrorCodes.DominioInvalido, "Domínio do fato fora do vocabulário fechado."),
        ForaDoVocabularioFechado(definicao.Cardinalidade, "cardinalidade", FatoCandidatoErrorCodes.CardinalidadeInvalida, "Cardinalidade do fato fora do vocabulário fechado."),
        ForaDoVocabularioFechado(definicao.Escopo, "escopo", FatoCandidatoErrorCodes.EscopoObrigatorio, "Escopo do fato fora do vocabulário fechado."),
        ForaDoVocabularioFechado(definicao.FonteValores, "fonteValores", FatoCandidatoErrorCodes.FonteValoresObrigatoria, "Fonte dos valores do fato fora do vocabulário fechado."),
        ForaDoVocabularioFechado(definicao.Formato, "formato", FatoCandidatoErrorCodes.FormatoObrigatorio, "Formato do fato fora do vocabulário fechado."));

    private static FieldError? ForaDoVocabularioFechado<T>(T? informado, string campo, string codigo, string mensagem)
        where T : struct, Enum =>
        informado is { } valor && !Enum.IsDefined(valor) ? new FieldError(campo, new DomainError(codigo, mensagem)) : null;

    private static bool TocaNaDefinicao(DefinicaoDoFato definicao, EixosDaDefinicao atuais) =>
        Difere(definicao.Dominio, atuais.Dominio)
        || Difere(definicao.Cardinalidade, atuais.Cardinalidade)
        || Difere(definicao.FonteValores, atuais.FonteValores)
        || Difere(definicao.Formato, atuais.Formato)
        || Difere(definicao.Escopo, atuais.Escopo)
        || (!string.IsNullOrWhiteSpace(definicao.PontoResolucao)
            && !string.Equals(definicao.PontoResolucao.Trim(), atuais.PontoResolucao, StringComparison.Ordinal));

    private static bool Difere<T>(T? informado, T? atual)
        where T : struct =>
        informado is not null && !Nullable.Equals(informado, atual);
}

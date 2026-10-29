namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Único lugar em que o token de um eixo do fato vira o enum, no cadastro e na alteração. Ausente,
/// vazio ou só espaços é "não informado" (<see langword="null"/>); o token preenchido que não é do
/// vocabulário vira <see cref="ForaDoVocabulario"/>, valor que não existe no enum e que o agregado
/// recusa como inválido no próprio campo. A comparação é feita após <c>Trim()</c>.
/// </summary>
internal static class TokensDoFato
{
    /// <summary>O que o token preenchido e desconhecido vira: nenhum enum o define.</summary>
    internal const int ForaDoVocabulario = -1;

    internal static DominioFato? Dominio(string? token) => Ler<DominioFato>(token, DominiosFato.TryAnalisar);

    internal static CardinalidadeFato? Cardinalidade(string? token) => Ler<CardinalidadeFato>(token, CardinalidadesFato.TryAnalisar);

    internal static FonteValoresFato? Fonte(string? token) => Ler<FonteValoresFato>(token, FontesValoresFato.TryAnalisar);

    internal static FormatoTexto? Formato(string? token) => Ler<FormatoTexto>(token, FormatosTexto.TryAnalisar);

    internal static EscopoFato? Escopo(string? token) => Ler<EscopoFato>(token, EscoposFato.TryAnalisar);

    internal static ClassificacaoDeProtecao? Classificacao(string? token) =>
        Ler<ClassificacaoDeProtecao>(token, ClassificacoesDeProtecao.TryAnalisar);

    internal static HipoteseLegalTratamento? HipoteseLegal(string? token) =>
        Ler<HipoteseLegalTratamento>(token, HipotesesLegaisTratamento.TryAnalisar);

    private delegate bool Analisador<T>(string? token, out T valor)
        where T : struct, Enum;

    private static T? Ler<T>(string? token, Analisador<T> analisar)
        where T : struct, Enum
    {
        string? normalizado = token?.Trim();
        if (string.IsNullOrEmpty(normalizado))
        {
            return null;
        }

        return analisar(normalizado, out T valor) ? valor : (T)Enum.ToObject(typeof(T), ForaDoVocabulario);
    }
}

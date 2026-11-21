namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A hipótese legal, a finalidade e a descrição que o fato coletado copia do catálogo, para os testes que não as discutem.</summary>
internal static class ProtecaoDoFatoDeTeste
{
    public const string HipoteseLegal = "CUMPRIMENTO_OBRIGACAO_LEGAL";

    public const string Finalidade = "Enquadramento do candidato na reserva de vagas da Lei nº 12.711/2012 e nas ações afirmativas do processo seletivo.";

    public const string Descricao = "O que o dado significa, para quem analisa a inscrição.";

    /// <summary>A cópia do catálogo de um item de teste: classificação pessoal, valores do catálogo e sem formato, salvo o que o teste pede.</summary>
    public static CopiaDoItemNoCatalogo Copia(
        string classificacaoProtecao = "PESSOAL",
        OrigemValoresColeta origemValores = OrigemValoresColeta.Catalogo,
        string? formato = null,
        string? componenteExibicao = null) =>
        new(origemValores, formato, classificacaoProtecao, HipoteseLegal, Finalidade, Descricao, componenteExibicao);
}

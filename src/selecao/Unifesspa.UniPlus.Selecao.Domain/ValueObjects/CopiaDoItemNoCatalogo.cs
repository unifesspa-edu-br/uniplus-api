namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que o item do formulário copia do fato no catálogo: a origem dos valores, o formato da
/// resposta de texto, a proteção de dados (classificação, hipótese legal e finalidade), a descrição e o componente de
/// exibição. O catálogo é editável, e o item refaz a cópia enquanto nenhuma versão publicada coletou o fato.
/// </summary>
public sealed record CopiaDoItemNoCatalogo(
    OrigemValoresColeta OrigemValores,
    string? Formato,
    string ClassificacaoProtecao,
    string HipoteseLegal,
    string FinalidadeTratamento,
    string Descricao,
    string? ComponenteExibicao)
{
    /// <summary>O que falta à cópia para conferir a forma do item; nulo quando os três textos obrigatórios vieram.</summary>
    internal string? Violacao() =>
        string.IsNullOrWhiteSpace(HipoteseLegal) ? "A cópia do catálogo precisa de hipótese legal."
        : string.IsNullOrWhiteSpace(FinalidadeTratamento) ? "A cópia do catálogo precisa de finalidade do tratamento."
        : string.IsNullOrWhiteSpace(Descricao) ? "A cópia do catálogo precisa de descrição."
        : null;

    /// <summary>A classificação de proteção e o componente de exibição estão no vocabulário fechado.</summary>
    internal IEnumerable<FieldError> ConferirVocabulario()
    {
        if (!ClassificacoesDeProtecao.TokensCanonicos.Contains(ClassificacaoProtecao))
        {
            // A Seleção só copia o valor já validado pelo catálogo, no vocabulário fechado do Kernel.
            yield return new("classificacaoProtecao", new DomainError(
                FatoColetadoErrorCodes.ClassificacaoProtecaoInvalida,
                "Classificação de proteção de dados fora do vocabulário fechado."));
        }

        if (FormaDoItem.TextoOpcional(ComponenteExibicao) is { } componente && !CatalogoDe<ComponenteDeExibicao>.TentarObter(componente, out _))
        {
            yield return new("componenteExibicao", new DomainError(
                FatoColetadoErrorCodes.ComponenteExibicaoInvalido,
                "Componente de exibição fora do vocabulário fechado."));
        }
    }

    /// <summary>
    /// A cópia na forma em que o envelope a congela: textos aparados, hipótese legal, finalidade e
    /// descrição também em NFC, e o texto opcional em branco ausente. A cópia precisa já ter passado
    /// por <see cref="Violacao"/>.
    /// </summary>
    internal CopiaDoItemNoCatalogo NaFormaCongelada() => this with
    {
        Formato = FormaDoItem.TextoOpcional(Formato),
        HipoteseLegal = HipoteseLegal.Trim(),
        FinalidadeTratamento = FinalidadeTratamento.Trim().Normalize(NormalizationForm.FormC),
        Descricao = Descricao.Trim().Normalize(NormalizationForm.FormC),
        ComponenteExibicao = FormaDoItem.TextoOpcional(ComponenteExibicao),
    };
}

/// <summary>A recusa da cópia que um item ou campo de grupo tirou do catálogo, com o item de origem.</summary>
public sealed record RecusaDaCopiaDoItem(FatoColetado Item, FieldError Erro);

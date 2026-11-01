namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
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
    string? ComponenteExibicao);

/// <summary>A recusa da cópia que um item ou campo de grupo tirou do catálogo, com o item de origem.</summary>
public sealed record RecusaDaCopiaDoItem(FatoColetado Item, FieldError Erro);

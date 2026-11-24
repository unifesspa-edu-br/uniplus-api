namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

/// <summary>
/// O que a reconciliação dos produtos tem em comum entre a fase e a etapa: a identidade da linha,
/// o ato publicado e o papel da publicação. É a chave sobre a qual <c>ProdutosPublicados</c>
/// decide se a linha que chega é a mesma que já existia.
/// </summary>
internal interface IProdutoPublicado
{
    Guid Id { get; }

    /// <summary>Código do tipo de ato publicado; com o papel, é a chave natural dentro da fase ou da etapa.</summary>
    string AtoCodigo { get; }

    PapelDaPublicacaoPrevista? Papel { get; }

    /// <summary>
    /// Troca o papel preservando a linha rastreada. A reconciliação casa o produto vivo pelo ato
    /// quando o papel foi o que mudou; recriá-lo faria a mesma matéria sair e voltar, com DELETE e
    /// INSERT disputando o mesmo slot do índice único na mesma transação.
    /// </summary>
    void AtualizarPapel(PapelDaPublicacaoPrevista? papel);
}

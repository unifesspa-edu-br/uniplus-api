namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Leitura da biblioteca <c>rol_de_regras</c> (Story #772): resolve regras
/// tipadas e versionadas para que as dimensões da configuração do Processo
/// Seletivo montem sua <see cref="Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ReferenciaRegra"/>
/// (<c>codigo</c>+<c>versao</c>+<c>hash</c>). Somente leitura — o catálogo é
/// seed-governado e append-only; não há escrita por esta via.
/// </summary>
public interface IRegraCatalogoReader
{
    /// <summary>
    /// Resolve a regra pela sua identidade <c>(codigo, versao)</c>, ou
    /// <see langword="null"/> se não houver aquela versão no catálogo.
    /// </summary>
    Task<RegraCatalogo?> ObterAsync(string codigo, string versao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista as regras de um <paramref name="tipo"/> (ex.: as regras de
    /// desempate disponíveis para o admin escolher), ordenadas por
    /// <c>codigo</c>+<c>versao</c>.
    /// </summary>
    Task<IReadOnlyList<RegraCatalogo>> ListarPorTipoAsync(TipoRegra tipo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista o catálogo paginado por cursor bidirecional (ADR-0026 + ADR-0089), com filtro
    /// opcional por tipo. A ordem é <c>tipo</c>, <c>codigo</c> e <c>versao</c>, ascendente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ordem é do contrato, e não uma conveniência do banco. Ordenar por <c>Id</c> — como faz
    /// a paginação padrão do módulo — daria a ordem em que o seed inseriu as linhas, que não
    /// significa nada para quem consulta o catálogo.
    /// </para>
    /// <para>
    /// <b>Ordenar por versão não é eleger a mais recente.</b> A ordenação existe para a página
    /// ser estável entre requisições; qual versão vale para um certame é decisão de quem
    /// configura, e o catálogo mantém as versões coexistindo justamente porque uma comparação
    /// lexical de <c>v2</c> contra <c>v10</c> não responde essa pergunta.
    /// </para>
    /// </remarks>
    Task<(IReadOnlyList<RegraCatalogo> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        TipoRegra? tipo,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        CancellationToken cancellationToken = default);
}

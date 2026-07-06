namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

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
}

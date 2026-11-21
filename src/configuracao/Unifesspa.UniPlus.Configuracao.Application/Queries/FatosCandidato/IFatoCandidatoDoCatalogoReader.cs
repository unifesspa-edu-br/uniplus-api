namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Leitura do catálogo de fatos para a consulta pública: cada fato com os operadores que uma condição pode
/// usar sobre ele. Somente leitura; a Seleção lê o catálogo por outra porta.
/// </summary>
public interface IFatoCandidatoDoCatalogoReader
{
    /// <summary>Todos os fatos do catálogo, ordenados por código.</summary>
    Task<IReadOnlyList<FatoDoCatalogoDto>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>O fato de código informado, ou <see langword="null"/> quando o código não existe no catálogo.</summary>
    Task<FatoDoCatalogoDto?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}

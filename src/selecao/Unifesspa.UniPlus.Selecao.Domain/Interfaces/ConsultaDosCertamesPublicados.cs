namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Consulta dos certames publicados.</summary>
/// <param name="IncluirContadores">
/// Pede a contagem por situação junto da página. Ela considera os demais filtros, mas não a situação,
/// pois os números alimentam esse filtro.
/// </param>
/// <param name="VersaoDaProjecaoServida">
/// Versão do documento público que quem consulta sabe servir. Linhas de outra versão ficam fora
/// da página e da contagem, para que os números não prometam itens que não podem ser mostrados.
/// </param>
public sealed record ConsultaDosCertamesPublicados(
    DateTimeOffset InstanteSeForAPrimeiraPagina, FiltroDosCertamesPublicados Filtro,
    IReadOnlyList<SortField> Ordenacao, bool IncluirContadores, string VersaoDaProjecaoServida);

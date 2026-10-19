namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os tipos de ato únicos por objeto que o catálogo de teste declara, para os gates de publicação.</summary>
internal static class TiposDeAtoUnicosDeTeste
{
    /// <summary>Catálogo sem tipo único: o cronograma do teste não ocupa nenhuma vaga de linhagem.</summary>
    public static TiposDeAtoUnicosPorObjeto Nenhum { get; } =
        TiposDeAtoUnicosPorObjeto.ParaChecklist(new Dictionary<string, string>(StringComparer.Ordinal), []);
}

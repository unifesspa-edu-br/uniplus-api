namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os fatos do catálogo cujos valores são modalidades, como a Application os lê do seed.</summary>
internal static class FatosDeModalidadeDeTeste
{
    /// <summary>A modalidade de concorrência e o grupo da convocação.</summary>
    public static FatosDeModalidade DoCatalogo { get; } = new(["MODALIDADE", "MODALIDADE_CONVOCACAO"]);
}

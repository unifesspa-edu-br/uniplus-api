namespace Unifesspa.UniPlus.Kernel.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Conversão e resolução da classificação de proteção.</summary>
public static class ClassificacoesDeProtecao
{
    /// <summary>Códigos aceitos para classificação de proteção.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. CatalogoDe<ClassificacaoDeProtecao>.Todos().Select(static c => c.Codigo)];

    /// <summary>Código de contrato da classificação.</summary>
    public static string ParaTokenCanonico(ClassificacaoDeProtecao valor) => valor.Codigo();

    /// <summary>Uma classificação ausente ou inválida exige a proteção mais restrita.</summary>
    public static ClassificacaoDeProtecao Resolvida(ClassificacaoDeProtecao? classificacao) =>
        classificacao is { } valor && Enum.IsDefined(valor) ? valor : ClassificacaoDeProtecao.Sensivel;

    /// <summary>Procura uma classificação pelo código exato.</summary>
    public static bool TryAnalisar(string? token, out ClassificacaoDeProtecao valor) =>
        CatalogoDe<ClassificacaoDeProtecao>.TentarObter(token, out valor);

    /// <summary>Obtém a classificação pelo código.</summary>
    public static ClassificacaoDeProtecao Analisar(string? token) =>
        TryAnalisar(token, out ClassificacaoDeProtecao valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Código de classificação de proteção inválido.");
}

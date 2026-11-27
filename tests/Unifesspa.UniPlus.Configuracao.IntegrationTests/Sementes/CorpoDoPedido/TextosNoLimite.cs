namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Textos, códigos e condições no tamanho máximo que a validação aceita, em caracteres acentuados para pesar em UTF-8.</summary>
internal static class TextosNoLimite
{
    public static string Texto(int tamanho) => new('ã', tamanho);

    /// <summary>Um código de formato válido — começa com letra maiúscula, só letras e algarismos — com o tamanho exato, distinto por índice.</summary>
    public static string Codigo(int tamanho, int indice)
    {
        string numero = indice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new string('A', tamanho - numero.Length) + numero;
    }

    public static string MaiorCodigo<TEnum>()
        where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.Todos().Select(static c => c.Codigo).MaxBy(static c => c.Length)!;

    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Predicado(DimensoesDasSementesDoModelo dimensoes, int tamanhoDoFato) =>
    [
        .. Enumerable.Range(0, dimensoes.Clausulas).Select(clausula =>
            (IReadOnlyList<CondicaoPrecondicaoInput>)[.. Enumerable.Range(0, dimensoes.Condicoes).Select(condicao => Condicao(tamanhoDoFato, clausula * 100 + condicao))]),
    ];

    private static CondicaoPrecondicaoInput Condicao(int tamanhoDoFato, int indice) =>
        new(Codigo(tamanhoDoFato, indice), Operador.Igual.Codigo(), JsonSerializer.SerializeToElement(Codigo(tamanhoDoFato, indice)));
}

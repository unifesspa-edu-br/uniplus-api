namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.Collections.Frozen;

using Microsoft.OpenApi;

/// <summary>
/// Distingue o parâmetro de texto livre, que a pessoa digita e pode ser dado pessoal, do valor controlado pelo contrato:
/// enumeração de códigos, número e os formatos que não carregam dado pessoal. O que o contrato não permite resolver conta
/// como texto livre, para que a dúvida reprove em vez de passar.
/// </summary>
internal static class TextoLivreNoContrato
{
    private static readonly FrozenSet<string> FormatosSemDadoPessoal = new[]
    {
        "uuid", "date", "date-time", "time", "duration", "int32", "int64", "float", "double", "decimal",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Eh(IOpenApiSchema? esquema)
    {
        IOpenApiSchema? alvo = esquema is OpenApiSchemaReference referencia ? referencia.Target : esquema;
        if (alvo is null)
        {
            return true;
        }

        // Composição só é controlada quando todas as ramificações são; o tipo do próprio esquema, se houver, também conta.
        bool algumaRamificacaoLivre = Ramificacoes(alvo).Any(Eh);
        return algumaRamificacaoLivre || (TemComposicao(alvo) ? alvo.Type is not null && EhProprioLivre(alvo) : EhProprioLivre(alvo));
    }

    private static bool EhProprioLivre(IOpenApiSchema esquema) => esquema switch
    {
        { Items: { } itens } when Tem(esquema, JsonSchemaType.Array) => Eh(itens),
        { Type: null } => !EhValorControlado(esquema),
        _ => Tem(esquema, JsonSchemaType.String) && !EhValorControlado(esquema),
    };

    private static IEnumerable<IOpenApiSchema> Ramificacoes(IOpenApiSchema esquema) =>
        (esquema.AllOf ?? []).Concat(esquema.OneOf ?? []).Concat(esquema.AnyOf ?? []);

    private static bool TemComposicao(IOpenApiSchema esquema) => Ramificacoes(esquema).Any();

    private static bool EhValorControlado(IOpenApiSchema esquema) =>
        esquema.Enum?.Count > 0 || (esquema.Format is not null && FormatosSemDadoPessoal.Contains(esquema.Format));

    private static bool Tem(IOpenApiSchema esquema, JsonSchemaType tipo) => esquema.Type?.HasFlag(tipo) == true;
}

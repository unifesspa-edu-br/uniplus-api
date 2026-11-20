namespace Unifesspa.UniPlus.ArchTests.TestSupport;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Lugar do modelo finalizado que guarda um valor, a propriedade escalar ou o elemento de uma coleção primitiva, com o
/// conversor que o EF aplica a ele. A propriedade de coleção sem tipo de elemento, como a que recebeu o conversor na
/// coleção inteira, também é um ponto: ela é examinada pelo tipo da coleção.
/// </summary>
/// <param name="Descricao">Onde o ponto está, do tipo de entidade à propriedade, para a mensagem da violação.</param>
/// <param name="Propriedade">O nome da propriedade; para o elemento, o da coleção.</param>
/// <param name="TipoDeclarado">O tipo do valor, inclusive o <see cref="Nullable{T}"/>.</param>
/// <param name="Conversor">O conversor que o modelo aplica ao valor, ou nulo quando não há.</param>
internal sealed record PontoDoModelo(string Descricao, string Propriedade, Type TipoDeclarado, ValueConverter? Conversor)
{
    internal Type TipoDoValor => Nullable.GetUnderlyingType(TipoDeclarado) ?? TipoDeclarado;

    internal bool GuardaEnumDeContrato => EnumsDeContrato.EhEnumQueDeclaraCodigo(TipoDoValor);

    internal bool UsaOConversorDoCodigo =>
        Conversor?.GetType() == typeof(CodigoDeContratoValueConverter<>).MakeGenericType(TipoDoValor);

    /// <summary>O enum com código de contrato que o ponto guarda, direto ou como elemento da coleção que ele é.</summary>
    internal Type? EnumDeContrato => GuardaEnumDeContrato ? TipoDoValor : EnumDaColecao;

    /// <summary>
    /// Verdadeiro quando o ponto grava um enum com código de contrato de outro jeito que não o conversor do código:
    /// o valor com outra conversão, ou a coleção de enum com código sem tipo de elemento, que só é gravada pelo código
    /// quando é coleção primitiva.
    /// </summary>
    internal bool NaoGravaPeloCodigo => (GuardaEnumDeContrato && !UsaOConversorDoCodigo) || EnumDaColecao is not null;

    private Type? EnumDaColecao =>
        TipoDeclarado.GetInterfaces().Append(TipoDeclarado)
            .Where(tipo => tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(tipo => Nullable.GetUnderlyingType(tipo.GenericTypeArguments[0]) ?? tipo.GenericTypeArguments[0])
            .FirstOrDefault(EnumsDeContrato.EhEnumQueDeclaraCodigo);
}

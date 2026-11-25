namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using System.Text.Json;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Lugar do modelo finalizado que guarda um valor, a propriedade escalar ou o elemento de uma coleção primitiva, com o
/// conversor que o EF aplica a ele.
/// </summary>
/// <param name="Descricao">Onde o ponto está, do tipo de entidade à propriedade, para a mensagem da violação.</param>
/// <param name="Propriedade">O nome da propriedade; para o elemento, o da coleção.</param>
/// <param name="TipoDeclarado">O tipo do valor, inclusive o <see cref="Nullable{T}"/>.</param>
/// <param name="Conversor">O conversor que o modelo aplica ao valor, ou nulo quando não há.</param>
internal sealed record PontoDoModelo(string Descricao, string Propriedade, Type TipoDeclarado, ValueConverter? Conversor, string? Comentario)
{
    internal Type TipoDoValor => Nullable.GetUnderlyingType(TipoDeclarado) ?? TipoDeclarado;

    internal bool GuardaEnumDeContrato => EnumsDeContrato.EhEnumQueDeclaraCodigo(TipoDoValor);

    private static readonly JsonSerializerOptions Opcoes = OpcoesJsonDeDocumento.Criar(new JsonSerializerOptions());

    internal bool UsaOConversorDoCodigo => Conversor is not null && Enum.GetValues(TipoDoValor).Cast<object>()
        .All(valor => Equals(Conversor.ConvertToProvider(valor), JsonSerializer.SerializeToElement(valor, TipoDoValor, Opcoes).GetString()));

    internal Type? EnumDeContrato => GuardaEnumDeContrato ? TipoDoValor : null;

    internal bool NaoGravaPeloCodigo => GuardaEnumDeContrato && !UsaOConversorDoCodigo;
}

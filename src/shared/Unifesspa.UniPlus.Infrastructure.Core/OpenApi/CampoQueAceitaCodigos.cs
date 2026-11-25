namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O campo de pedido ou o parâmetro de consulta marcado com <see cref="AceitaCodigosDeAttribute{TEnum}"/>: o enum cujos
/// códigos ele aceita e se ele é um texto ou uma coleção de textos.
/// </summary>
internal sealed class CampoQueAceitaCodigos
{
    private CampoQueAceitaCodigos(Type enumDeContrato, bool ehColecao)
    {
        EnumDeContrato = enumDeContrato;
        EhColecao = ehColecao;
    }

    public Type EnumDeContrato { get; }

    public bool EhColecao { get; }

    /// <summary>O <c>codigo</c> da resposta, que é um texto com os códigos do enum.</summary>
    public static CampoQueAceitaCodigos DoCodigoDe(Type enumDeContrato)
    {
        ArgumentNullException.ThrowIfNull(enumDeContrato);

        return new CampoQueAceitaCodigos(enumDeContrato, ehColecao: false);
    }

    /// <summary>A propriedade que declara o atributo, ou nada se ela não o declara.</summary>
    /// <exception cref="InvalidOperationException">A propriedade não é texto nem coleção de texto.</exception>
    public static CampoQueAceitaCodigos? De(JsonPropertyInfo propriedade)
    {
        ArgumentNullException.ThrowIfNull(propriedade);

        return EnumDoAtributo(AtributosDa(propriedade)) is { } enumDeContrato
            ? Criar($"{propriedade.DeclaringType.Name}.{propriedade.Name}", propriedade.PropertyType, enumDeContrato)
            : null;
    }

    /// <summary>O parâmetro que declara o atributo, ou nada se ele não o declara.</summary>
    /// <param name="parametro">O parâmetro descrito pelo ApiExplorer.</param>
    /// <param name="operacao">A operação do parâmetro, para a mensagem de erro.</param>
    /// <exception cref="InvalidOperationException">O parâmetro não é texto nem coleção de texto.</exception>
    public static CampoQueAceitaCodigos? De(ApiParameterDescription parametro, string operacao)
    {
        ArgumentNullException.ThrowIfNull(parametro);

        return EnumDoAtributo(AtributosDo(parametro)) is { } enumDeContrato
            ? Criar($"{operacao}, parâmetro {parametro.Name}", parametro.Type, enumDeContrato)
            : null;
    }

    private static CampoQueAceitaCodigos Criar(string nome, Type tipo, Type enumDeContrato)
    {
        bool ehColecao = tipo != typeof(string) && ItemDaColecao(tipo) == typeof(string);

        return tipo == typeof(string) || ehColecao
            ? new CampoQueAceitaCodigos(enumDeContrato, ehColecao)
            : throw new InvalidOperationException(
                $"O campo {nome}, do tipo {tipo.Name}, declara AceitaCodigosDe<{enumDeContrato.Name}>, que vale só para texto ou coleção de texto.");
    }

    // O parâmetro do construtor que o serializador liga à propriedade é o que carrega o atributo de um registro posicional;
    // outro construtor com um parâmetro de mesmo nome não conta.
    private static object[] AtributosDa(JsonPropertyInfo propriedade) =>
        [.. AtributosDe(propriedade.AttributeProvider), .. AtributosDe(propriedade.AssociatedParameter?.AttributeProvider)];

    private static object[] AtributosDe(ICustomAttributeProvider? provedor) => provedor?.GetCustomAttributes(inherit: true) ?? [];

    // O tipo de consulta com [FromQuery] chega ao ApiExplorer como um parâmetro por propriedade, que só o metadado do modelo
    // liga à propriedade do tipo; o parâmetro direto de uma ação ou de uma rota mínima tem o atributo no ParameterInfo.
    private static object[] AtributosDo(ApiParameterDescription parametro) =>
        parametro.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } tipo, PropertyName: { } nome }
            ? AtributosDaPropriedadeDeConsulta(tipo, nome)
            : (parametro.ParameterDescriptor as IParameterInfoParameterDescriptor)?.ParameterInfo.GetCustomAttributes(inherit: true) ?? [];

    private static object[] AtributosDaPropriedadeDeConsulta(Type tipo, string nome)
    {
        object[] atributos = AtributosDe(tipo.GetProperty(nome));
        JsonPropertyInfo? propriedade = JsonSerializerOptions.Default.GetTypeInfo(tipo).Properties
            .FirstOrDefault(p => p.AttributeProvider is PropertyInfo info && info.Name == nome);
        return [.. atributos, .. AtributosDe(propriedade?.AssociatedParameter?.AttributeProvider)];
    }

    private static Type? EnumDoAtributo(IEnumerable<object>? atributos) =>
        atributos?.OfType<IAceitaCodigosDe>().FirstOrDefault()?.EnumDeContrato;

    private static Type? ItemDaColecao(Type tipo) =>
        tipo.IsArray
            ? tipo.GetElementType()
            : tipo.GetInterfaces().Prepend(tipo)
                .FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
}

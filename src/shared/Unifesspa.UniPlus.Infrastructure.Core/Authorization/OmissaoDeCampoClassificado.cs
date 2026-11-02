namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// Defesa secundária da projeção (ADR-0153): o serializador lê a classificação declarada no DTO e as decisões que o
/// projetor já tomou na requisição, e <b>omite</b> o campo classificado sem decisão permitida, sem pedir a decisão de novo.
/// A propriedade declara a classe por <see cref="ProtegidoComoAttribute"/>; os itens de uma lista carregam a própria, e a
/// lista sai já sem os itens negados, porque um modificador de contrato não filtra elementos. Falha fechada: sem decisão
/// na requisição, nada classificado sai.
/// </summary>
internal static class OmissaoDeCampoClassificado
{
    /// <summary>Instala o modificador nas duas pilhas de JSON do ASP.NET: controllers e minimal APIs.</summary>
    public static IServiceCollection AddOmissaoDeCampoClassificado(this IServiceCollection services)
    {
        services.AddOptions<Microsoft.AspNetCore.Mvc.JsonOptions>()
            .Configure<IHttpContextAccessor>((opcoes, acessor) => Instalar(opcoes.JsonSerializerOptions, acessor));
        services.AddOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>()
            .Configure<IHttpContextAccessor>((opcoes, acessor) => Instalar(opcoes.SerializerOptions, acessor));

        return services;
    }

    /// <summary>O modificador do contrato JSON, ligado às decisões da requisição corrente.</summary>
    public static Action<JsonTypeInfo> Modificador(IHttpContextAccessor acessor)
    {
        ArgumentNullException.ThrowIfNull(acessor);

        return tipo => Modificar(tipo, () => DecisoesDaRequisicaoHttp.Ler(acessor.HttpContext));
    }

    private static void Instalar(System.Text.Json.JsonSerializerOptions opcoes, IHttpContextAccessor acessor) =>
        opcoes.TypeInfoResolver = (opcoes.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(Modificador(acessor));

    private static void Modificar(JsonTypeInfo tipo, Func<IPermissoesDaProjecao> decisoes)
    {
        if (tipo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo propriedade in tipo.Properties)
        {
            if (ProtegidoComoDe(propriedade) is { } declaracao)
            {
                Func<object, object?, bool>? anterior = propriedade.ShouldSerialize;
                propriedade.ShouldSerialize = (objeto, valor) =>
                    decisoes().Permite(declaracao.Natureza, declaracao.Classificacao) && (anterior?.Invoke(objeto, valor) ?? true);
            }
            else if (EhValorDoItem(propriedade))
            {
                Func<object, object?, bool>? anterior = propriedade.ShouldSerialize;
                propriedade.ShouldSerialize = (objeto, valor) =>
                    (objeto is not IItemClassificado item || decisoes().ValorSai(item)) && (anterior?.Invoke(objeto, valor) ?? true);
            }
            else if (ElementoClassificado(propriedade.PropertyType) is { } elemento && propriedade.Get is { } ler)
            {
                propriedade.Get = objeto => Filtrar(ler(objeto), elemento, decisoes());
            }
            else if (EhItemClassificadoUnico(propriedade))
            {
                Func<object, object?, bool>? anterior = propriedade.ShouldSerialize;
                propriedade.ShouldSerialize = (objeto, valor) =>
                    (valor is not IItemClassificado item || decisoes().ItemSai(item)) && (anterior?.Invoke(objeto, valor) ?? true);
            }
        }
    }

    /// <summary>
    /// Se a serialização pode omitir a propriedade: pela condição de <c>JsonIgnore</c> (nula ou default), pela classificação
    /// declarada, por ser o valor de um item classificado ou por ser um item classificado único. É a regra única que o modificador aplica e o contrato OpenAPI
    /// espelha para não declarar como obrigatório o que a resposta omite. A lista de itens não entra: sai filtrada, não omitida.
    /// </summary>
    public static bool PodeOmitir(JsonPropertyInfo propriedade)
    {
        ArgumentNullException.ThrowIfNull(propriedade);

        return (propriedade.AttributeProvider as MemberInfo is { } membro
                && Attribute.GetCustomAttributes(membro, typeof(JsonIgnoreAttribute), inherit: true)
                    .OfType<JsonIgnoreAttribute>()
                    .Any(static a => a.Condition is JsonIgnoreCondition.WhenWritingNull or JsonIgnoreCondition.WhenWritingDefault))
            || ProtegidoComoDe(propriedade) is not null
            || EhValorDoItem(propriedade)
            || EhItemClassificadoUnico(propriedade);
    }

    private static bool EhValorDoItem(JsonPropertyInfo propriedade) =>
        propriedade.AttributeProvider is MemberInfo membro && Attribute.IsDefined(membro, typeof(ValorDoItemAttribute), inherit: true);

    private static ProtegidoComoAttribute? ProtegidoComoDe(JsonPropertyInfo propriedade) =>
        propriedade.AttributeProvider is MemberInfo membro
            ? Attribute.GetCustomAttribute(membro, typeof(ProtegidoComoAttribute), inherit: true) as ProtegidoComoAttribute
            : null;

    private static bool EhItemClassificadoUnico(JsonPropertyInfo propriedade) =>
        typeof(IItemClassificado).IsAssignableFrom(propriedade.PropertyType);

    private static Type? ElementoClassificado(Type tipo)
    {
        Type? elemento = tipo.IsArray
            ? tipo.GetElementType()
            : tipo.IsGenericType && tipo.GetGenericArguments().Length == 1 && typeof(IEnumerable).IsAssignableFrom(tipo)
                ? tipo.GetGenericArguments()[0]
                : null;

        if (elemento is null || !typeof(IItemClassificado).IsAssignableFrom(elemento))
        {
            return null;
        }

        return tipo.IsAssignableFrom(elemento.MakeArrayType())
            ? elemento
            : throw new InvalidOperationException(
                $"A propriedade de itens classificados '{tipo}' precisa ser uma lista somente leitura (IReadOnlyList, IEnumerable ou vetor) para a omissão alcançá-la.");
    }

    private static object? Filtrar(object? lista, Type elemento, IPermissoesDaProjecao decisoes)
    {
        if (lista is not IEnumerable itens)
        {
            return lista;
        }

        List<object> permitidos = [.. itens.Cast<object>().Where(i => i is not IItemClassificado item || decisoes.ItemSai(item))];
        Array vetor = Array.CreateInstance(elemento, permitidos.Count);
        for (int i = 0; i < permitidos.Count; i++)
        {
            vetor.SetValue(permitidos[i], i);
        }

        return vetor;
    }
}

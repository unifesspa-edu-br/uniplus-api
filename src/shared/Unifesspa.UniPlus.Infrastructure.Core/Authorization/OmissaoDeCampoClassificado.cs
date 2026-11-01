namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using System.Collections;
using System.Reflection;
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
            if (propriedade.AttributeProvider?.GetCustomAttributes(typeof(ProtegidoComoAttribute), inherit: true)
                .OfType<ProtegidoComoAttribute>().FirstOrDefault() is { } declaracao)
            {
                Func<object, object?, bool>? anterior = propriedade.ShouldSerialize;
                propriedade.ShouldSerialize = (objeto, valor) =>
                    decisoes().Permite(declaracao.Natureza, declaracao.Classificacao) && (anterior?.Invoke(objeto, valor) ?? true);
            }
            else if (ElementoClassificado(propriedade.PropertyType) is { } elemento && propriedade.Get is { } ler)
            {
                propriedade.Get = objeto => Filtrar(ler(objeto), elemento, decisoes());
            }
            else if (typeof(IItemClassificado).IsAssignableFrom(propriedade.PropertyType))
            {
                Func<object, object?, bool>? anterior = propriedade.ShouldSerialize;
                propriedade.ShouldSerialize = (objeto, valor) =>
                    (valor is not IItemClassificado item || decisoes().Permite(item)) && (anterior?.Invoke(objeto, valor) ?? true);
            }
        }
    }

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

        List<object> permitidos = [.. itens.Cast<object>().Where(i => i is not IItemClassificado item || decisoes.Permite(item))];
        Array vetor = Array.CreateInstance(elemento, permitidos.Count);
        for (int i = 0; i < permitidos.Count; i++)
        {
            vetor.SetValue(permitidos[i], i);
        }

        return vetor;
    }
}

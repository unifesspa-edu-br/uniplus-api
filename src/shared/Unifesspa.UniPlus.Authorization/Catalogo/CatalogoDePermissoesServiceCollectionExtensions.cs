namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registro de enums de permissões no catálogo da aplicação.</summary>
public static class CatalogoDePermissoesServiceCollectionExtensions
{
    /// <summary>
    /// Inclui o enum no catálogo da aplicação. Confere o enum na hora, de modo que
    /// um membro inválido derruba a subida e a mensagem nomeia o membro.
    /// </summary>
    /// <typeparam name="TPermissao">Enum de permissões do módulo.</typeparam>
    public static IServiceCollection AddCatalogoDePermissoes<TPermissao>(this IServiceCollection services)
        where TPermissao : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = CatalogoDePermissoes<TPermissao>.Todas();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IFonteDeCatalogo, FonteDeCatalogo<TPermissao>>());
        services.TryAddSingleton<CatalogoDaAplicacao>();

        return services;
    }
}

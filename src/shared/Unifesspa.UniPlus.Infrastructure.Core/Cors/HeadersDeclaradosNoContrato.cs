namespace Unifesspa.UniPlus.Infrastructure.Core.Cors;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// Os headers de resposta que os endpoints da aplicação prometem no contrato: os próprios de cada
/// rota, declarados por <see cref="EmiteHeaderAttribute"/>, e o selo de entidade, declarado por
/// <see cref="EmiteETagAttribute"/>.
/// </summary>
internal static class HeadersDeclaradosNoContrato
{
    private const string NomeDoSelo = "ETag";

    internal static IReadOnlyCollection<string> Ler(IActionDescriptorCollectionProvider acoes) =>
        acoes.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(static acao => acao.MethodInfo)
            .Distinct()
            .SelectMany(Declarados)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Declarados(MethodInfo metodo)
    {
        foreach (EmiteHeaderAttribute header in metodo.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true))
        {
            yield return header.Nome;
        }

        if (metodo.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is not null)
        {
            yield return NomeDoSelo;
        }
    }
}

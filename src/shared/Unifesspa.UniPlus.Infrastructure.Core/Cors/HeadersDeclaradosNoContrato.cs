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
            .SelectMany(Declarados)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Declarados(ControllerActionDescriptor acao) =>
        HeadersProprios(acao.MethodInfo)
            .Concat(SeloDeEntidade(acao.MethodInfo));

    private static IEnumerable<string> HeadersProprios(MethodInfo metodo) =>
        metodo.GetCustomAttributes<EmiteHeaderAttribute>(inherit: true).Select(static header => header.Nome);

    private static IEnumerable<string> SeloDeEntidade(MethodInfo metodo) =>
        metodo.GetCustomAttribute<EmiteETagAttribute>(inherit: true) is null ? [] : [NomeDoSelo];
}

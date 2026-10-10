namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.Selecao.API.Controllers;

/// <summary>
/// CA-08 da issue #1859: toda action pública de <see cref="InscricoesController"/> declara a
/// policy <c>Candidato</c> — <see cref="Infrastructure.Core.Authentication.OidcAuthenticationConfiguration.AddOidcAuthentication"/>
/// não registra fallback policy nenhuma, então uma action sem <c>[Authorize]</c> nasceria
/// anônima por omissão, não protegida pelo claim <c>cpf</c> que identifica o candidato.
/// </summary>
public sealed class InscricaoActionsDeclaramPolicyCandidatoTests
{
    [Fact(DisplayName = "Toda action pública de InscricoesController declara [Authorize(Policy = \"Candidato\")]")]
    public void Actions_DeInscricoesController_DeclaramPolicyCandidato()
    {
        MethodInfo[] actions = [.. typeof(InscricoesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static m => !m.IsSpecialName)];

        actions.Should().NotBeEmpty("o controller precisa ter ao menos uma action para este teste ter o que cobrar");

        IReadOnlyList<string> semPolicy = [.. actions
            .Where(static m => m.GetCustomAttribute<AuthorizeAttribute>() is not { Policy: "Candidato" })
            .Select(static m => m.Name)];

        semPolicy.Should().BeEmpty(
            $"toda action de InscricoesController precisa declarar [Authorize(Policy = \"Candidato\")] — sem ela, o " +
            $"endpoint nasce anônimo por omissão. Sem a policy: {string.Join(", ", semPolicy)}");
    }
}

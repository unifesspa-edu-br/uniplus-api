namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Linq;
using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;

using Unifesspa.UniPlus.Governance.Contracts;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>
/// Fitness test do <see cref="ExplicitlyUnscopedAttribute"/> (ADR-0056 §"Carve-out
/// read-side"): todo método marcado como unscoped deve carregar
/// <c>[Authorize(Roles = "plataforma-admin")]</c> — no próprio método ou no
/// tipo declarante. Background jobs, relatórios noturnos e admin reports que
/// legitimamente operam sem filtro de áreas precisam de assertion explícita
/// de role — o atributo sozinho não enforça autorização.
/// </summary>
/// <remarks>
/// <para><strong>Regra estrita</strong> (Codex P1 do plano #448):
/// <list type="bullet">
///   <item><description>Sem exceção para <c>[AllowAnonymous]</c>: unscoped não combina com leitura anônima.</description></item>
///   <item><description>Sem "comment fallback": justificativa estrutural via attribute, não texto em comentário.</description></item>
///   <item><description>Role obrigatória literal <c>plataforma-admin</c> em <c>Authorize.Roles</c>.</description></item>
/// </list></para>
/// <para>O <see cref="ExplicitlyUnscopedAttribute"/> aceita apenas
/// <see cref="AttributeTargets.Method"/>, então a regra inspeciona métodos
/// dos assemblies do produto via reflection. Não há call sites em V1
/// (atributo vai aparecer em F2 com background jobs do Parametrizacao).</para>
/// </remarks>
public sealed class ExplicitlyUnscopedRequiresPlataformaAdminTests
{
    private const string RoleObrigatoria = "plataforma-admin";

    [Fact(DisplayName = "ExplicitlyUnscoped: todo método com o atributo exige [Authorize(Roles=\"plataforma-admin\")]")]
    public void ExplicitlyUnscoped_ExigePlataformaAdmin()
    {
        ReflectionAssembly[] productAssemblies = ProductAssemblies();
        List<string> violations = [];

        foreach (ReflectionAssembly assembly in productAssemblies)
        {
            foreach (Type type in assembly.GetExportedTypes().Concat(GetInternalTypes(assembly)))
            {
                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    ExplicitlyUnscopedAttribute? unscoped = method.GetCustomAttribute<ExplicitlyUnscopedAttribute>();
                    if (unscoped is null)
                    {
                        continue;
                    }

                    bool temRolePlataformaAdmin = HasPlataformaAdminAuthorization(method) || HasPlataformaAdminAuthorization(type);
                    if (!temRolePlataformaAdmin)
                    {
                        violations.Add(
                            $"{type.FullName}.{method.Name}: [ExplicitlyUnscoped(Reason=\"{unscoped.Reason}\")] "
                            + $"sem [Authorize(Roles = \"{RoleObrigatoria}\")] no método nem no tipo. "
                            + "ADR-0056 exige assertion explícita de role para operações que ignoram o filtro de áreas.");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            $"ADR-0056 §\"Carve-out read-side\": [ExplicitlyUnscoped] precisa de "
            + $"[Authorize(Roles = \"{RoleObrigatoria}\")] adjacente. "
            + $"Violações:\n  - {string.Join("\n  - ", violations)}");
    }

    private static bool HasPlataformaAdminAuthorization(MemberInfo member)
    {
        IEnumerable<AuthorizeAttribute> auths = member.GetCustomAttributes<AuthorizeAttribute>();
        foreach (AuthorizeAttribute auth in auths)
        {
            if (string.IsNullOrEmpty(auth.Roles))
            {
                continue;
            }

            // Roles é CSV ("plataforma-admin,outra-role"). Aceita se contém a role
            // canônica em qualquer posição.
            IEnumerable<string> roles = auth.Roles
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roles.Contains(RoleObrigatoria, StringComparer.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<Type> GetInternalTypes(ReflectionAssembly assembly)
    {
        // Internal types também precisam ser inspecionados — endpoints podem ser
        // internal sealed em alguns projetos (controllers Selecao/OrganizacaoInstitucional
        // são public por exigência do MVC, mas helpers em Application/Infrastructure
        // podem ser internal).
        try
        {
            return assembly.GetTypes().Except(assembly.GetExportedTypes());
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>().Except(assembly.GetExportedTypes());
        }
    }

    private static ReflectionAssembly[] ProductAssemblies() =>
    [
        // Shared
        typeof(global::Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus).Assembly,
        typeof(global::Unifesspa.UniPlus.Infrastructure.Core.Messaging.WolverineOutboxConfiguration).Assembly,

        // Selecao
        typeof(global::Unifesspa.UniPlus.Selecao.Domain.Entities.Edital).Assembly,
        typeof(global::Unifesspa.UniPlus.Selecao.Application.Commands.Editais.CriarEditalCommand).Assembly,
        typeof(global::Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.SelecaoDbContext).Assembly,
        typeof(global::Unifesspa.UniPlus.Selecao.API.Controllers.EditalController).Assembly,

        // Ingresso
        typeof(global::Unifesspa.UniPlus.Ingresso.Domain.Entities.Chamada).Assembly,
        typeof(global::Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence.IngressoDbContext).Assembly,
        typeof(global::Unifesspa.UniPlus.Ingresso.API.IngressoApiAssemblyMarker).Assembly,

        // Portal
        typeof(global::Unifesspa.UniPlus.Portal.Domain.PortalDomainAssemblyMarker).Assembly,
        typeof(global::Unifesspa.UniPlus.Portal.Infrastructure.Persistence.PortalDbContext).Assembly,
        typeof(global::Unifesspa.UniPlus.Portal.API.PortalApiAssemblyMarker).Assembly,

        // OrganizacaoInstitucional
        typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities.AreaOrganizacional).Assembly,
        typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.AreasOrganizacionais.CriarAreaOrganizacionalCommand).Assembly,
        typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.OrganizacaoInstitucionalDbContext).Assembly,
        typeof(global::Unifesspa.UniPlus.OrganizacaoInstitucional.API.OrganizacaoApiAssemblyMarker).Assembly,

        // Parametrizacao
        typeof(global::Unifesspa.UniPlus.Parametrizacao.Domain.ParametrizacaoDomainAssemblyMarker).Assembly,
        typeof(global::Unifesspa.UniPlus.Parametrizacao.Application.ParametrizacaoApplicationAssemblyMarker).Assembly,
        typeof(global::Unifesspa.UniPlus.Parametrizacao.Contracts.ParametrizacaoContractsAssemblyMarker).Assembly,
        typeof(global::Unifesspa.UniPlus.Parametrizacao.Infrastructure.Persistence.ParametrizacaoDbContext).Assembly,
        typeof(global::Unifesspa.UniPlus.Parametrizacao.API.ParametrizacaoApiAssemblyMarker).Assembly,
    ];
}

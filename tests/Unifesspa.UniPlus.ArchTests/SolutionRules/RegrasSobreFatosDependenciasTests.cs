namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Fronteira do projeto compartilhado de regras sobre fatos do candidato (ADR-0135).
/// </summary>
/// <remarks>
/// Conferida pelas referências de assembly compiladas, e não por dependência entre tipos:
/// é a referência de projeto que cria o acoplamento que a ADR veda. As camadas dos módulos
/// podem depender das regras livremente; o que a regra impede é o projeto crescer para
/// fora do papel de lógica pura sobre valores, ou virar dependência de outro projeto
/// compartilhado de base.
/// </remarks>
public sealed class RegrasSobreFatosDependenciasTests
{
    [Fact(DisplayName = "as regras sobre fatos ficam dentro da fronteira da ADR-0135")]
    public void RegrasSobreFatos_RespeitamAFronteira()
    {
        Assembly regras = typeof(PredicadoDnf).Assembly;
        string[] referencias = [.. regras.GetReferencedAssemblies().Select(a => a.Name!)];

        // Lista do que é permitido, não do que é proibido: qualquer pacote de terceiro que
        // entre no projeto (validação, persistência, log) quebra a fronteira, não só os
        // três que ocorreriam primeiro.
        referencias.Where(n => n != "Unifesspa.UniPlus.Kernel"
                && n is not ("netstandard" or "mscorlib" or "System")
                && !n.StartsWith("System.", StringComparison.Ordinal))
            .Should().BeEmpty("o projeto de regras depende só do Kernel e do .NET");

        regras.GetTypes().Where(t => typeof(EntityBase).IsAssignableFrom(t))
            .Should().BeEmpty("o que depende de entidade fica no módulo dono dela");

        // O Kernel não entra: referenciar as regras de volta seria ciclo, que a build já recusa.
        string[] baseCompartilhada =
            ["Unifesspa.UniPlus.Governance.Contracts", "Unifesspa.UniPlus.Application.Abstractions", "Unifesspa.UniPlus.Authorization"];
        baseCompartilhada
            .Where(nome => Assembly.Load(nome).GetReferencedAssemblies().Any(a => a.Name == regras.GetName().Name))
            .Should().BeEmpty("fora das camadas dos módulos, só Infrastructure.Core depende das regras, para registrar os erros");
    }
}

namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;


public sealed class Stage1ArchitectureRulesTests
{
    private static readonly Architecture WolverineGuardArchitecture = ArquiteturaDaSelecao.Produto;

    /// <summary>
    /// R2: exceção estreita para <c>Wolverine.Attributes</c> (ADR-0003, emenda 2026-08-04) —
    /// só o namespace dos marcadores (<c>[NonTransactional]</c> etc.), lidos pelo codegen em
    /// build-time, zero acoplamento a <c>IMessageBus</c>/dispatch (confirmado por reflection:
    /// <c>NonTransactionalAttribute</c> deriva só de <c>System.Attribute</c>). Necessário para
    /// as consultas que registram o acesso no Kafka e não devem segurar a conexão numa transação
    /// ambiente (ex.: <c>ListarInscricoesDoProcessoQueryHandler</c>) — <c>[NonTransactional]</c> é o
    /// único mecanismo que o Wolverine expõe para excusar o handler do detector de transação
    /// (<c>AutoApplyTransactions</c>, ADR-0004); não há alternativa fluente registrável fora do atributo. O resto de <c>Wolverine.*</c> (bus,
    /// runtime, EF Core middleware) continua proibido.
    /// </summary>
    [Fact(DisplayName = "R2: Application.Abstractions, Selecao.Application e Selecao.Domain nao dependem de Wolverine (exceto Wolverine.Attributes)")]
    public void ApplicationEDomain_NaoDependemDeWolverine()
    {
        IArchRule rule = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.(Application\.Abstractions|Selecao\.(Domain|Application))(\.|$)")
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(@"^Wolverine(?!\.Attributes(\.|$))(\.|$)")
            .Because("ADR-0003 limita Wolverine a Infrastructure.Core; Application e Domain dependem apenas das abstracoes do projeto (exceção estreita a Wolverine.Attributes, emenda 2026-08-04).");

        rule.Check(WolverineGuardArchitecture);
    }

    [Fact(DisplayName = "F2: domínio e aplicação de Seleção não dependem de ASP.NET Core")]
    public void DomainEApplication_NaoDependemDeAspNetCore()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Domain|Application)(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore(\.|$)");

        regra.Check(WolverineGuardArchitecture);
    }

}

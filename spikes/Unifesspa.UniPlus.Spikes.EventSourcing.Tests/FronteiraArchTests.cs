using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G6: a fronteira EF Core × Marten é enforçável. Aqui, no spike isolado, a
/// regra demonstrável é que Domain e Application permanecem limpos de Marten e
/// Wolverine — só a Infrastructure conhece esses detalhes. Ao graduar para produção,
/// as regras completas (contexto CRUD não importa Marten; agregado ES não persiste
/// via DbContext; Kafka não é fonte canônica) migram para os ArchTests reais.
/// </summary>
public sealed class FronteiraArchTests
{
    private static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies(typeof(ConfiguracaoSpike).Assembly)
        .Build();

    private const string Dominio = @"^Unifesspa\.UniPlus\.Spikes\.EventSourcing\.Domain(\.|$)";
    private const string Aplicacao = @"^Unifesspa\.UniPlus\.Spikes\.EventSourcing\.Application(\.|$)";
    private const string Marten = @"^Marten(\.|$)";
    private const string Wolverine = @"^Wolverine(\.|$)";

    [Fact(DisplayName = "G6: Domain não depende de Marten")]
    public void Dominio_nao_depende_de_marten() => NaoDependeDe(Dominio, Marten);

    [Fact(DisplayName = "G6: Domain não depende de Wolverine")]
    public void Dominio_nao_depende_de_wolverine() => NaoDependeDe(Dominio, Wolverine);

    [Fact(DisplayName = "G6: Application não depende de Marten")]
    public void Aplicacao_nao_depende_de_marten() => NaoDependeDe(Aplicacao, Marten);

    [Fact(DisplayName = "G6: Application não depende de Wolverine")]
    public void Aplicacao_nao_depende_de_wolverine() => NaoDependeDe(Aplicacao, Wolverine);

    [Fact(DisplayName = "G6: eventos de domínio são sealed (fato imutável)")]
    public void Eventos_sao_sealed()
    {
        IArchRule regra = Classes().That()
            .ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Spikes\.EventSourcing\.Domain\.Eventos(\.|$)")
            .Should().BeSealed()
            .Because("eventos de domínio são fatos imutáveis");

        regra.Check(Arquitetura);
    }

    private static void NaoDependeDe(string origem, string proibido)
    {
        IArchRule regra = Types().That()
            .ResideInNamespaceMatching(origem)
            .Should().NotDependOnAnyTypesThat()
            .ResideInNamespaceMatching(proibido)
            .Because("Domain e Application permanecem limpos; só a Infrastructure conhece a stack de persistência/mensageria");

        regra.Check(Arquitetura);
    }
}

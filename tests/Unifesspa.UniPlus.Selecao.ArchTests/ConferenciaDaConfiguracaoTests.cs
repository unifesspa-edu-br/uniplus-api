namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// A conferência da configuração do processo e a restauração da configuração congelada julgam coisas diferentes: a primeira
/// avalia o que o autor declarou contra os cadastros vivos, a segunda só confere a estrutura do documento já congelado. Nenhuma
/// conhece a outra, e a configuração apta a congelar só nasce de <c>Conferir</c>.
/// </summary>
public sealed class ConferenciaDaConfiguracaoTests
{
    private const string NamespaceDaConferencia = @"^Unifesspa\.UniPlus\.Selecao\.Domain\.ProcessosSeletivos\.ConferenciaDaConfiguracao(\.|$)";
    private const string NamespaceDaRestauracao = @"^Unifesspa\.UniPlus\.Selecao\.Domain\.ProcessosSeletivos\.Restauracao(\.|$)";

    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(typeof(ProcessoSeletivo).Assembly).Build();

    [Fact(DisplayName = "A conferência da configuração não depende da restauração")]
    public void Conferencia_NaoDependeDaRestauracao()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(NamespaceDaConferencia)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(NamespaceDaRestauracao)
            .Because("a conferência julga o que foi declarado; a restauração não reinterpreta o passado congelado");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A restauração da configuração não depende da conferência")]
    public void Restauracao_NaoDependeDaConferencia()
    {
        IArchRule regra = Types()
            .That().ResideInNamespaceMatching(NamespaceDaRestauracao)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(NamespaceDaConferencia)
            .Because("a restauração confere só a estrutura do documento congelado, sem os cadastros da conferência");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A configuração apta a congelar não tem construtor público")]
    public void ConfiguracaoAptaACongelar_NaoTemConstrutorPublico()
    {
        IArchRule regra = MethodMembers()
            .That().AreConstructors().And().AreDeclaredIn(typeof(ConfiguracaoAptaACongelar))
            .Should().NotBePublic()
            .Because("a configuração apta só nasce de Conferir, que julga todas as famílias de regra");

        regra.Check(Arquitetura);
    }
}

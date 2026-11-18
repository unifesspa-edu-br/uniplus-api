namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// A conferência da configuração do processo avalia o que o autor declarou contra os cadastros vivos, e a configuração
/// apta a congelar só nasce de <c>Conferir</c>.
/// </summary>
public sealed class ConferenciaDaConfiguracaoTests
{
    private static readonly Architecture Arquitetura = new ArchLoader().LoadAssemblies(typeof(ProcessoSeletivo).Assembly).Build();

    private static readonly Architecture ArquiteturaDosConsumidores = new ArchLoader().LoadAssemblies(
        typeof(ProcessoSeletivo).Assembly,
        typeof(Application.Commands.ProcessosSeletivos.CriarProcessoSeletivoCommand).Assembly,
        typeof(API.Controllers.InscricoesController).Assembly).Build();

    private static readonly System.Type[] TiposDosBlocos =
    [
        typeof(IdentidadeDoProcessoDeclarada), typeof(CronogramaDeclarado), typeof(AvaliacaoDeclarada),
        typeof(VagasDeclaradas), typeof(ExigenciasDeclaradas), typeof(ColetaDeclarada), typeof(DivulgacaoETaxaDeclaradas),
    ];

    [Fact(DisplayName = "A configuração apta a congelar não tem construtor público")]
    public void ConfiguracaoAptaACongelar_NaoTemConstrutorPublico()
    {
        IArchRule regra = MethodMembers()
            .That().AreConstructors().And().AreDeclaredIn(typeof(ConfiguracaoAptaACongelar))
            .Should().NotBePublic()
            .Because("a configuração apta só nasce de Conferir, que julga todos os assuntos de regra");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A configuração declarada e a origem da leitura não têm construtor público")]
    public void ConfiguracaoDeclarada_EOrigem_NaoTemConstrutorPublico()
    {
        IArchRule regra = MethodMembers()
            .That().AreConstructors().And().AreDeclaredIn(typeof(ConfiguracaoDeclarada), typeof(OrigemNoDocumento), typeof(ParteRecusada))
            .Should().NotBePublic()
            .Because("a configuração declarada só nasce da raiz, com cópias das coleções do agregado, ou da leitura do documento");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "Application e API não constroem os blocos da configuração declarada")]
    public void BlocosDaConfiguracao_NaoSaoConstruidosNaApplicationNemNaApi()
    {
        IArchRule regra = Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Application|API)(\.|$)")
            .Should().NotCallAny(MethodMembers().That().AreConstructors().And().AreDeclaredIn(TiposDosBlocos))
            .Because("a raiz e a leitura do envelope montam os blocos; os casos de uso apenas os consomem");

        regra.Check(ArquiteturaDosConsumidores);
    }

    [Fact(DisplayName = "A adoção da fase e da etapa lidas do documento, sem conferência, não é pública")]
    public void AdocaoSemConferencia_NaoEPublica()
    {
        IArchRule regra = MethodMembers()
            .That().AreDeclaredIn(typeof(FaseCronograma), typeof(EtapaProcesso)).And().HaveNameStartingWith("Adotar(")
            .Should().NotBePublic()
            .Because("a conferência da declaração adotada é do congelamento, e só a leitura do documento, dentro do domínio, adota sem conferir");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "A origem no documento e a parte recusada não dependem da fase nem da etapa")]
    public void OrigemNoDocumento_NaoDependeDaFaseNemDaEtapa()
    {
        IArchRule regra = Types()
            .That().Are(typeof(OrigemNoDocumento), typeof(ParteRecusada))
            .Should().NotDependOnAnyTypesThat().Are(typeof(FaseCronograma), typeof(EtapaProcesso), typeof(FaseDeclarada), typeof(EtapaDeclarada))
            .Because("a origem liga listas nomeadas e posições, e quem lê a fase e a etapa recebe dela só os nomes das listas com parte recusada");

        regra.Check(Arquitetura);
    }
}

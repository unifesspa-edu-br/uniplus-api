namespace Unifesspa.UniPlus.Selecao.ArchTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// A autoridade para assinar e a lista de assinantes são regra de domínio pura: dependem só do Kernel
/// e de si mesmas, e nunca do motor de autorização. A hierarquia das unidades decide quem pode constar
/// no documento; não concede acesso (ADR-0079).
/// </summary>
public sealed class AssinantesDaFaseArquiteturaTests
{
    private const string NamespaceDosAssinantes = @"^Unifesspa\.UniPlus\.Selecao\.Domain\.ProcessosSeletivos\.Assinantes(\.|$)";
    private const string MotorDeAutorizacao = @"^Unifesspa\.UniPlus\.(Authorization|Autorizacao)(\.|$)";
    private const string CamadasDeFora = @"^Unifesspa\.UniPlus\.(Selecao\.(Application|Infrastructure|API)|Infrastructure\.Core|Governance|OrganizacaoInstitucional|Autorizacao|Publicacoes|Ingresso|Portal|Configuracao|Discentes)(\.|$)";

    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;

    private static readonly Architecture ArquiteturaDosModulos = ArquiteturaDaSelecao.Produto;

    [Fact(DisplayName = "Os tipos dos assinantes não dependem de Application, Infrastructure, API nem do cadastro de outro módulo")]
    public void Assinantes_NaoDependemDeCamadasDeFora()
    {
        IArchRule regra = Types().That().ResideInNamespaceMatching(NamespaceDosAssinantes)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(CamadasDeFora)
            .Because("a regra de quem assina é função pura do domínio sobre dados que a Application entrega");

        regra.Check(Arquitetura);
    }

    [Fact(DisplayName = "Os tipos dos assinantes não dependem do motor de autorização")]
    public void Assinantes_NaoDependemDoMotorDeAutorizacao()
    {
        IArchRule regra = Types().That().ResideInNamespaceMatching(NamespaceDosAssinantes)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(MotorDeAutorizacao)
            .Because("constar na lista de assinantes não concede permissão, e a permissão não decide quem assina");

        regra.Check(Arquitetura);
    }

    [Theory(DisplayName = "A regra das camadas de fora alcança cada alvo proibido que uma dependência real atinge")]
    [InlineData(@"^Unifesspa\.UniPlus\.Selecao\.Application(\.|$)")]
    [InlineData(@"^Unifesspa\.UniPlus\.Infrastructure\.Core(\.|$)")]
    [InlineData(@"^Unifesspa\.UniPlus\.Governance(\.|$)")]
    [InlineData(@"^Unifesspa\.UniPlus\.OrganizacaoInstitucional(\.|$)")]
    public void RegraDasCamadasDeFora_DetectaDependenciaProibida(string alvoDaViolacao)
    {
        IArchRule regra = Types().That().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Selecao\.(Infrastructure|API|Application|ArchTests\.Modelos)(\.|$)")
            .Should().NotDependOnAnyTypesThat().Are(
                Types().That().ResideInNamespaceMatching(CamadasDeFora).And().ResideInNamespaceMatching(alvoDaViolacao));

        Action verificar = () => regra.Check(ArquiteturaDosModulos);

        verificar.Should().Throw<FailedArchRuleException>();
    }

    [Fact(DisplayName = "A regra de dependência detecta a violação: os assinantes dependem do Kernel, e a regra que o proíbe falha")]
    public void RegraDeDependencia_DetectaAViolacao()
    {
        IArchRule regraQueOKernelViola = Types().That().ResideInNamespaceMatching(NamespaceDosAssinantes)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Unifesspa\.UniPlus\.Kernel(\.|$)");

        Action verificar = () => regraQueOKernelViola.Check(Arquitetura);

        verificar.Should().Throw<FailedArchRuleException>();
    }
}

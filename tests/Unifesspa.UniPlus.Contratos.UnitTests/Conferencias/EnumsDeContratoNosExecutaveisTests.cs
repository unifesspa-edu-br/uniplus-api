namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Portal.API;
using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Infrastructure;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;

public sealed class EnumsDeContratoNosExecutaveisTests
{
    [Fact]
    public void Conferencia_Verificador_NaoEncontraDefeito() =>
        FluentActions.Invoking(static () => EnumsDeContrato.ConferirNoExecutavel(typeof(VerificadorOptions).Assembly)).Should().NotThrow();

    [Fact]
    public void Alcance_Host_IncluiBibliotecasEAcoes()
    {
        IReadOnlyList<Assembly> alcancados = AssembliesDoUniPlus.AlcancadosAPartirDe(typeof(HostAssemblyMarker).Assembly);
        alcancados.Should().Contain(typeof(AssembliesDoUniPlus).Assembly).And.Contain(typeof(EntradaDeRegras).Assembly)
            .And.Contain(typeof(ConferenciaDosEnumsDeContratoNaSubida).Assembly).And.Contain(typeof(MonolitoDbContext).Assembly);
        IEnumerable<ControllerActionDescriptor> acoes = HostsDoProdutoIniciados.Obter(HostsDoProdutoIniciados.UniPlus).ProvedorDeServicos
            .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>();
        foreach (ControllerActionDescriptor acao in acoes)
        {
            alcancados.Should().Contain(acao.ControllerTypeInfo.Assembly);
        }
    }

    [Fact]
    public void Alcance_Portal_IncluiBibliotecasEProjetos() =>
        AssembliesDoUniPlus.AlcancadosAPartirDe(typeof(PortalApiAssemblyMarker).Assembly)
            .Should().Contain(typeof(AssembliesDoUniPlus).Assembly).And.Contain(typeof(ConferenciaDosEnumsDeContratoNaSubida).Assembly)
            .And.Contain(typeof(ComposicaoDaPaginaDoCertame).Assembly).And.Contain(typeof(PortalInfrastructureRegistration).Assembly);

    [Fact]
    public void Alcance_Verificador_IncluiContratosSemInfraestruturaDaApi()
    {
        IReadOnlyList<Assembly> alcancados = AssembliesDoUniPlus.AlcancadosAPartirDe(typeof(VerificadorOptions).Assembly);
        alcancados.Should().Contain(typeof(AssembliesDoUniPlus).Assembly).And.Contain(typeof(DestinoDoResultado).Assembly);
        alcancados.Should().NotContain(typeof(ConferenciaDosEnumsDeContratoNaSubida).Assembly);
        alcancados.Should().NotContain(static a => a.GetName().Name!.StartsWith("Unifesspa.UniPlus.Selecao", StringComparison.Ordinal));
    }
}

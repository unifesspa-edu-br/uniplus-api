namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class ExigePermissaoUsaPermissaoDoEnumCertoTests
{
    [Fact]
    public void Acoes_ComExigenciaDeAutorizacao_ConstroemSeusMetadados()
    {
        object[] exigencias = [.. HostsDoProdutoIniciados.Executaveis
            .SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos
                .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items)
            .SelectMany(static acao => acao.EndpointMetadata.OfType<IExigeAutorizacaoAlemDoLogin>())];

        exigencias.Should().NotBeEmpty();
    }
}

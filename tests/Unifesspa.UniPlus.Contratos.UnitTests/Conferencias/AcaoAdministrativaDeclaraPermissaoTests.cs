namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.TestSupport;

public sealed class AcaoAdministrativaDeclaraPermissaoTests
{
    [Fact]
    public void AcoesAdministrativas_DeclaramDecisaoOuExcecaoJustificada()
    {
        ControllerActionDescriptor[] acoes = [.. HostsDoProdutoIniciados.Executaveis
            .SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos
                .GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items)
            .OfType<ControllerActionDescriptor>()];
        string[] nomes = [.. acoes.Select(Nome)];
        nomes.Should().NotBeEmpty();
        ExcecoesDeAcessoDasAcoes.DecididasNoHandler.Keys.Concat(ExcecoesDeAcessoDasAcoes.DePessoaAutenticada)
            .Concat(ExcecoesDeAcessoDasAcoes.DoTitular).Should().BeSubsetOf(nomes);
        acoes.Where(static a => DeclaraDecisao(a.EndpointMetadata))
            .Select(static a => a.ControllerTypeInfo.Assembly).Distinct().Should().HaveCountGreaterThan(1);
        acoes.Where(static a => SemDecisao(a.EndpointMetadata) && !ExcecoesDeAcessoDasAcoes.DecididasNoHandler.ContainsKey(Nome(a))
            && !ExcecoesDeAcessoDasAcoes.DePessoaAutenticada.Contains(Nome(a)) && !ExcecoesDeAcessoDasAcoes.DoTitular.Contains(Nome(a)))
            .Select(Nome).Should().BeEmpty();
    }

    [Fact]
    public void Metadados_SemDecisao_SaoRecusados()
    {
        SemDecisao([]).Should().BeTrue();
        SemDecisao([new AuthorizeAttribute()]).Should().BeTrue();
        SemDecisao([new AuthorizeAttribute("Instalacao")]).Should().BeFalse();
        SemDecisao([new AllowAnonymousAttribute()]).Should().BeFalse();
        SemDecisao([new ExigePermissaoNoProcessoAttribute(PermissaoSelecao.ProcessosConfigurar)]).Should().BeFalse();
    }

    private static string Nome(ControllerActionDescriptor acao) =>
        ExcecoesDeAcessoDasAcoes.Acao(acao.ControllerTypeInfo.AsType(), acao.MethodInfo.Name);

    private static bool SemDecisao(IList<object> metadados) =>
        !metadados.OfType<IAllowAnonymous>().Any()
        && !metadados.OfType<IAuthorizeData>().Any(static a => a.Policy == "Instalacao")
        && !DeclaraDecisao(metadados);

    private static bool DeclaraDecisao(IList<object> metadados) =>
        metadados.OfType<IExigeAutorizacaoAlemDoLogin>().Any();
}

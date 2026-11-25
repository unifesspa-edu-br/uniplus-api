namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Text.RegularExpressions;

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent.Syntax.Elements.Types;
using ArchUnitNET.xUnit;

using AwesomeAssertions;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Selecao.Application.Registro;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

public sealed class ServicosRecebidosPeloConstrutorTests
{
    private static readonly Architecture Arquitetura = ArquiteturaDaSelecao.Produto;
    private static readonly IType[] Servicos = ObterServicos();

    [Fact]
    public void ServicosRegistrados_ContemOsServicosDoModulo()
    {
        Servicos.Select(static servico => servico.FullName).Should().NotBeEmpty()
            .And.Contain(typeof(ContextoDaVersaoFixadaResolver).FullName)
            .And.Contain(typeof(DocumentosEnviadosDaInscricao).FullName)
            .And.Contain(typeof(CalculadorDoIndiceCegoDoCpf).FullName);
    }

    [Fact]
    public void ServicosRegistrados_PorFabrica_ContemOsServicos() =>
        Servicos.Select(static servico => servico.FullName).Should()
            .Contain(typeof(InscricaoDoSujeitoNoProcesso).FullName)
            .And.Contain(typeof(ArmazenamentoDoCandidato).FullName)
            .And.Contain(typeof(AcervoPublicoService).FullName);

    [Fact]
    public void ServicosRegistrados_NasCamadasDaSelecao_NaoSaoConstruidosDiretamente()
    {
        RegraDeConstrucaoDeServicos(Types().That().ResideInNamespaceMatching(
            @"^Unifesspa\.UniPlus\.Selecao\.(Application|Infrastructure|API)(\.|$)")
            .And().AreNot(typeof(SelecaoInfrastructureRegistration)))
            .Check(Arquitetura);
    }

    [Fact]
    public void ConstrucaoDeServico_EmMetodoAssincrono_RegraReprova()
    {
        string nome = typeof(ConstrucaoDeExemplo).FullName!;
        TypesShouldConjunctionWithDescription regra = RegraDeConstrucaoDeServicos(
            Types().That().HaveFullNameMatching($@"^{Regex.Escape(nome)}(\+|$)"));

        regra.Evaluate(Arquitetura).Should().Contain(static resultado => !resultado.Passed);
    }

    private static TypesShouldConjunctionWithDescription RegraDeConstrucaoDeServicos(GivenTypesConjunction tipos) =>
        tipos.Should().NotCallAny(MethodMembers().That().AreConstructors().And().AreDeclaredIn(Servicos))
            .Because("o serviço chega pronto pelo construtor ou pelo parâmetro do tratador (ADR-0167); classes de registro compõem serviços e validadores podem compor validadores filhos");

    private static IType[] ObterServicos()
    {
        ServiceCollection servicos = new();
        servicos.AddSelecaoApplication();
        servicos.AddSelecaoInfrastructure();

        ServiceCollection validadores = new();
        validadores.AddValidatorsFromAssembly(typeof(SelecaoApplicationServiceRegistration).Assembly);
        System.Type[] tiposDeValidadores = [.. validadores.Select(static registro => registro.ImplementationType).OfType<System.Type>()];

        return [.. ObterTiposDosServicos(servicos).Where(tipo => !tiposDeValidadores.Any(validador => validador.FullName == tipo.FullName))];
    }

    private static IType[] ObterTiposDosServicos(ServiceCollection servicos)
    {
        return [.. servicos.SelectMany(static registro => registro.ImplementationType is { } implementacao
                ? Arquitetura.Types.Where(tipo => tipo.FullName == implementacao.FullName?.Split('[', 2)[0]
                    && PertenceAsCamadasDaSelecao(tipo))
                : registro.ImplementationFactory is not null
                    ? ObterTiposDaFabrica(registro.ServiceType)
                    : [])
            .Distinct()];
    }

    private static IEnumerable<IType> ObterTiposDaFabrica(System.Type tipoDeServico) =>
        Arquitetura.Classes.Where(classe => classe.IsAbstract == false
            && (classe.FullName == tipoDeServico.FullName
                || (PertenceAsCamadasDaSelecao(classe)
                    && classe.ImplementedInterfaces.Any(contrato => contrato.FullName == tipoDeServico.FullName))));

    private static bool PertenceAsCamadasDaSelecao(IType tipo) =>
        tipo.Assembly.Name == typeof(SelecaoApplicationServiceRegistration).Assembly.GetName().Name
        || tipo.Assembly.Name == typeof(SelecaoInfrastructureRegistration).Assembly.GetName().Name;

    private static class ConstrucaoDeExemplo
    {
        internal static async Task CarregarAsync(IDocumentoDaInscricaoRepository repositorio) =>
            await new DocumentosEnviadosDaInscricao(repositorio).CarregarAsync(Guid.Empty, CancellationToken.None).ConfigureAwait(false);
    }
}

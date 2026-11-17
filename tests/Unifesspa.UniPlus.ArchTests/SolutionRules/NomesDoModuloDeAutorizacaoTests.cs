namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures.Canario;
using Unifesspa.UniPlus.Autorizacao.API.Controllers;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os nomes do módulo de autorização dizem o que cada coisa guarda: "retrato" é metáfora e não nomeia tipo nem membro do
/// domínio, da aplicação, da infraestrutura nem da API. A regra é a mesma que cada projeto de teste do módulo aplica ao
/// próprio assembly, e o canário prova que ela acusa o tipo e o método auxiliar de teste com o termo.
/// </summary>
public sealed class NomesDoModuloDeAutorizacaoTests
{
    private const string NamespaceDoCanario = @"^Unifesspa\.UniPlus\.ArchTests\.Fixtures\.Canario$";

    public static TheoryData<Assembly> AssembliesDeProducao => new()
    {
        typeof(Perfil).Assembly,
        typeof(ConferenciaDoCatalogoNaVersao).Assembly,
        typeof(ConciliacaoDoCatalogo).Assembly,
        typeof(AprovacaoDeVersoesController).Assembly,
    };

    [Theory(DisplayName = "Nenhum tipo nem membro de produção do módulo de autorização leva retrato no nome")]
    [MemberData(nameof(AssembliesDeProducao))]
    public void Producao_NaoLevaMetaforaNoNome(Assembly assembly) =>
        RegraSemMetaforaNosNomes.Violacoes(assembly).Should().BeEmpty();

    [Theory(DisplayName = "A regra enxerga os tipos de cada assembly de produção, para não passar vazia")]
    [MemberData(nameof(AssembliesDeProducao))]
    public void Regra_EnxergaOsTiposDeProducao(Assembly assembly) =>
        RegraSemMetaforaNosNomes.TiposVistos(assembly).Should().BeGreaterThan(0);

    [Fact(DisplayName = "TipoEMetodoAuxiliarComRetrato_RegraIdentificaAmbos")]
    public void TipoEMetodoAuxiliarComRetrato_RegraIdentificaAmbos() =>
        RegraSemMetaforaNosNomes.Violacoes(typeof(ApoioDeTeste).Assembly, NamespaceDoCanario).Should()
            .Contain(static v => v.Contains(nameof(RetratoDoPerfilDeTeste), StringComparison.Ordinal))
            .And.Contain(static v => v.Contains("RebaixarRetratoAsync", StringComparison.Ordinal))
            .And.NotContain(static v => v.Contains("RebaixarClassificacaoAsync", StringComparison.Ordinal));
}

namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using ArchUnitNET.Fluent.Syntax.Elements.Members.MethodMembers;
using ArchUnitNET.Fluent.Syntax.Elements.Types;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.Fixtures;
using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// A convenção dos enums de contrato (ADR-0176) só vale se a montagem do modelo a chama: o contexto do portal em
/// <c>OnModelCreating</c> e a composição do modelo do contexto único. O modelo do portal está vazio, e os modelos
/// sintéticos chamam a extensão por conta própria, então remover a chamada de um desses dois lugares não reprovaria nenhum
/// teste de metadados do modelo. A composição chama a convenção num auxiliar privado que o <c>Compor</c> invoca, por isso
/// a regra dela é do tipo; que o <c>Compor</c> chega ao auxiliar é coberto pelo teste que monta o modelo do contexto único.
/// </summary>
public sealed class ConversaoDosEnumsDeContratoChamadaNaMontagemDoModeloTests
{
    private const string ComposicaoDoModelo = "Unifesspa.UniPlus.Infrastructure.Core.Persistence.ComposicaoDoModelo";

    [Fact(DisplayName = "O `OnModelCreating` do contexto do portal chama a convenção dos enums de contrato")]
    public void ContextoDoPortal_ChamaAConvencao()
    {
        OnModelCreatingDe(typeof(PortalDbContext)).GetObjects(ArquiteturaDasTravas.Produto).Should()
            .ContainSingle("sem o método descoberto, a regra passaria vazia");

        ArquiteturaDasTravas.Reprovacoes(OnModelCreatingDe(typeof(PortalDbContext)).Should().CallAny(Convencao()), ArquiteturaDasTravas.Produto)
            .Should().BeEmpty("sem a chamada, o enum com código do portal seria gravado pelo número");
    }

    [Fact(DisplayName = "A composição do modelo do contexto único chama a convenção dos enums de contrato")]
    public void ComposicaoDoModelo_ChamaAConvencao()
    {
        Types().That().HaveFullName(ComposicaoDoModelo).GetObjects(ArquiteturaDasTravas.Produto).Should()
            .ContainSingle("sem o tipo descoberto, a regra passaria vazia");

        ArquiteturaDasTravas.Reprovacoes(ChamaAConvencao(Types().That().HaveFullName(ComposicaoDoModelo)), ArquiteturaDasTravas.Produto)
            .Should().BeEmpty("sem a chamada, o enum com código de todos os módulos seria gravado pelo número");
    }

    [Fact(DisplayName = "Um contexto que não chama a convenção dos enums de contrato é reprovado pela regra")]
    public void Contraprova_ContextoQueNaoChamaAConvencao_Reprovado()
    {
        Type semChamada = typeof(FixturesDeConversaoDosEnumsDeContrato.ContextoQueNaoConfiguraAConversao);
        Type comChamada = typeof(FixturesDeConversaoDosEnumsDeContrato.ContextoQueConfiguraAConversao);

        OnModelCreatingDe(semChamada).GetObjects(ArquiteturaDasTravas.Fixtures).Should().ContainSingle();
        OnModelCreatingDe(comChamada).GetObjects(ArquiteturaDasTravas.Fixtures).Should().ContainSingle();
        ArquiteturaDasTravas.Reprovacoes(OnModelCreatingDe(semChamada).Should().CallAny(Convencao()), ArquiteturaDasTravas.Fixtures).Should().NotBeEmpty();
        ArquiteturaDasTravas.Reprovacoes(OnModelCreatingDe(comChamada).Should().CallAny(Convencao()), ArquiteturaDasTravas.Fixtures).Should().BeEmpty();
    }

    [Fact(DisplayName = "Uma composição que não chama a convenção dos enums de contrato é reprovada pela regra")]
    public void Contraprova_ComposicaoQueNaoChamaAConvencao_Reprovada()
    {
        string semChamada = typeof(FixturesDeConversaoDosEnumsDeContrato.ComposicaoQueNaoConfiguraAConversao).FullName!;
        string comChamada = typeof(FixturesDeConversaoDosEnumsDeContrato.ComposicaoQueConfiguraAConversao).FullName!;

        Types().That().HaveFullName(semChamada).GetObjects(ArquiteturaDasTravas.Fixtures).Should().ContainSingle();
        Types().That().HaveFullName(comChamada).GetObjects(ArquiteturaDasTravas.Fixtures).Should().ContainSingle();
        ArquiteturaDasTravas.Reprovacoes(ChamaAConvencao(Types().That().HaveFullName(semChamada)), ArquiteturaDasTravas.Fixtures).Should().NotBeEmpty();
        ArquiteturaDasTravas.Reprovacoes(ChamaAConvencao(Types().That().HaveFullName(comChamada)), ArquiteturaDasTravas.Fixtures).Should().BeEmpty();
    }

    private static GivenMethodMembersConjunction OnModelCreatingDe(Type contexto) =>
        MethodMembers().That().HaveNameStartingWith("OnModelCreating(").And().AreDeclaredIn(contexto);

    private static GivenMethodMembersConjunction Convencao() =>
        MethodMembers().That().HaveNameStartingWith("ConfigurarConversaoDosEnumsDeContrato(")
            .And().AreDeclaredIn(typeof(ModelBuilderCodigosDeContratoExtensions));

    private static TypesShouldConjunction ChamaAConvencao(GivenTypesConjunction tipo) => tipo.Should().CallAny(Convencao());
}

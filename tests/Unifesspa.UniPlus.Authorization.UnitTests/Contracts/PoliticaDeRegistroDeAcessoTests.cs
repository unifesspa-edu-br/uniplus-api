namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.UnitTests.Decisao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

public sealed class PoliticaDeRegistroDeAcessoTests
{
    private static readonly PermissionRequirement Interno = Requisito();

    private static readonly PermissionRequirement Pessoal =
        Requisito(ClassificacaoDeProtecao.Pessoal, baseLegalPadrao: "Lei nº 13.709/2018 (LGPD), art. 7º, III");

    [Theory(DisplayName = "O pedido grava a negativa de qualquer sensibilidade e a permitida só de dado pessoal")]
    [InlineData(ResultadoDoAcesso.Negado, true, true)]
    [InlineData(ResultadoDoAcesso.Impedido, true, true)]
    [InlineData(ResultadoDoAcesso.Permitido, true, false)]
    public void Pedido_GravaConformeSensibilidade(ResultadoDoAcesso resultado, bool gravaPessoal, bool gravaInterno)
    {
        PoliticaDeRegistroDeAcesso.Desfecho([DecisaoDeAcessoDeTeste.Tomada(resultado, Pessoal)], IntencaoDoRegistro.Pedido)
            .Any().Should().Be(gravaPessoal);
        PoliticaDeRegistroDeAcesso.Desfecho([DecisaoDeAcessoDeTeste.Tomada(resultado, Interno)], IntencaoDoRegistro.Pedido)
            .Any().Should().Be(gravaInterno);
    }

    [Fact(DisplayName = "Pedido com duas permitidas grava só a primeira que autorizou")]
    public void Pedido_DuasPermitidas_GravaAPrimeira()
    {
        DecisaoDeAcesso primeira = DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal);
        DecisaoDeAcesso segunda = DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal);

        IReadOnlyList<RegistroDecisaoAcesso> registros = PoliticaDeRegistroDeAcesso.Desfecho([primeira, segunda], IntencaoDoRegistro.Pedido);

        registros.Should().ContainSingle().Which.Should().BeSameAs(((DecisaoPermitida)primeira).Registro);
    }

    [Fact(DisplayName = "Aprovador com duas permitidas grava todas as que habilitaram a aprovação")]
    public void Aprovador_DuasPermitidas_GravaTodas()
    {
        DecisaoDeAcesso[] permitidas =
        [
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal),
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal),
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Pessoal),
        ];

        PoliticaDeRegistroDeAcesso.Desfecho(permitidas, IntencaoDoRegistro.Aprovador).Should().HaveCount(2)
            .And.OnlyContain(static r => r.Permitido);
    }

    [Fact(DisplayName = "Projeção com duas permitidas grava todas, de qualquer sensibilidade")]
    public void Projecao_DuasPermitidas_GravaTodas()
    {
        DecisaoDeAcesso[] permitidas =
        [
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Interno),
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal),
        ];

        PoliticaDeRegistroDeAcesso.Desfecho(permitidas, IntencaoDoRegistro.Projecao).Should().HaveCount(2);
    }

    [Theory(DisplayName = "Etapa de aprovação: a decisão de permissão de dado pessoal, permitida ou negada, gera um registro")]
    [InlineData(ResultadoDoAcesso.Permitido)]
    [InlineData(ResultadoDoAcesso.Negado)]
    public void Aprovador_PermissoesPessoais_UmRegistroPorDecisao(ResultadoDoAcesso resultado)
    {
        DecisaoDeAcesso decisao = DecisaoDeAcessoDeTeste.Tomada(resultado, Pessoal);

        PoliticaDeRegistroDeAcesso.Desfecho([decisao], IntencaoDoRegistro.Aprovador).Should().ContainSingle()
            .Which.Permitido.Should().Be(resultado == ResultadoDoAcesso.Permitido);
    }

    [Fact(DisplayName = "Projeção nunca grava a negada: quem perguntou foi o sistema")]
    public void Projecao_Negada_NaoGrava()
    {
        PoliticaDeRegistroDeAcesso.Desfecho(
            [DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Pessoal)], IntencaoDoRegistro.Projecao).Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem nenhuma permitida, grava as negadas de todas as decisões")]
    public void Pedido_NenhumaPermitida_GravaAsNegadas()
    {
        DecisaoDeAcesso[] decisoes =
        [
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Pessoal),
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Impedido, Pessoal),
        ];

        PoliticaDeRegistroDeAcesso.Desfecho(decisoes, IntencaoDoRegistro.Pedido).Select(static r => r.Resultado)
            .Should().Equal(ResultadoDoAcesso.Negado, ResultadoDoAcesso.Impedido);
    }

    [Fact(DisplayName = "Decisão sem sujeito, na principal ou na lista, anula todo o registro do desfecho")]
    public void SemSujeito_EmQualquerPosicao_NaoGrava()
    {
        DecisaoDeAcesso permitida = DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Pessoal);

        PoliticaDeRegistroDeAcesso.Desfecho([SemSujeito.Instancia], IntencaoDoRegistro.Pedido).Should().BeEmpty();
        PoliticaDeRegistroDeAcesso.Desfecho([SemSujeito.Instancia, permitida], IntencaoDoRegistro.Pedido).Should().BeEmpty();
        PoliticaDeRegistroDeAcesso.Desfecho([permitida, SemSujeito.Instancia], IntencaoDoRegistro.Projecao).Should().BeEmpty();
    }

    [Fact(DisplayName = "A negativa por concessão em unidade exigida é gravada")]
    public void Pedido_ConcessaoEmUnidadeExigida_Grava()
    {
        ResourceContext resource = Resource(ClassificacaoDeProtecao.Interno);
        RegistroDecisaoAcesso registro = RegistroDecisaoAcesso.De(
            Sujeito(), Interno, resource, Requisicao(), AuthorizationDecision.Negar(MotivoNegativa.ConcessaoEmUnidadeExigida));

        PoliticaDeRegistroDeAcesso.Desfecho([DecisaoDeAcesso.Tomada(Interno, resource, registro)], IntencaoDoRegistro.Pedido)
            .Should().ContainSingle().Which.MotivoNegativa.Should().Be("concessao_em_unidade_exigida");
    }

    [Fact(DisplayName = "Principal negada com alternativa permitida grava só a permitida")]
    public void Pedido_PrincipalNegadaAlternativaPermitida_NaoGravaANegada()
    {
        DecisaoDeAcesso negada = DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Interno);
        DecisaoDeAcesso permitida = DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Permitido, Interno);

        PoliticaDeRegistroDeAcesso.Desfecho([negada, permitida], IntencaoDoRegistro.Pedido).Should().BeEmpty();
    }

    [Fact(DisplayName = "Principal e alternativa negadas gravam as duas negativas")]
    public void Pedido_PrincipalEAlternativaNegadas_GravaAsDuas()
    {
        DecisaoDeAcesso[] negadas =
        [
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Interno),
            DecisaoDeAcessoDeTeste.Tomada(ResultadoDoAcesso.Negado, Interno),
        ];

        PoliticaDeRegistroDeAcesso.Desfecho(negadas, IntencaoDoRegistro.Pedido).Should().HaveCount(2);
    }

    [Fact(DisplayName = "Por resource exibido grava a permitida de qualquer sensibilidade, um registro por id")]
    public void PorResource_Permitida_GravaUmPorId()
    {
        DecisaoPermitida permitida = DecisaoDeAcessoDeTeste.Permitida(Interno);
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];

        PoliticaDeRegistroDeAcesso.PorResource(permitida, "inscricao", ids, resourceEhInscricao: true)
            .Select(static r => r.ResourceId).Should().BeEquivalentTo(ids.Select(static i => (Guid?)i));
    }
}

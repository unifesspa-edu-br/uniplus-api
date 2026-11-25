namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

public sealed class ImpedimentoPorInscricaoCheckTests
{
    [Fact(DisplayName = "Nega por impedido_por_inscricao_no_processo quem tem a concessão e está inscrito no processo do resource")]
    public async Task Decide_InscritoNoProcesso_NegaPorImpedimento()
    {
        Guid processo = Guid.CreateVersion7();
        InscricoesEmMemoria inscricoes = new(processo);

        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: ConcessaoInstitucional()), RequisitoPessoal(), Resource(processoId: processo), inscricoes: inscricoes);

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ImpedidoPorInscricaoNoProcesso);
        decisao.Resultado.Should().Be(ResultadoDoAcesso.Impedido);
    }

    [Fact(DisplayName = "O impedimento vale para a concessão de processo, que também não o supera")]
    public async Task Decide_InscritoComConcessaoDoProprioProcesso_NegaPorImpedimento()
    {
        Guid processo = Guid.CreateVersion7();

        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: ConcessaoDoProcesso(processo)),
            Requisito(),
            Resource(processoId: processo),
            inscricoes: new InscricoesEmMemoria(processo));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ImpedidoPorInscricaoNoProcesso);
    }

    [Fact(DisplayName = "Quem não tem concessão segue recusado por sem_concessao_aplicavel, ainda que esteja inscrito")]
    public async Task Decide_InscritoSemConcessao_MantemMotivoDaConcessao()
    {
        Guid processo = Guid.CreateVersion7();

        AuthorizationDecision decisao = await Decidir(
            Sujeito(), Requisito(), Resource(processoId: processo), inscricoes: new InscricoesEmMemoria(processo));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Inscrição em outro processo não impede a operação")]
    public async Task Decide_InscritoEmOutroProcesso_Permite()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: ConcessaoInstitucional()),
            Requisito(),
            Resource(processoId: Guid.CreateVersion7()),
            inscricoes: new InscricoesEmMemoria(Guid.CreateVersion7()));

        decisao.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Resource sem processo não passa pela verificação de impedimento")]
    public async Task Decide_ResourceSemProcesso_NaoConsultaInscricoes()
    {
        InscricoesEmMemoria inscricoes = new(Guid.CreateVersion7());

        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: ConcessaoInstitucional()), Requisito(), Resource(), inscricoes: inscricoes);

        decisao.Allowed.Should().BeTrue();
        inscricoes.Consultas.Should().Be(0);
    }
}

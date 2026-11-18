namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ModeloDeFase;

public sealed class JanelaDeIsencaoTests
{
    private static readonly TimeZoneInfo Belem = TimeZoneInfo.FindSystemTimeZoneById("America/Belem");

    private static ModeloDeFase Inscricao() => new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 10);

    private static ModeloDeFase Isencao(int ordem = 2) =>
        new ModeloDeFase("ISENCAO", ordem) { ColetaSolicitacaoIsencao = true }.Janela(0, 7);

    private static (string? Campo, string Codigo)[] Completude(TimeZoneInfo? fuso, params FaseDeclarada[] fases) =>
        [.. Cronograma(fases).ConferirCompletude(fuso).Select(static r => (r.Field, r.Error.Code))];

    private static bool ItemConforme(TimeZoneInfo? fuso, params FaseDeclarada[] fases) =>
        Cronograma(fases).ItensDaCompletude(fuso).Single(static i => i.Codigo == "cronograma_janela_de_isencao").Ok;

    [Fact(DisplayName = "Janela que abre com a inscrição, fecha antes dela e dura cinco dias é aceita")]
    public void Conferir_JanelaConforme_NaoRecusa()
    {
        Completude(Belem, Inscricao().Declarar(), Isencao().Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Isenção sem início ou sem fim é recusada no extremo que falta e não segue às outras regras")]
    public void Conferir_SemPrazo_RecusaNoExtremoAusente()
    {
        Completude(Belem, Inscricao().Declarar(), (Isencao() with { Fim = null }).Declarar())
            .Should().Equal(("fases[1].fim", "ProcessoSeletivo.JanelaDeIsencaoSemPrazo"));
        Completude(Belem, Inscricao().Declarar(), (Isencao() with { Inicio = null }).Declarar())
            .Should().Equal(("fases[1].inicio", "ProcessoSeletivo.JanelaDeIsencaoSemPrazo"));
    }

    [Fact(DisplayName = "As três falhas da isenção saem juntas, na ordem: duração, abertura e término")]
    public void Conferir_AsTresFalhas_SaemEmOrdem()
    {
        FaseDeclarada inscricaoCurta = new ModeloDeFase("INSCRICAO", 1) { ColetaInscricao = true }.Janela(0, 3).Declarar();
        FaseDeclarada isencao = new ModeloDeFase("ISENCAO", 2) { ColetaSolicitacaoIsencao = true }.Janela(1, 3).Declarar();

        Completude(Belem, inscricaoCurta, isencao).Should().Equal(
            ("fases[1].fim", "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias"),
            ("fases[1].inicio", "ProcessoSeletivo.JanelaDeIsencaoNaoAbreComAInscricao"),
            ("fases[1].fim", "ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao"));
    }

    [Fact(DisplayName = "Término no mesmo instante do fim das inscrições é recusado")]
    public void Conferir_TerminoNoFimDaInscricao_Recusa()
    {
        Completude(Belem, Inscricao().Declarar(), (Isencao() with { Fim = Abertura.AddDays(10) }).Declarar())
            .Should().Equal(("fases[1].fim", "ProcessoSeletivo.JanelaDeIsencaoNaoTerminaAntesDaInscricao"));
    }

    [Fact(DisplayName = "Duas fases coletam isenção e só a segunda é inválida: a segunda é recusada")]
    public void Conferir_DuasFasesDeIsencao_ConfereAsDuas()
    {
        FaseDeclarada primeira = Isencao(2).Declarar();
        FaseDeclarada segundaCurta = (new ModeloDeFase("REABERTURA", 3) { ColetaSolicitacaoIsencao = true }.Janela(0, 2)).Declarar();

        Completude(Belem, Inscricao().Declarar(), primeira, segundaCurta).Should().Equal(
            ("fases[2].fim", "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias"));
        ItemConforme(Belem, Inscricao().Declarar(), primeira, segundaCurta).Should().BeFalse();
    }

    [Fact(DisplayName = "Cinco dias completos contam até o último segundo do quinto dia no fuso institucional")]
    public void Conferir_QuintoDiaNoFuso_BordaDoUltimoSegundo()
    {
        DateTimeOffset abertura = new(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(-3));
        DateTimeOffset ultimoSegundo = new(2026, 3, 7, 23, 59, 59, TimeSpan.FromHours(-3));
        ModeloDeFase noLimite = new ModeloDeFase("ISENCAO", 1) { ColetaSolicitacaoIsencao = true, Inicio = abertura, Fim = ultimoSegundo };

        Completude(Belem, noLimite.Declarar()).Should().BeEmpty();
        Completude(Belem, (noLimite with { Fim = ultimoSegundo.AddSeconds(-1) }).Declarar())
            .Should().Equal(("fases[0].fim", "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias"));
    }

    [Fact(DisplayName = "Fuso diferente muda o quinto dia: a mesma janela passa em um fuso e falha em outro")]
    public void Conferir_FusoDiferente_MudaOQuintoDia()
    {
        TimeZoneInfo adiantado = TimeZoneInfo.CreateCustomTimeZone("adiantado", TimeSpan.FromHours(10), "adiantado", "adiantado");
        DateTimeOffset abertura = new(2026, 3, 2, 23, 0, 0, TimeSpan.Zero);
        ModeloDeFase modelo = new ModeloDeFase("ISENCAO", 1) { ColetaSolicitacaoIsencao = true, Inicio = abertura, Fim = abertura.AddDays(5).AddHours(4) };

        Completude(Belem, modelo.Declarar()).Should().BeEmpty();
        Completude(adiantado, modelo.Declarar()).Should().ContainSingle();
    }

    [Fact(DisplayName = "Mudança de horário entre a abertura e o quinto dia usa o deslocamento do quinto dia")]
    public void Conferir_MudancaDeHorarioNoMeio_UsaODeslocamentoDoQuintoDia()
    {
        TimeZoneInfo.TransitionTime inicioDoVerao = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 8);
        TimeZoneInfo.TransitionTime fimDoVerao = TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1);
        TimeZoneInfo.AdjustmentRule regra = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), inicioDoVerao, fimDoVerao);
        TimeZoneInfo comVerao = TimeZoneInfo.CreateCustomTimeZone("com-verao", TimeSpan.FromHours(-3), "com-verao", "padrao", "verao", [regra]);
        DateTimeOffset ultimoSegundoNoVerao = new(2026, 3, 9, 23, 59, 59, TimeSpan.FromHours(-2));
        ModeloDeFase modelo = new("ISENCAO", 1) { ColetaSolicitacaoIsencao = true, Inicio = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero) };

        Completude(comVerao, (modelo with { Fim = ultimoSegundoNoVerao }).Declarar()).Should().BeEmpty();
        Completude(comVerao, (modelo with { Fim = ultimoSegundoNoVerao.AddSeconds(-1) }).Declarar()).Should().ContainSingle();
    }

    [Fact(DisplayName = "Sem fuso institucional a fase que coleta isenção com janela completa lança, e sem isenção não")]
    public void Conferir_SemFuso_LancaSoQuandoHaIsencaoParaConferir()
    {
        Action comIsencao = () => Cronograma(Inscricao().Declarar(), Isencao().Declarar()).ConferirCompletude(null);

        comIsencao.Should().Throw<InvalidOperationException>();
        Completude(null, Inscricao().Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Janela invertida corta a conferência da isenção, e o item não fica verde por exclusão")]
    public void Conferir_JanelaInvertida_CortaARegraEDeixaOItemVermelho()
    {
        FaseDeclarada invertida = (Isencao() with { Inicio = Abertura.AddDays(7), Fim = Abertura }).Declarar();

        Completude(Belem, Inscricao().Declarar(), invertida).Should().BeEmpty();
        ItemConforme(Belem, Inscricao().Declarar(), invertida).Should().BeFalse();
    }

    [Fact(DisplayName = "Inscrição de janela invertida corta o contorno da isenção, e o item fica vermelho")]
    public void Conferir_InscricaoInvertida_CortaOContorno()
    {
        FaseDeclarada inscricaoInvertida = (Inscricao() with { Inicio = Abertura.AddDays(10), Fim = Abertura }).Declarar();

        Completude(Belem, inscricaoInvertida, Isencao().Declarar()).Should().BeEmpty();
        ItemConforme(Belem, inscricaoInvertida, Isencao().Declarar()).Should().BeFalse();
    }

    [Fact(DisplayName = "Com fuso nulo o item da isenção fica verde e a recusa é de outro item")]
    public void Item_SemFuso_FicaVerde()
    {
        ItemConforme(null, Inscricao().Declarar(), Isencao().Declarar()).Should().BeTrue();
    }

    [Fact(DisplayName = "Sem fase de inscrição de janela completa a isenção só confere o prazo")]
    public void Conferir_SemInscricaoComJanela_ConfereSoOPrazo()
    {
        Completude(Belem, Isencao().Declarar()).Should().BeEmpty();
        Completude(Belem, new ModeloDeFase("ISENCAO", 1) { ColetaSolicitacaoIsencao = true }.Janela(0, 2).Declarar())
            .Should().Equal(("fases[0].fim", "ProcessoSeletivo.JanelaDeIsencaoMenorQueCincoDias"));
    }
}

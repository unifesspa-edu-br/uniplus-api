namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class JanelaDeMigracaoEncerramentoTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestor;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Abertura = CenarioDeMigracao.Abertura;

    [Fact(DisplayName = "A concessão retida sem decisão cai no prazo, revogada pelo sistema com a data do prazo")]
    public void ConferirEstado_PrazoVencido_RevogaARetida()
    {
        (JanelaDeMigracao janela, Concessao deDiogo, DomainError? recusa) = RetidaNoPrazoVencido();

        recusa!.Code.Should().Be(MigracaoErrorCodes.PrazoEncerrado);
        deDiogo.RevogadaEm.Should().Be(janela.Prazo);
        deDiogo.MotivoRevogacao.Should().Be("não migrado para a versão 2");
        deDiogo.RevogadaPelaJanelaId.Should().Be(janela.Id);
        deDiogo.RevogadaPorSub.Should().BeNull();
        janela.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
    }

    [Fact(DisplayName = "O prazo vencido encerra a janela pelo prazo, com a decisão do sistema na trilha")]
    public void ConferirEstado_PrazoVencido_EncerraPeloPrazo()
    {
        (JanelaDeMigracao janela, _, _) = RetidaNoPrazoVencido();

        janela.Encerrada.Should().BeTrue();
        janela.EncerradaEm.Should().Be(janela.Prazo);
        janela.Forma.Should().Be(FormaDoEncerramento.Prazo);
        janela.Decisoes[^1].Should().Match<DecisaoDaMigracao>(d => d.Ator == AtorDaMigracao.Sistema && d.Etapa == EtapaDaMigracao.Sistema && d.LoteDeOrigemId == janela.LoteDaGestao!.Id);
    }

    [Fact(DisplayName = "A janela sem lote materializa o prazo com a decisão do sistema sem lote de origem")]
    public void Materializar_SemLote_DecisaoSemLoteDeOrigem()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.ConferirEstado(cenario.Contexto(janela.Prazo));

        janela.Decisoes.Should().ContainSingle().Which.LoteDeOrigemId.Should().BeNull();
        deLeandro.RevogadaEm.Should().Be(janela.Prazo);
    }

    [Fact(DisplayName = "A rotina atrasada avalia no corte: vencida depois do prazo é não migrada, antes do prazo é fora da lista")]
    public void Materializar_AvaliaNoCorteDoPrazo()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao venceDepois = cenario.Conceder(Leandro, validaAte: Abertura.AddHours(73));
        Concessao venceuAntes = cenario.Conceder(Isabella, validaAte: Abertura.AddHours(10));
        JanelaDeMigracao janela = LoteDoGestor(cenario, venceDepois, venceuAntes);

        janela.Materializar(cenario.Contexto(Abertura.AddHours(100)), FormaDoEncerramento.Prazo);

        janela.LinhaDe(venceDepois.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
        venceDepois.RevogadaEm.Should().Be(janela.Prazo);
        janela.LinhaDe(venceuAntes.Id)!.Situacao.Should().Be(SituacaoDaMigracao.ForaDaLista);
        venceuAntes.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "A rotina atrasada não revoga de novo a concessão revogada por uma pessoa depois do prazo")]
    public void Materializar_RevogadaDepoisDoPrazo_FicaComQuemRevogou()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao revogadaDepois = cenario.Conceder(Diogo);
        JanelaDeMigracao janela = LoteDoGestor(cenario, revogadaDepois);
        revogadaDepois.Revogar(Betina, "Revogação manual.", Abertura.AddHours(75)).IsSuccess.Should().BeTrue();

        janela.Materializar(cenario.Contexto(Abertura.AddHours(100)), FormaDoEncerramento.Prazo);

        janela.LinhaDe(revogadaDepois.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
        revogadaDepois.RevogadaPorSub.Should().Be(Betina.Sub);
        revogadaDepois.RevogadaPelaJanelaId.Should().BeNull();
    }

    [Fact(DisplayName = "A janela encerrada não muda de novo")]
    public void Materializar_JaEncerrada_NaoFazNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.ConferirEstado(cenario.Contexto(janela.Prazo));
        int decisoes = janela.Decisoes.Count;

        janela.Materializar(cenario.Contexto(janela.Prazo.AddHours(5)), FormaDoEncerramento.Manual);

        janela.Decisoes.Should().HaveCount(decisoes);
        janela.Forma.Should().Be(FormaDoEncerramento.Prazo);
        deLeandro.RevogadaEm.Should().Be(janela.Prazo);
    }

    [Fact(DisplayName = "A concessão que saiu da lista leva a linha aberta a fora da lista, sem decisão de ninguém")]
    public void Reconciliar_ConcessaoRevogada_ForaDaLista()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deIsabella = cenario.Conceder(Isabella);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar), (deIsabella, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Abertura.AddHours(1)));
        deLeandro.Revogar(Betina, "Revogação manual.", Abertura.AddHours(2)).IsSuccess.Should().BeTrue();

        DomainError? recusa = janela.ConferirEstado(cenario.Contexto(Abertura.AddHours(3)));

        recusa.Should().BeNull();
        janela.LinhaDe(deLeandro.Id)!.Situacao.Should().Be(SituacaoDaMigracao.ForaDaLista);
        janela.LinhaDe(deIsabella.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        janela.Decisoes[^1].Should().Match<DecisaoDaMigracao>(d => d.Resultado == ResultadoDaMigracao.ForaDaLista && d.Ator == AtorDaMigracao.Sistema);
    }

    [Fact(DisplayName = "A janela cuja última pendência saiu se encerra como concluída, inclusive sem lote")]
    public void ConferirEstado_SemPendencia_Conclui()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        deLeandro.Revogar(Betina, "Revogação manual.", Abertura.AddHours(2)).IsSuccess.Should().BeTrue();

        DomainError? recusa = janela.ConferirEstado(cenario.Contexto(Abertura.AddHours(3)));

        recusa!.Code.Should().Be(MigracaoErrorCodes.JanelaEncerrada);
        janela.Forma.Should().Be(FormaDoEncerramento.Concluida);
        janela.EncerradaEm.Should().Be(Abertura.AddHours(3));
    }

    [Fact(DisplayName = "O encerramento manual exige motivo e confirmação")]
    public void EncerrarManualmente_SemMotivoOuConfirmacao_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        ContextoDaMigracao contexto = cenario.Contexto(Abertura.AddHours(5));
        AutoriaDaVersao quem = CenarioDeMigracao.Autoria(Betina);

        Codigos(janela.EncerrarManualmente(new EncerramentoManual(quem, "  "), true, contexto)).Should().Equal(MigracaoErrorCodes.MotivoInvalido);
        Codigos(janela.EncerrarManualmente(new EncerramentoManual(quem, new string('x', 501)), true, contexto)).Should().Equal(MigracaoErrorCodes.MotivoInvalido);
        Codigos(janela.EncerrarManualmente(new EncerramentoManual(quem, "Versão antiga descontinuada."), false, contexto)).Should().Equal(MigracaoErrorCodes.ExclusaoNaoConfirmada);
        janela.Encerrada.Should().BeFalse();
    }

    [Fact(DisplayName = "O encerramento manual revoga quem não migrou e grava quem encerrou")]
    public void EncerrarManualmente_RevogaEGravaQuemEncerrou()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        DateTimeOffset agora = Abertura.AddHours(5);

        janela.EncerrarManualmente(new EncerramentoManual(CenarioDeMigracao.Autoria(Betina), " Versão antiga descontinuada. "), true, cenario.Contexto(agora))
            .IsSuccess.Should().BeTrue();

        janela.Forma.Should().Be(FormaDoEncerramento.Manual);
        janela.MotivoDoEncerramento.Should().Be("Versão antiga descontinuada.");
        janela.EncerradaPorSub.Should().Be(Betina.Sub);
        janela.EncerradaEm.Should().Be(agora);
        deLeandro.RevogadaEm.Should().Be(agora);
        deLeandro.MotivoRevogacao.Should().Be("não migrado para a versão 2");
    }

    [Fact(DisplayName = "Os avisos de 48 e de 24 horas se marcam uma vez, e o de 24 horas dispensa o de 48 que ficou para trás")]
    public void RegistrarAvisos_MarcaUmaVez()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();

        janela.RegistrarAvisoDe24Horas(Abertura.AddHours(50));
        janela.RegistrarAvisoDe24Horas(Abertura.AddHours(60));
        janela.RegistrarAvisoDe48Horas(Abertura.AddHours(61));

        janela.Aviso24hEm.Should().Be(Abertura.AddHours(50));
        janela.Aviso48hEm.Should().Be(Abertura.AddHours(50));
    }

    [Fact(DisplayName = "A remoção na janela redireciona o alvo e as migrações seguintes vão para a versão reduzida")]
    public void RedirecionarAlvo_MigraParaAReduzida()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        Guid reduzida = Guid.NewGuid();

        janela.RedirecionarAlvo(reduzida);

        janela.VersaoAlvoId.Should().Be(reduzida);
        janela.VersaoNovaId.Should().Be(cenario.VersaoNova!.Id);
        janela.Elegivel(deLeandro, Abertura).Should().BeTrue();
    }

    [Fact(DisplayName = "A revogação pela migração não tem pessoa, guarda a janela e não revoga duas vezes")]
    public void RevogarPelaMigracao_RevogaPeloSistema()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao concessao = cenario.Conceder(Leandro);
        Guid janela = Guid.NewGuid();

        concessao.RevogarPelaMigracao(janela, "recusado na migração", Abertura);

        concessao.RevogadaPelaJanelaId.Should().Be(janela);
        concessao.RevogadaPorSub.Should().BeNull();
        concessao.EstavaAtivaEm(Abertura.AddMinutes(-1)).Should().BeTrue();
        concessao.EstavaAtivaEm(Abertura).Should().BeFalse();
        FluentActions.Invoking(() => concessao.RevogarPelaMigracao(janela, "de novo", Abertura))
            .Should().Throw<InvalidOperationException>();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    // Diogo envia o lote que migra Leandro, e a concessão do próprio Diogo fica retida até o prazo vencer.
    private static (JanelaDeMigracao Janela, Concessao DeDiogo, DomainError? Recusa) RetidaNoPrazoVencido()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(CenarioDeMigracao.Lote(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Abertura.AddHours(1)));

        return (janela, deDiogo, janela.ConferirEstado(cenario.Contexto(Abertura.AddHours(80))));
    }

    // Abre a janela e envia, pelo gestor, o lote que migra todas as concessões.
    private static JanelaDeMigracao LoteDoGestor(CenarioDeMigracao cenario, params Concessao[] concessoes)
    {
        JanelaDeMigracao janela = cenario.Abrir();
        janela.EnviarLote(
            CenarioDeMigracao.Lote(false, [.. concessoes.Select(static c => (c, ResultadoDaMigracao.Migrar))]),
            CenarioDeMigracao.Autoria(Betina),
            cenario.Contexto(Abertura.AddHours(1)));

        return janela;
    }
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RevisaoDeMigracaoEncerramentoTests
{
    private static readonly Identidade Diogo = PerfisDeTeste.Autor;
    private static readonly Identidade Betina = PerfisDeTeste.Gestor;
    private static readonly Identidade Leandro = CenarioDeMigracao.Leandro;
    private static readonly Identidade Isabella = CenarioDeMigracao.Isabella;
    private static readonly DateTimeOffset Abertura = CenarioDeMigracao.Abertura;

    [Fact(DisplayName = "A concessão retida sem decisão cai no prazo, revogada pelo sistema com a data do prazo")]
    public void ConferirEstado_PrazoVencido_RevogaARetida()
    {
        (RevisaoDasConcessoesDoPerfil revisao, Concessao deDiogo, DomainError? recusa) = RetidaNoPrazoVencido();

        recusa!.Code.Should().Be(RevisaoDasConcessoesErrorCodes.PrazoEncerrado);
        deDiogo.RevogadaEm.Should().Be(revisao.TerminaEm);
        deDiogo.MotivoRevogacao.Should().Be("não migrado para a versão 2");
        deDiogo.RevogadaPelaRevisaoId.Should().Be(revisao.Id);
        deDiogo.RevogadaPorContaId.Should().BeNull();
        revisao.LinhaDe(deDiogo.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
    }

    [Fact(DisplayName = "O prazo vencido encerra a revisão pelo prazo, com a decisão do sistema na trilha")]
    public void ConferirEstado_PrazoVencido_EncerraPeloPrazo()
    {
        (RevisaoDasConcessoesDoPerfil revisao, _, _) = RetidaNoPrazoVencido();

        revisao.Encerrada.Should().BeTrue();
        revisao.EncerradaEm.Should().Be(revisao.TerminaEm);
        revisao.Forma.Should().Be(FormaDoEncerramento.Prazo);
        revisao.Decisoes[^1].Should().Match<DecisaoDaMigracao>(d => d.Ator == AtorDaMigracao.Sistema && d.Etapa == EtapaDaMigracao.Sistema && d.EnvioDeOrigemId == revisao.EnvioDaGestao!.Id);
    }

    [Fact(DisplayName = "A revisão sem envio materializa o prazo com a decisão do sistema sem envio de origem")]
    public void Materializar_SemEnvio_DecisaoSemEnvioDeOrigem()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.ConferirEstado(cenario.Contexto(revisao.TerminaEm));

        revisao.Decisoes.Should().ContainSingle().Which.EnvioDeOrigemId.Should().BeNull();
        deLeandro.RevogadaEm.Should().Be(revisao.TerminaEm);
    }

    [Fact(DisplayName = "A rotina atrasada avalia no instante do encerramento: vencida depois do prazo é não migrada, antes do prazo é fora da lista")]
    public void Materializar_AvaliaNoFimDoPrazo()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao venceDepois = cenario.Conceder(Leandro, validaAte: Abertura.AddHours(73));
        Concessao venceuAntes = cenario.Conceder(Isabella, validaAte: Abertura.AddHours(10));
        RevisaoDasConcessoesDoPerfil revisao = EnvioDoGestor(cenario, venceDepois, venceuAntes);

        revisao.Materializar(cenario.Contexto(Abertura.AddHours(100)), FormaDoEncerramento.Prazo);

        revisao.LinhaDe(venceDepois.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
        venceDepois.RevogadaEm.Should().Be(revisao.TerminaEm);
        revisao.LinhaDe(venceuAntes.Id)!.Situacao.Should().Be(SituacaoDaMigracao.ForaDaLista);
        venceuAntes.RevogadaEm.Should().BeNull();
    }

    [Fact(DisplayName = "A rotina atrasada não revoga de novo a concessão revogada por uma pessoa depois do prazo")]
    public void Materializar_RevogadaDepoisDoPrazo_FicaComQuemRevogou()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao revogadaDepois = cenario.Conceder(Diogo);
        RevisaoDasConcessoesDoPerfil revisao = EnvioDoGestor(cenario, revogadaDepois);
        revogadaDepois.Revogar(new AtoDoAgente(Betina, Abertura.AddHours(75)), "Revogação manual.").IsSuccess.Should().BeTrue();

        revisao.Materializar(cenario.Contexto(Abertura.AddHours(100)), FormaDoEncerramento.Prazo);

        revisao.LinhaDe(revogadaDepois.Id)!.Situacao.Should().Be(SituacaoDaMigracao.NaoMigrada);
        revogadaDepois.RevogadaPorContaId.Should().Be(Betina.ContaId);
        revogadaDepois.RevogadaPelaRevisaoId.Should().BeNull();
    }

    [Fact(DisplayName = "A revisão encerrada não muda de novo")]
    public void Materializar_JaEncerrada_NaoFazNada()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.ConferirEstado(cenario.Contexto(revisao.TerminaEm));
        int decisoes = revisao.Decisoes.Count;

        revisao.Materializar(cenario.Contexto(revisao.TerminaEm.AddHours(5)), FormaDoEncerramento.Manual);

        revisao.Decisoes.Should().HaveCount(decisoes);
        revisao.Forma.Should().Be(FormaDoEncerramento.Prazo);
        deLeandro.RevogadaEm.Should().Be(revisao.TerminaEm);
    }

    [Fact(DisplayName = "A concessão que saiu da lista leva a linha aberta a fora da lista, sem decisão de ninguém")]
    public void Reconciliar_ConcessaoRevogada_ForaDaLista()
    {
        var cenario = new CenarioDeMigracao(protegido: true);
        Concessao deLeandro = cenario.Conceder(Leandro);
        Concessao deIsabella = cenario.Conceder(Isabella);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar), (deIsabella, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Abertura.AddHours(1)));
        deLeandro.Revogar(new AtoDoAgente(Betina, Abertura.AddHours(2)), "Revogação manual.").IsSuccess.Should().BeTrue();

        DomainError? recusa = revisao.ConferirEstado(cenario.Contexto(Abertura.AddHours(3)));

        recusa.Should().BeNull();
        revisao.LinhaDe(deLeandro.Id)!.Situacao.Should().Be(SituacaoDaMigracao.ForaDaLista);
        revisao.LinhaDe(deIsabella.Id)!.Situacao.Should().Be(SituacaoDaMigracao.AguardandoProtecaoDeDados);
        revisao.Decisoes[^1].Should().Match<DecisaoDaMigracao>(d => d.Resultado == ResultadoDaMigracao.ForaDaLista && d.Ator == AtorDaMigracao.Sistema);
    }

    [Fact(DisplayName = "A revisão cuja última pendência saiu se encerra como concluída, inclusive sem envio")]
    public void ConferirEstado_SemPendencia_Conclui()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        deLeandro.Revogar(new AtoDoAgente(Betina, Abertura.AddHours(2)), "Revogação manual.").IsSuccess.Should().BeTrue();

        DomainError? recusa = revisao.ConferirEstado(cenario.Contexto(Abertura.AddHours(3)));

        recusa!.Code.Should().Be(RevisaoDasConcessoesErrorCodes.RevisaoEncerrada);
        revisao.Forma.Should().Be(FormaDoEncerramento.Concluida);
        revisao.EncerradaEm.Should().Be(Abertura.AddHours(3));
    }

    [Fact(DisplayName = "O encerramento manual exige motivo e confirmação")]
    public void EncerrarManualmente_SemMotivoOuConfirmacao_Recusa()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        ContextoDaRevisao contexto = cenario.Contexto(Abertura.AddHours(5));
        AutoriaDaVersao quem = CenarioDeMigracao.Autoria(Betina);

        Codigos(revisao.EncerrarManualmente(new EncerramentoManual(quem, "  "), true, contexto)).Should().Equal(RevisaoDasConcessoesErrorCodes.MotivoInvalido);
        Codigos(revisao.EncerrarManualmente(new EncerramentoManual(quem, new string('x', 501)), true, contexto)).Should().Equal(RevisaoDasConcessoesErrorCodes.MotivoInvalido);
        Codigos(revisao.EncerrarManualmente(new EncerramentoManual(quem, "Versão antiga descontinuada."), false, contexto)).Should().Equal(RevisaoDasConcessoesErrorCodes.ExclusaoNaoConfirmada);
        revisao.Encerrada.Should().BeFalse();
    }

    [Theory(DisplayName = "O encerramento manual confere o limite em runas")]
    [InlineData(500, "a", true)]
    [InlineData(501, "a", false)]
    [InlineData(500, "😀", true)]
    [InlineData(501, "😀", false)]
    public void EncerrarManualmente_LimiteDoMotivo_Valida(int tamanho, string caractere, bool aceita)
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        string motivo = string.Concat(Enumerable.Repeat(caractere, tamanho));

        Result resultado = revisao.EncerrarManualmente(new EncerramentoManual(CenarioDeMigracao.Autoria(Betina), motivo), true, cenario.Contexto(Abertura.AddHours(5)));

        resultado.IsSuccess.Should().Be(aceita);
        revisao.Encerrada.Should().Be(aceita);
    }

    [Fact(DisplayName = "O encerramento manual revoga quem não migrou e grava quem encerrou")]
    public void EncerrarManualmente_RevogaEGravaQuemEncerrou()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        DateTimeOffset agora = Abertura.AddHours(5);

        revisao.EncerrarManualmente(new EncerramentoManual(CenarioDeMigracao.Autoria(Betina), " Versão antiga descontinuada. "), true, cenario.Contexto(agora))
            .IsSuccess.Should().BeTrue();

        revisao.Forma.Should().Be(FormaDoEncerramento.Manual);
        revisao.MotivoDoEncerramento.Should().Be("Versão antiga descontinuada.");
        revisao.EncerradaPorContaId.Should().Be(Betina.ContaId);
        revisao.EncerradaEm.Should().Be(agora);
        deLeandro.RevogadaEm.Should().Be(agora);
        deLeandro.MotivoRevogacao.Should().Be("não migrado para a versão 2");
    }

    [Fact(DisplayName = "Os avisos de 48 e de 24 horas se marcam uma vez, e o de 24 horas dispensa o de 48 que ficou para trás")]
    public void RegistrarAvisos_MarcaUmaVez()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();

        revisao.RegistrarAvisoDe24Horas(Abertura.AddHours(50));
        revisao.RegistrarAvisoDe24Horas(Abertura.AddHours(60));
        revisao.RegistrarAvisoDe48Horas(Abertura.AddHours(61));

        revisao.Aviso24hEm.Should().Be(Abertura.AddHours(50));
        revisao.Aviso48hEm.Should().Be(Abertura.AddHours(50));
    }

    [Fact(DisplayName = "A remoção na revisão redireciona o alvo e as migrações seguintes vão para a versão reduzida")]
    public void RedirecionarAlvo_MigraParaAReduzida()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        Guid reduzida = Guid.NewGuid();

        revisao.RedirecionarAlvo(reduzida);

        revisao.VersaoAlvoId.Should().Be(reduzida);
        revisao.VersaoNovaId.Should().Be(cenario.VersaoNova!.Id);
        revisao.Elegivel(deLeandro, Abertura).Should().BeTrue();
    }

    [Fact(DisplayName = "A revogação pela migração não tem pessoa, guarda a revisão e não revoga duas vezes")]
    public void RevogarPelaMigracao_RevogaPeloSistema()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao concessao = cenario.Conceder(Leandro);
        Guid revisao = Guid.NewGuid();

        concessao.RevogarPelaMigracao(revisao, "recusado na migração", Abertura);

        concessao.RevogadaPelaRevisaoId.Should().Be(revisao);
        concessao.RevogadaPorContaId.Should().BeNull();
        concessao.EstavaAtivaEm(Abertura.AddMinutes(-1)).Should().BeTrue();
        concessao.EstavaAtivaEm(Abertura).Should().BeFalse();
        FluentActions.Invoking(() => concessao.RevogarPelaMigracao(revisao, "de novo", Abertura))
            .Should().Throw<InvalidOperationException>();
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    // Diogo envia o envio que migra Leandro, e a concessão do próprio Diogo fica retida até o prazo vencer.
    private static (RevisaoDasConcessoesDoPerfil Revisao, Concessao DeDiogo, DomainError? Recusa) RetidaNoPrazoVencido()
    {
        var cenario = new CenarioDeMigracao(protegido: false);
        Concessao deDiogo = cenario.Conceder(Diogo);
        Concessao deLeandro = cenario.Conceder(Leandro);
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(CenarioDeMigracao.Envio(false, (deLeandro, ResultadoDaMigracao.Migrar)), CenarioDeMigracao.Autoria(Diogo), cenario.Contexto(Abertura.AddHours(1)));

        return (revisao, deDiogo, revisao.ConferirEstado(cenario.Contexto(Abertura.AddHours(80))));
    }

    // Abre a revisão e envia, pelo gestor, o envio que migra todas as concessões.
    private static RevisaoDasConcessoesDoPerfil EnvioDoGestor(CenarioDeMigracao cenario, params Concessao[] concessoes)
    {
        RevisaoDasConcessoesDoPerfil revisao = cenario.Abrir();
        revisao.EnviarDecisoesDaGestao(
            CenarioDeMigracao.Envio(false, [.. concessoes.Select(static c => (c, ResultadoDaMigracao.Migrar))]),
            CenarioDeMigracao.Autoria(Betina),
            cenario.Contexto(Abertura.AddHours(1)));

        return revisao;
    }
}

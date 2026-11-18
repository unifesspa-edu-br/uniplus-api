namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoCancelamentoTests
{
    [Fact(DisplayName = "Cancelar a inscrição enviada grava data, ator e motivo, mantém o número e emite InscricaoCanceladaEvent")]
    public void Cancelar_Enviada_GravaDataAtorEMotivoEMantemONumero()
    {
        Inscricao inscricao = NovaInscricaoEnviada();
        int revisao = inscricao.Revisao;
        DateTimeOffset instante = Instante();

        Result resultado = inscricao.Cancelar("Mudei de curso", "conta-1", instante, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Cancelada);
        inscricao.Estado.Cancelamento?.CanceladaEm.Should().Be(instante);
        inscricao.Estado.Cancelamento?.CanceladaPor.Should().Be("conta-1");
        inscricao.Estado.Cancelamento?.MotivoDoCancelamento.Should().Be("Mudei de curso");
        inscricao.Estado.NumeroInscricao.Should().Be("000123");
        inscricao.Revisao.Should().Be(revisao + 1);
        inscricao.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InscricaoCanceladaEvent>();
    }

    [Fact(DisplayName = "A inscrição cancelada não emite comprovante, e a enviada ou o rascunho não têm o que conferir")]
    public void ConferirComprovanteEmitivel_SoRecusaACancelada()
    {
        Inscricao enviada = NovaInscricaoEnviada();
        enviada.ConferirComprovanteEmitivel().Should().BeNull();

        enviada.Cancelar("Mudei de curso", "conta-1", Instante(), PrecondicaoIfMatch.DeTags([enviada.ETag])).IsSuccess.Should().BeTrue();

        enviada.ConferirComprovanteEmitivel()!.Code.Should().Be(InscricaoErrorCodes.ComprovanteDeInscricaoCancelada);
    }

    [Theory(DisplayName = "Cancelar sem motivo grava \"a pedido do candidato\"")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Cancelar_SemMotivo_GravaOMotivoPadrao(string? motivo)
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        inscricao.Cancelar(motivo, "conta-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();

        inscricao.Estado.Cancelamento?.MotivoDoCancelamento.Should().Be("a pedido do candidato");
    }

    [Fact(DisplayName = "Descartar a inscrição enviada é recusado orientando a cancelar")]
    public void ConferirDescarte_Enviada_OrientaACancelar()
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        DomainError recusa = inscricao.ConferirDescarte(PrecondicaoIfMatch.DeTags([inscricao.ETag]))!;

        recusa.Code.Should().Be(InscricaoErrorCodes.DescarteSoDeRascunho);
        recusa.Message.Should().Contain("cancele a inscrição").And.NotContain("/");
    }

    [Fact(DisplayName = "O rascunho não se cancela: a recusa orienta a descartar e nada muda")]
    public void Cancelar_Rascunho_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.Cancelar(null, "conta-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.CancelamentoSoDeEnviada);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Rascunho);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Cancelar com If-Match vencido ou com motivo acima do limite é recusado, sem mutar")]
    public void Cancelar_PrecondicaoOuMotivoInvalidos_RecusaSemMutar()
    {
        Inscricao inscricao = NovaInscricaoEnviada();

        inscricao.Cancelar(null, "conta-1", Instante(), PrecondicaoIfMatch.DeTags(["\"outro\""])).Error!.Code
            .Should().Be(InscricaoErrorCodes.PrecondicaoFalhou);
        inscricao.Cancelar(new string('x', CancelamentoDaInscricao.TamanhoMaximoDoMotivoDoCancelamento + 1), "conta-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).Error!.Code
            .Should().Be(InscricaoErrorCodes.MotivoDoCancelamentoLongo);

        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Submetida);
    }

    [Fact(DisplayName = "A inscrição cancelada recusa salvar, enviar, cancelar de novo, descartar e adotar versão")]
    public void Cancelada_NaoSeAltera()
    {
        Inscricao inscricao = NovaInscricaoEnviada();
        inscricao.Cancelar(null, "conta-1", Instante(), PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        PrecondicaoIfMatch corrente = PrecondicaoIfMatch.DeTags([inscricao.ETag]);
        int revisao = inscricao.Revisao;

        IReadOnlyList<DomainError?> recusas = RecusasDaCancelada(inscricao, corrente);

        recusas.Should().OnlyContain(e => e != null && e.Code == InscricaoErrorCodes.InscricaoCancelada);
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "Com o período encerrado nenhuma ação vale; aberto, corrigir e cancelar pedem a enviada e continuar e descartar, o rascunho")]
    public void AcoesPermitidas_DependemDaSituacaoEDoPeriodo()
    {
        Inscricao rascunho = NovaInscricao();
        Inscricao enviada = NovaInscricaoEnviada();
        Inscricao cancelada = NovaInscricaoEnviada();
        cancelada.Cancelar(null, "conta-1", Instante(), PrecondicaoIfMatch.DeTags([cancelada.ETag]));

        enviada.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(false, true, true, false));
        enviada.AcoesPermitidas(inscricoesAbertas: false).Should().Be(new AcoesDaInscricao(false, false, false, false));
        rascunho.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(true, false, false, true));
        rascunho.AcoesPermitidas(inscricoesAbertas: false).Should().Be(new AcoesDaInscricao(false, false, false, false));
        cancelada.AcoesPermitidas(inscricoesAbertas: true).Should().Be(new AcoesDaInscricao(false, false, false, false));
    }

    private static IReadOnlyList<DomainError?> RecusasDaCancelada(Inscricao inscricao, PrecondicaoIfMatch corrente) =>

        [
            inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(OfertaDoProcesso, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, corrente).Error,
            inscricao.Submeter("999999",
            Recebido(Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), corrente, Instante()).Error,
            inscricao.Cancelar(null, "conta-1", Instante(), corrente).Error,
            inscricao.ConferirDescarte(corrente),
            inscricao.AdotarVersao(Guid.CreateVersion7(), Admitidas).Error,
        ];

}

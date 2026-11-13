namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A situação derivada na leitura, o encerramento da concessão e o impedimento de responder (UNI-REQ-0376).</summary>
public sealed class ProvidenciaSituacaoTests
{
    private static readonly DateTimeOffset Agora = ProvidenciaBuilder.Agora;

    [Fact(DisplayName = "A aberta com concessão ativa aparece como aberta")]
    public void Situacao_AbertaComConcessaoAtiva()
    {
        ProvidenciaBuilder cenario = new();

        cenario.Aberta().SituacaoEm(cenario.Concessao, Agora).Should().Be(SituacaoDaProvidencia.Aberta);
    }

    [Fact(DisplayName = "A aberta cuja concessão foi revogada por outro caminho aparece como prejudicada, sem mudar o estado gravado")]
    public void Situacao_ConcessaoRevogada_Prejudicada()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();
        cenario.Concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(1)), "Revogada pela revogação comum.");

        (p.SituacaoEm(cenario.Concessao, Agora.AddMinutes(2)), p.Estado).Should().Be((SituacaoDaProvidencia.Prejudicada, EstadoDaProvidencia.Aberta));
    }

    [Fact(DisplayName = "A aberta fica prejudicada no instante em que a concessão vence, sem nada gravado")]
    public void Situacao_ConcessaoVencida_Prejudicada()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));
        Providencia p = cenario.Aberta();

        p.SituacaoEm(cenario.Concessao, Agora.AddMinutes(59)).Should().Be(SituacaoDaProvidencia.Aberta);
        (p.SituacaoEm(cenario.Concessao, Agora.AddHours(1)), p.Estado).Should().Be((SituacaoDaProvidencia.Prejudicada, EstadoDaProvidencia.Aberta));
    }

    [Fact(DisplayName = "A respondida continua respondida depois que a concessão deixa de valer")]
    public void Situacao_RespondidaNaoFicaPrejudicada()
    {
        ProvidenciaBuilder cenario = new ProvidenciaBuilder().SobreA(ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1)));

        cenario.Respondida().SituacaoEm(cenario.Concessao, Agora.AddHours(2)).Should().Be(SituacaoDaProvidencia.Respondida);
    }

    [Fact(DisplayName = "A atendida aparece como atendida, embora a concessão esteja revogada")]
    public void Situacao_Atendida()
    {
        ProvidenciaBuilder cenario = new();

        cenario.Atendida().SituacaoEm(cenario.Concessao, Agora.AddMinutes(1)).Should().Be(SituacaoDaProvidencia.Atendida);
    }

    [Fact(DisplayName = "O encerramento é a revogação, mesmo depois do fim da vigência, o vencimento quando não revogada, e nada enquanto ativa")]
    public void Encerramento_DaConcessao()
    {
        Concessao ativa = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        Concessao revogada = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        revogada.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(10)), "Mudou de função.");

        EncerramentoDaConcessao.De(ativa, Agora).Should().BeNull();
        EncerramentoDaConcessao.De(ativa, Agora.AddHours(2)).Should().Be(new EncerramentoDaConcessao(CausaDoEncerramento.Vencida, Agora.AddHours(1)));
        EncerramentoDaConcessao.De(revogada, Agora.AddHours(2)).Should().Be(new EncerramentoDaConcessao(CausaDoEncerramento.Revogada, Agora.AddMinutes(10)));
    }

    [Fact(DisplayName = "A concessão vencida e revogada depois tem o vencimento como encerramento, no instante do fim da vigência")]
    public void Encerramento_VencidaAntesDaRevogacao_Vencimento()
    {
        Concessao concessao = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(3)), "Revogada depois de vencida.");

        EncerramentoDaConcessao.De(concessao, Agora.AddHours(4)).Should().Be(new EncerramentoDaConcessao(CausaDoEncerramento.Vencida, Agora.AddHours(1)));
        EncerramentoDaConcessao.VencimentoDe(concessao, Agora.AddHours(4)).Should().Be(Agora.AddHours(1));
    }

    [Fact(DisplayName = "No empate entre o fim da vigência e a revogação, a concessão já não valia: o encerramento é o vencimento")]
    public void Encerramento_Empate_Vencimento()
    {
        Concessao concessao = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddHours(1)), "Revogada no fim da vigência.");

        EncerramentoDaConcessao.De(concessao, Agora.AddHours(2)).Should().Be(new EncerramentoDaConcessao(CausaDoEncerramento.Vencida, Agora.AddHours(1)));
    }

    [Fact(DisplayName = "A concessão revogada antes do fim da vigência tem a revogação como encerramento e não tem vencimento")]
    public void Encerramento_RevogadaAntesDoFim_Revogacao()
    {
        Concessao concessao = ProvidenciaBuilder.ConcessaoDeLeandro(validaAte: Agora.AddHours(1));
        concessao.Revogar(ProvidenciaBuilder.AtoDeDiogo(Agora.AddMinutes(59)), "Revogada um minuto antes do fim.");

        EncerramentoDaConcessao.De(concessao, Agora.AddHours(2)).Should().Be(new EncerramentoDaConcessao(CausaDoEncerramento.Revogada, Agora.AddMinutes(59)));
        EncerramentoDaConcessao.VencimentoDe(concessao, Agora.AddHours(2)).Should().BeNull();
    }

    [Fact(DisplayName = "Estão impedidos de responder quem solicitou e o beneficiário; quem administra concessões, não")]
    public void ImpedeResposta_SolicitanteEBeneficiario()
    {
        ProvidenciaBuilder cenario = new();
        Providencia p = cenario.Aberta();

        p.ImpedeResposta(ProvidenciaBuilder.Cecilia.Identidade, cenario.Concessao).Should().BeTrue();
        p.ImpedeResposta(ProvidenciaBuilder.Leandro.Identidade, cenario.Concessao).Should().BeTrue();
        p.ImpedeResposta(ProvidenciaBuilder.Diogo, cenario.Concessao).Should().BeFalse();
    }

    [Theory(DisplayName = "Os códigos dos estados, das situações, dos tipos e das causas são os do contrato")]
    [InlineData(SituacaoDaProvidencia.Aberta, "aberta")]
    [InlineData(SituacaoDaProvidencia.Respondida, "respondida")]
    [InlineData(SituacaoDaProvidencia.Atendida, "atendida")]
    [InlineData(SituacaoDaProvidencia.Prejudicada, "prejudicada")]
    public void Codigos_DaSituacao(SituacaoDaProvidencia situacao, string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        (situacao.ToCodigo(), SituacaoDaProvidenciaCodigo.DoCodigo($" {codigo.ToUpperInvariant()} ")).Should().Be((codigo, (SituacaoDaProvidencia?)situacao));
    }

    [Fact(DisplayName = "Os códigos gravados voltam ao mesmo valor, e o desconhecido é recusado")]
    public void Codigos_GravadosEDesconhecidos()
    {
        Enum.GetValues<EstadoDaProvidencia>().Should().OnlyContain(e => EstadoDaProvidenciaCodigo.FromCodigo(e.ToCodigo()) == e);
        Enum.GetValues<TipoDaProvidencia>().Should().OnlyContain(t => TipoDaProvidenciaCodigo.FromCodigo(t.ToCodigo()) == t);
        (CausaDoEncerramento.Revogada.ToCodigo(), CausaDoEncerramento.Vencida.ToCodigo()).Should().Be(("revogada", "vencida"));
        SituacaoDaProvidenciaCodigo.DoCodigo("todas").Should().BeNull();
        FluentActions.Invoking(() => TipoDaProvidenciaCodigo.FromCodigo("suspensao")).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => EstadoDaProvidenciaCodigo.FromCodigo("prejudicada")).Should().Throw<ArgumentOutOfRangeException>();
    }
}

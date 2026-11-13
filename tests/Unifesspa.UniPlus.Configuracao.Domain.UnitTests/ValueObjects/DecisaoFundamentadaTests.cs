namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>
/// A regra da justificativa diante de manifestação técnica desfavorável vigente (UNI-REQ-0374): aprovar ou recomendar
/// ajuste a exige; a reprovação já a exige sempre; sem manifestação desfavorável, ela é opcional.
/// </summary>
public sealed class DecisaoFundamentadaTests
{
    private static readonly Fundamentacao Justificativa = Fundamentacao.Analisar("A classificação pessoal basta: o laudo só comprova a matrícula.");

    [Theory(DisplayName = "Aprovar ou recomendar ajuste sem justificativa diante de manifestação desfavorável é recusado no campo justificativa")]
    [InlineData(AtoDaProtecao.Aprovacao)]
    [InlineData(AtoDaProtecao.AjusteRecomendado)]
    public void ConferirDiante_SemJustificativaDianteDeDesfavoravel_Recusa(AtoDaProtecao ato)
    {
        Result resultado = Decisao(ato, null).ConferirDiante(CircuitoComManifestacao(Inadequada()));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "justificativa" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao);
    }

    [Theory(DisplayName = "Com justificativa, aprovar ou recomendar ajuste diante de manifestação desfavorável é aceito")]
    [InlineData(AtoDaProtecao.Aprovacao)]
    [InlineData(AtoDaProtecao.AjusteRecomendado)]
    public void ConferirDiante_ComJustificativa_Aceita(AtoDaProtecao ato) =>
        Decisao(ato, Justificativa).ConferirDiante(CircuitoComManifestacao(RecomendaAjuste())).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Sem manifestação, ou só com manifestação adequada, a aprovação sem justificativa é aceita")]
    public void ConferirDiante_SemDesfavoravel_Aceita()
    {
        DecisaoFundamentada aprovacao = Decisao(AtoDaProtecao.Aprovacao, null);

        aprovacao.ConferirDiante(new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build().Aprovacao).IsSuccess.Should().BeTrue();
        aprovacao.ConferirDiante(CircuitoComManifestacao(Adequada())).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A reprovação não passa por esta regra: a justificativa dela é obrigatória sempre")]
    public void ConferirDiante_Reprovacao_NaoConfere() =>
        Decisao(AtoDaProtecao.Reprovacao, null).ConferirDiante(CircuitoComManifestacao(Inadequada())).IsSuccess.Should().BeTrue();

    [Fact(DisplayName = "Fora de solicitação a regra não confere: a transição do circuito recusa a decisão")]
    public void ConferirDiante_ForaDeSolicitacao_NaoConfere() =>
        Decisao(AtoDaProtecao.Aprovacao, null).ConferirDiante(new TipoDocumentoNaAprovacaoBuilder().Build().Aprovacao).IsSuccess.Should().BeTrue();

    private static DecisaoFundamentada Decisao(AtoDaProtecao ato, Fundamentacao? justificativa) =>
        new(ato, ato == AtoDaProtecao.AjusteRecomendado ? ClassificacaoDeProtecao.Sensivel : null, justificativa);

    private static AprovacaoDaProtecao CircuitoComManifestacao(ConteudoDaManifestacao conteudo)
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        Do(tipo).Registrar(tipo.Aprovacao, tipo.Aprovacao.Ciclo, conteudo, Kevin, TipoDocumentoNaAprovacaoBuilder.Segunda.AddHours(2));
        return tipo.Aprovacao;
    }
}

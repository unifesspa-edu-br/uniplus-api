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
    public void Conferir_SemJustificativaDianteDeDesfavoravel_Recusa(AtoDaProtecao ato)
    {
        TipoDocumento tipo = ComManifestacao(Inadequada());

        Result resultado = Decisao(ato, null).Conferir(tipo.Aprovacao, tipo.Protecao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "justificativa" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao);
    }

    [Fact(DisplayName = "O ajuste sem justificativa diante de manifestação desfavorável e com a classificação atual sai com as duas recusas no lote")]
    public void Conferir_AjusteSemJustificativaEIgualAAtual_AcumulaAsDuasRecusas()
    {
        TipoDocumento tipo = ComManifestacao(Inadequada());
        DecisaoFundamentada ajuste = new(AtoDaProtecao.AjusteRecomendado, tipo.ClassificacaoProtecao, null);

        Result resultado = ajuste.Conferir(tipo.Aprovacao, tipo.Protecao);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("justificativa", ProtecaoDoCatalogoErrorCodes.JustificativaExigidaPelaManifestacao),
            ("classificacaoRecomendada", ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual),
        ]);
    }

    [Fact(DisplayName = "Sem manifestação, o ajuste com a classificação atual é recusado só pela recomendação")]
    public void Conferir_AjusteIgualAAtualSemManifestacao_RecusaARecomendacao()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();

        Result resultado = new DecisaoFundamentada(AtoDaProtecao.AjusteRecomendado, tipo.ClassificacaoProtecao, null).Conferir(tipo.Aprovacao, tipo.Protecao);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual);
    }

    [Theory(DisplayName = "Com justificativa, aprovar ou recomendar ajuste diante de manifestação desfavorável é aceito")]
    [InlineData(AtoDaProtecao.Aprovacao)]
    [InlineData(AtoDaProtecao.AjusteRecomendado)]
    public void Conferir_ComJustificativa_Aceita(AtoDaProtecao ato)
    {
        TipoDocumento tipo = ComManifestacao(RecomendaAjuste());

        Decisao(ato, Justificativa).Conferir(tipo.Aprovacao, tipo.Protecao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Sem manifestação, ou só com manifestação adequada, a aprovação sem justificativa é aceita")]
    public void Conferir_SemDesfavoravel_Aceita()
    {
        DecisaoFundamentada aprovacao = Decisao(AtoDaProtecao.Aprovacao, null);
        TipoDocumento semManifestacao = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        TipoDocumento adequada = ComManifestacao(Adequada());

        aprovacao.Conferir(semManifestacao.Aprovacao, semManifestacao.Protecao).IsSuccess.Should().BeTrue();
        aprovacao.Conferir(adequada.Aprovacao, adequada.Protecao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A reprovação não passa pela regra da justificativa diante da manifestação: a dela é obrigatória sempre")]
    public void Conferir_Reprovacao_NaoConfere()
    {
        TipoDocumento tipo = ComManifestacao(Inadequada());

        Decisao(AtoDaProtecao.Reprovacao, null).Conferir(tipo.Aprovacao, tipo.Protecao).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Fora de solicitação a regra da justificativa não confere: a transição do circuito recusa a decisão")]
    public void Conferir_ForaDeSolicitacao_NaoConfere()
    {
        TipoDocumento pendente = new TipoDocumentoNaAprovacaoBuilder().Build();

        Decisao(AtoDaProtecao.Aprovacao, null).Conferir(pendente.Aprovacao, pendente.Protecao).IsSuccess.Should().BeTrue();
    }

    private static DecisaoFundamentada Decisao(AtoDaProtecao ato, Fundamentacao? justificativa) =>
        new(ato, ato == AtoDaProtecao.AjusteRecomendado ? ClassificacaoDeProtecao.Sensivel : null, justificativa);

    private static TipoDocumento ComManifestacao(ConteudoDaManifestacao conteudo)
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        Do(tipo).Registrar(tipo.Aprovacao, tipo.Aprovacao.Ciclo, conteudo, Kevin, TipoDocumentoNaAprovacaoBuilder.Segunda.AddHours(2));
        return tipo;
    }
}

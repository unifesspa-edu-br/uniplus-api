namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>
/// O que o Representante do controlador leu do circuito antes de decidir: a manifestação registrada entre a leitura e o
/// envio torna a decisão recusada por conflito (UNI-REQ-0374).
/// </summary>
public sealed class LeituraDoCircuitoTests
{
    [Fact(DisplayName = "O ciclo e a contagem lidos formam a leitura, sem recusa")]
    public void Ler_LeituraValida()
    {
        List<FieldError> erros = [];

        LeituraDoCircuito.Ler(2, 0, erros).Should().Be(new LeituraDoCircuito(new CicloDaAprovacao(2), 0));
        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "O ciclo inválido e a contagem ausente saem juntos no lote, cada um no seu campo")]
    public void Ler_AcumulaAsRecusas()
    {
        List<FieldError> erros = [];

        LeituraDoCircuito.Ler(0, null, erros).Should().BeNull();
        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("ciclo", ProtecaoDoCatalogoErrorCodes.CicloInvalido),
            ("manifestacoesNoCiclo", ProtecaoDoCatalogoErrorCodes.ManifestacoesNoCicloInvalida));
    }

    [Fact(DisplayName = "A contagem negativa é recusada")]
    public void Ler_ContagemNegativa_Recusa()
    {
        List<FieldError> erros = [];

        LeituraDoCircuito.Ler(1, -1, erros).Should().BeNull();
        erros.Should().ContainSingle(e => e.Field == "manifestacoesNoCiclo");
    }

    [Fact(DisplayName = "A leitura que bate com o circuito é aceita; a manifestação registrada depois dela a recusa por conflito")]
    public void ConferirCom_ManifestacaoDepoisDaLeitura_Recusa()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();
        LeituraDoCircuito lida = new(tipo.Aprovacao.Ciclo, tipo.Aprovacao.ManifestacoesNoCiclo);
        lida.ConferirCom(tipo.Aprovacao).IsSuccess.Should().BeTrue();

        Do(tipo).Registrar(tipo.Aprovacao, tipo.Aprovacao.Ciclo, Adequada(), Kevin, TipoDocumentoNaAprovacaoBuilder.Segunda);

        lida.ConferirCom(tipo.Aprovacao).Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "manifestacoesNoCiclo" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida);
    }

    [Fact(DisplayName = "O ciclo lido diferente do corrente recusa a leitura por conflito")]
    public void ConferirCom_CicloDiferente_Recusa()
    {
        TipoDocumento tipo = new TipoDocumentoNaAprovacaoBuilder().Solicitado().Build();

        new LeituraDoCircuito(new CicloDaAprovacao(2), 0).ConferirCom(tipo.Aprovacao).Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "ciclo" && e.Error.Code == ProtecaoDoCatalogoErrorCodes.ManifestacaoNaoLida);
    }
}

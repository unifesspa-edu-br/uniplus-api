namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O prazo de emissão e a validade que o candidato vê em cada exigência do certame: a data de referência sai do cronograma
/// e do fuso congelados na versão, e o documento sem prazo ou sem validade exigida não traz o objeto.
/// </summary>
public sealed class PrazoEValidadeNoCertameTests
{
    private static readonly Guid OutraFase = Guid.Parse("00000000-0000-0000-0000-0000000004aa");

    private static readonly PeriodoDaFaseCongelada Inscricao = new(
        EnvelopeDoCertameBuilder.FaseDaInscricao, true, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));

    private static readonly PeriodoDaFaseCongelada Recurso = new(
        OutraFase, false, new DateTimeOffset(2026, 3, 1, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 5, 2, 0, 0, TimeSpan.Zero));

    private static ExigenciaDocumentalCertameDto Projetada(EnvelopeDoCertameBuilder envelope) =>
        ProjecaoDoCertameDeTeste.Projetado(envelope.Construir()).DocumentosExigidos.Single();

    private static ReferenciaDoDocumentoCertameDto Referencia(TipoDaDataDeReferencia tipo, DateOnly? data, Guid? faseId = null) =>
        new(CodigoComRotulo<TipoDaDataDeReferencia>.De(tipo), data, faseId);

    private static PrazoDeEmissao PrazoReferenciado(DataDeReferenciaDoDocumento referencia) =>
        new PrazoDeEmissaoBuilder().Referenciado(referencia).Construir();

    [Fact(DisplayName = "O prazo de emissão traz o valor, a unidade e o efeito, e o fim das inscrições no fuso do processo, não em UTC")]
    public void Projetar_PrazoNoFimDasInscricoes_ResolveADataNoFusoDaVersao()
    {
        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder());

        exigencia.PrazoDeEmissao.Should().Be(new PrazoDeEmissaoCertameDto(
            CodigoComRotulo<EscolhaDoPrazoDeEmissao>.De(EscolhaDoPrazoDeEmissao.ComPrazo),
            90,
            CodigoComRotulo<UnidadeDoPrazoDeEmissao>.De(UnidadeDoPrazoDeEmissao.Dias),
            Referencia(TipoDaDataDeReferencia.FimDasInscricoes, new DateOnly(2026, 1, 30)),
            CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(EfeitoNaAnaliseDoDocumento.RecusarDocumento)));
    }

    [Fact(DisplayName = "Com várias fases que coletam inscrição, o fim das inscrições é o maior término, e a fase que não coleta não conta")]
    public void Projetar_VariasFasesDeInscricao_UsaOMaiorTermino()
    {
        PeriodoDaFaseCongelada segunda = new(Guid.Parse("00000000-0000-0000-0000-0000000004bb"), true, null, new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero));
        EnvelopeDoCertameBuilder envelope = new EnvelopeDoCertameBuilder().ComFases(Inscricao, segunda, Recurso);

        Projetada(envelope).PrazoDeEmissao!.Referencia.Data.Should().Be(new DateOnly(2026, 2, 20));
    }

    [Fact(DisplayName = "O prazo contado do início de uma fase traz a fase e o dia do início no fuso do processo")]
    public void Projetar_PrazoNoInicioDaFase_TrazAFaseEADataDoInicio()
    {
        PrazoDeEmissao prazo = PrazoReferenciado(new DataDeReferenciaBuilder().NoInicioDaFase(OutraFase).Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComFases(Inscricao, Recurso).ComPrazoDeEmissao(prazo));

        exigencia.PrazoDeEmissao!.Referencia.Should().Be(Referencia(TipoDaDataDeReferencia.InicioDaFase, new DateOnly(2026, 2, 28), OutraFase));
    }

    [Fact(DisplayName = "O prazo contado do fim de uma fase traz a fase e o dia do término no fuso do processo")]
    public void Projetar_PrazoNoFimDaFase_TrazAFaseEADataDoTermino()
    {
        PrazoDeEmissao prazo = PrazoReferenciado(new DataDeReferenciaBuilder().NoFimDaFase(OutraFase).Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComFases(Inscricao, Recurso).ComPrazoDeEmissao(prazo));

        exigencia.PrazoDeEmissao!.Referencia.Should().Be(Referencia(TipoDaDataDeReferencia.FimDaFase, new DateOnly(2026, 3, 4), OutraFase));
    }

    [Fact(DisplayName = "O prazo contado de uma data específica traz a data como o edital a declarou, sem fase")]
    public void Projetar_PrazoNaDataEspecifica_TrazADataDeclarada()
    {
        PrazoDeEmissao prazo = PrazoReferenciado(new DataDeReferenciaBuilder().NaData(new DateOnly(2026, 6, 15)).Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComPrazoDeEmissao(prazo));

        exigencia.PrazoDeEmissao!.Referencia.Should().Be(Referencia(TipoDaDataDeReferencia.DataEspecifica, new DateOnly(2026, 6, 15)));
    }

    [Fact(DisplayName = "O prazo contado da data da apresentação devolve data nula: ela só existe quando o candidato apresenta o documento")]
    public void Projetar_PrazoNaDataDaApresentacao_DevolveDataNula()
    {
        PrazoDeEmissao prazo = PrazoReferenciado(new DataDeReferenciaBuilder().NaApresentacao().Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComPrazoDeEmissao(prazo));

        exigencia.PrazoDeEmissao!.Referencia.Should().Be(Referencia(TipoDaDataDeReferencia.DataDaApresentacao, null));
    }

    [Fact(DisplayName = "A fase de referência que o cronograma congelado não tem fica sem data, em vez de buscar a fase no cadastro")]
    public void Projetar_FaseForaDoCronogramaCongelado_FicaSemData()
    {
        PrazoDeEmissao prazo = PrazoReferenciado(new DataDeReferenciaBuilder().NoFimDaFase(OutraFase).Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComPrazoDeEmissao(prazo));

        exigencia.PrazoDeEmissao!.Referencia.Should().Be(Referencia(TipoDaDataDeReferencia.FimDaFase, null, OutraFase));
    }

    [Fact(DisplayName = "A validade exigida traz a referência resolvida e o efeito do vencimento")]
    public void Projetar_ValidadeExigida_TrazReferenciaEEfeito()
    {
        ValidadeDoDocumento validade = new ValidadeDoDocumentoBuilder()
            .Referenciada(new DataDeReferenciaBuilder().NoFimDaFase(OutraFase).Construir())
            .ComEfeito(EfeitoNaAnaliseDoDocumento.AceitarComAviso)
            .Construir();

        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComFases(Inscricao, Recurso).ComValidade(validade));

        exigencia.Validade.Should().Be(new ValidadeDoDocumentoCertameDto(
            CodigoComRotulo<EscolhaDaValidade>.De(EscolhaDaValidade.ExigeDocumentoValido),
            Referencia(TipoDaDataDeReferencia.FimDaFase, new DateOnly(2026, 3, 4), OutraFase),
            CodigoComRotulo<EfeitoNaAnaliseDoDocumento>.De(EfeitoNaAnaliseDoDocumento.AceitarComAviso)));
    }

    [Fact(DisplayName = "Sem prazo de emissão e sem validade exigida, o candidato não recebe nenhum dos dois objetos")]
    public void Projetar_SemPrazoENaoExigeValidade_DevolveNulos()
    {
        EnvelopeDoCertameBuilder envelope = new EnvelopeDoCertameBuilder()
            .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().SemPrazo().Construir())
            .ComValidade(new ValidadeDoDocumentoBuilder().NaoExige().Construir());

        ExigenciaDocumentalCertameDto exigencia = Projetada(envelope);

        exigencia.PrazoDeEmissao.Should().BeNull();
        exigencia.Validade.Should().BeNull();
    }

    [Fact(DisplayName = "A escolha ainda não feita não aparece para o candidato")]
    public void Projetar_EscolhaNaoFeita_DevolveNulos()
    {
        ExigenciaDocumentalCertameDto exigencia = Projetada(new EnvelopeDoCertameBuilder().ComPrazoDeEmissao(null).ComValidade(null));

        exigencia.PrazoDeEmissao.Should().BeNull();
        exigencia.Validade.Should().BeNull();
    }
}

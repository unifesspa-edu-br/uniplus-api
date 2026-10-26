namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O lote de importação e os lançamentos: as cinco áreas, a faixa da nota e os totais derivados do
/// cruzamento.
/// </summary>
public sealed class LoteDeNotasDoEnemTests
{
    private static readonly string Hash = new('a', 64);

    private static ResultadoDaAreaNoRetorno[] Areas() =>
    [
        .. AreaDoEnem.Todas.Select(a => new ResultadoDaAreaNoRetorno(a, 600m, SituacaoDaProvaNoEnem.Presente)),
    ];

    private static ResultadoDoCruzamento Cruzamento() => new(
        [new InscritoComNota(Guid.CreateVersion7(), "240000000001", Areas()), new InscritoComNota(Guid.CreateVersion7(), "240000000002", Areas())],
        [Guid.CreateVersion7()],
        [new(5, "***.456.789-**", null, null), new(6, "***.456.789-**", null, null), new(7, "***.456.789-**", null, null)],
        [new ConflitoDeLinhas(Guid.CreateVersion7(), [3, 4])]);

    private static LoteDeNotasDoEnem Registrar(ResultadoDoCruzamento cruzamento, int totalDeLinhas = 20, DateTimeOffset? em = null, string arquivoHash = "", IReadOnlyList<AvaliacaoApurada>? avaliacoes = null) =>
        LoteDeNotasDoEnem.Registrar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "2025", "INEP_ENEM_RETORNO", "v1", Hash,
            arquivoHash.Length == 0 ? Hash : arquivoHash, "sub-admin", em ?? DateTimeOffset.UtcNow, totalDeLinhas,
            Guid.CreateVersion7(), cruzamento, avaliacoes ?? []);

    [Fact(DisplayName = "os totais saem do cruzamento e cada inscrito com nota gera cinco lançamentos")]
    public void Registrar_TotaisBatemComOCruzamento()
    {
        LoteDeNotasDoEnem lote = Registrar(Cruzamento());

        lote.TotalDeInscritosComNota.Should().Be(2);
        lote.TotalDeLancamentos.Should().Be(10);
        lote.TotalDeInscritosSemLinha.Should().Be(1);
        lote.TotalDeLinhasSemInscrito.Should().Be(3);
        lote.TotalDeInscritosEmConflito.Should().Be(1);
        lote.Lancamentos.Should().HaveCount(10).And.OnlyContain(l => l.LoteId == lote.Id && l.EtapaProcessoId == lote.EtapaProcessoId);
        lote.Inscritos.Should().HaveCount(2);
        lote.Ocorrencias.Should().HaveCount(1 + 3 + 1);
    }

    [Fact(DisplayName = "total de linhas menor que o das linhas explicadas pelo cruzamento é recusado")]
    public void Registrar_TotalDeLinhasIncoerente_Recusa()
    {
        Action registrar = () => Registrar(Cruzamento(), totalDeLinhas: 5);

        registrar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "lançamento fora das cinco áreas ou da faixa de 0 a 1000 é recusado")]
    [InlineData("HISTORIA", 500)]
    [InlineData("MATEMATICA", -0.1)]
    [InlineData("MATEMATICA", 1000.1)]
    public void LancamentoDeNota_ForaDaRegra_Recusa(string item, double nota)
    {
        Action registrar = () => LancamentoDeNota.Registrar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), item, (decimal)nota, SituacaoDaProvaNoEnem.Presente);

        registrar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "lançamento aceita as notas de borda 0 e 1000")]
    [InlineData(0)]
    [InlineData(1000)]
    public void LancamentoDeNota_NotasDeBorda_Aceita(double nota)
    {
        LancamentoDeNota lancamento = LancamentoDeNota.Registrar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), AreaDoEnem.Redacao, (decimal)nota, SituacaoDaProvaNoEnem.Ausente);

        lancamento.Nota.Should().Be((decimal)nota);
    }

    [Fact(DisplayName = "conflito com menos de duas linhas é recusado")]
    public void OcorrenciaConflito_ComUmaLinha_Recusa()
    {
        Action registrar = () => OcorrenciaDoLoteDeNotas.Conflito(Guid.CreateVersion7(), Guid.CreateVersion7(), [3]);

        registrar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "o lote guarda a versão e as avaliações, cada uma na situação que o cruzamento deu à inscrição")]
    public void Registrar_ComAvaliacoes_GuardaAsDoCruzamento()
    {
        ResultadoDoCruzamento cruzamento = Cruzamento();
        Guid oferta = Guid.CreateVersion7();
        IReadOnlyList<AvaliacaoApurada> avaliacoes =
        [
            new(cruzamento.ComNota[0].InscricaoId, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, 674.5m),
            new(cruzamento.InscritosSemLinha[0], oferta, "TECNOLOGICA", SituacaoDaAvaliacao.InscritoSemLinha, null),
            new(cruzamento.Conflitos[0].InscricaoId, oferta, "TECNOLOGICA", SituacaoDaAvaliacao.EmConflito, null),
        ];

        LoteDeNotasDoEnem lote = Registrar(cruzamento, avaliacoes: avaliacoes);

        lote.VersaoConfiguracaoId.Should().NotBeEmpty();
        lote.Avaliacoes.Should().HaveCount(3).And.OnlyContain(a => a.LoteId == lote.Id && a.EtapaProcessoId == lote.EtapaProcessoId);
    }

    [Fact(DisplayName = "avaliação de inscrição fora do cruzamento, ou com situação que o cruzamento não deu, é recusada")]
    public void Registrar_AvaliacaoIncoerenteComOCruzamento_Recusa()
    {
        ResultadoDoCruzamento cruzamento = Cruzamento();
        Guid oferta = Guid.CreateVersion7();
        AvaliacaoApurada deQuemNaoEDoCruzamento = new(Guid.CreateVersion7(), oferta, "TECNOLOGICA", SituacaoDaAvaliacao.InscritoSemLinha, null);
        AvaliacaoApurada comSituacaoErrada = new(cruzamento.InscritosSemLinha[0], oferta, "TECNOLOGICA", SituacaoDaAvaliacao.Apurada, 600m);

        Action fora = () => Registrar(cruzamento, avaliacoes: [deQuemNaoEDoCruzamento]);
        Action errada = () => Registrar(cruzamento, avaliacoes: [comSituacaoErrada]);

        fora.Should().Throw<ArgumentException>();
        errada.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "só a avaliação apurada tem nota, e a nota fica entre 0 e 1000")]
    [InlineData(SituacaoDaAvaliacao.Apurada, null)]
    [InlineData(SituacaoDaAvaliacao.EmConflito, 0.0)]
    [InlineData(SituacaoDaAvaliacao.Apurada, 1000.1)]
    [InlineData(SituacaoDaAvaliacao.Apurada, -0.1)]
    public void AvaliacaoDaEtapa_NotaIncoerenteComASituacao_Recusa(SituacaoDaAvaliacao situacao, double? nota)
    {
        AvaliacaoApurada apurada = new(Guid.CreateVersion7(), Guid.CreateVersion7(), "TECNOLOGICA", situacao, (decimal?)nota);

        Action registrar = () => AvaliacaoDaEtapa.Registrar(Guid.CreateVersion7(), Guid.CreateVersion7(), apurada);

        registrar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "o arquivo do lote vigente é reconhecido só sob a mesma versão do edital; o de um lote substituído não")]
    public void ArquivoJaVigente_SoReconheceOHashDoVigente()
    {
        DateTimeOffset base0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        LoteDeNotasDoEnem antigo = Registrar(Cruzamento(), em: base0, arquivoHash: new string('b', 64));
        LoteDeNotasDoEnem vigente = Registrar(Cruzamento(), em: base0.AddHours(1), arquivoHash: new string('c', 64));

        VigenciaDoLote.ArquivoJaVigente(vigente, vigente.ArquivoHash, vigente.VersaoConfiguracaoId).Should().BeTrue();
        VigenciaDoLote.ArquivoJaVigente(vigente, vigente.ArquivoHash, Guid.CreateVersion7()).Should().BeFalse();
        VigenciaDoLote.ArquivoJaVigente(vigente, antigo.ArquivoHash, vigente.VersaoConfiguracaoId).Should().BeFalse();
        VigenciaDoLote.ArquivoJaVigente(null, vigente.ArquivoHash, vigente.VersaoConfiguracaoId).Should().BeFalse();
    }
}

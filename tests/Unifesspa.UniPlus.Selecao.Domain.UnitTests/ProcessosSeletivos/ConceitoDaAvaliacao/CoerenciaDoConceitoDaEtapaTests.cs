namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O conceito que a etapa declara tem de ser um dos que o tipo admite, o conceito categórico não tem nota
/// (UNI-REQ-0175) e a nota máxima cabe na escala do conceito numérico (ADR-0169).
/// </summary>
public sealed class CoerenciaDoConceitoDaEtapaTests
{
    private static readonly string[] AdmitidosNotaEAptoInapto = ["NOTA", "APTO_INAPTO"];

    private static ConceitoDaEtapa Nota() =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", FormaDoConceito.Numerico, new EscalaInformada(0m, 10m, 2), null).Value!;

    private static ConceitoDaEtapa AptoInapto() =>
        ConceitoDaEtapa.Criar(
            "APTO_INAPTO", "Apto ou inapto", FormaDoConceito.PorOpcoes, null,
            [new ValorCategoricoInformado("APTO", "Apto", true), new ValorCategoricoInformado("INAPTO", "Inapto", false)]).Value!;

    private static DeclaracaoDaAvaliacaoDaEtapa Declaracao(
        decimal? peso = null,
        decimal? notaMinima = null,
        decimal? notaMaxima = null,
        decimal? passo = null,
        IReadOnlyList<NivelDeAtendimento?>? escala = null) =>
        new(peso, notaMinima, notaMaxima, passo, FormaDeAvaliacao.Humana, null, escala);

    private static (string? Campo, string Codigo)[] Recusas(
        ConceitoDaEtapa? conceito,
        DeclaracaoDaAvaliacaoDaEtapa declaracao,
        string[]? admitidos = null,
        bool declaraItens = false,
        bool etapaDeNotaDoEnem = false) =>
        [.. CoerenciaDoConceitoDaEtapa.Validar(conceito, admitidos ?? AdmitidosNotaEAptoInapto, declaracao, declaraItens, etapaDeNotaDoEnem)
            .Select(static erro => (erro.Field, erro.Error.Code))];

    [Fact(DisplayName = "O conceito entre os admitidos pelo tipo é aceito")]
    public void ConceitoAdmitido_Aceita()
    {
        Recusas(Nota(), Declaracao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito fora dos admitidos pelo tipo é recusado")]
    public void ConceitoNaoAdmitido_Recusa()
    {
        Recusas(Nota(), Declaracao(), admitidos: ["APTO_INAPTO"]).Should().Equal(("conceito", ConceitoDaEtapaErrorCodes.NaoAdmitidoPeloTipo));
    }

    [Fact(DisplayName = "A admissão compara o código de forma ordinal, mesmo com a coleção de comparador insensível a caixa")]
    public void Admissao_ComparaOrdinal()
    {
        ConceitoDaEtapa conceito = Nota()!;
        HashSet<string> admitidosSemDistinguirCaixa = new([conceito.Codigo.ToLowerInvariant()], StringComparer.OrdinalIgnoreCase);

        CoerenciaDoConceitoDaEtapa.Validar(conceito, admitidosSemDistinguirCaixa, Declaracao(), declaraItens: false, etapaDeNotaDoEnem: false)
            .Select(static erro => erro.Error.Code).Should().Equal(ConceitoDaEtapaErrorCodes.NaoAdmitidoPeloTipo);
    }

    [Fact(DisplayName = "O tipo sem conceitos admitidos recusa qualquer conceito")]
    public void TipoSemAdmitidos_Recusa()
    {
        Recusas(Nota(), Declaracao(), admitidos: []).Should().Equal(("conceito", ConceitoDaEtapaErrorCodes.NaoAdmitidoPeloTipo));
    }

    [Fact(DisplayName = "A etapa sem conceito não tem o que conferir, ainda que declare nota")]
    public void SemConceito_NaoRecusaNada()
    {
        Recusas(conceito: null, Declaracao(notaMaxima: 100m), admitidos: []).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito categórico sem campo de nota declarado é aceito")]
    public void CategoricoSemNota_Aceita()
    {
        Recusas(AptoInapto(), Declaracao()).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito categórico recusa a nota máxima declarada")]
    public void Categorico_ComNotaMaxima_Recusa()
    {
        Recusas(AptoInapto(), Declaracao(notaMaxima: 10m)).Should().Equal(("notaMaxima", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico recusa a nota mínima declarada")]
    public void Categorico_ComNotaMinima_Recusa()
    {
        Recusas(AptoInapto(), Declaracao(notaMinima: 5m)).Should().Equal(("notaMinima", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico recusa o peso declarado")]
    public void Categorico_ComPeso_Recusa()
    {
        Recusas(AptoInapto(), Declaracao(peso: 2m)).Should().Equal(("peso", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico recusa o passo do lançamento declarado")]
    public void Categorico_ComPasso_Recusa()
    {
        Recusas(AptoInapto(), Declaracao(passo: 0.5m)).Should().Equal(("passoDoLancamento", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico recusa os itens de avaliação declarados")]
    public void Categorico_ComItens_Recusa()
    {
        Recusas(AptoInapto(), Declaracao(), declaraItens: true).Should().Equal(("itensDeAvaliacao", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico recusa a escala de atendimento declarada")]
    public void Categorico_ComEscalaDeAtendimento_Recusa()
    {
        DeclaracaoDaAvaliacaoDaEtapa declaracao = Declaracao(escala: [new NivelDeAtendimento("Atendeu", 1m)]);

        Recusas(AptoInapto(), declaracao).Should().Equal(("escalaDeAtendimento", ConceitoDaEtapaErrorCodes.NotaNoConceitoCategorico));
    }

    [Fact(DisplayName = "O conceito categórico com todos os campos de nota recusa um por campo, na ordem")]
    public void Categorico_ComTodosOsCamposDeNota_RecusaUmPorCampo()
    {
        DeclaracaoDaAvaliacaoDaEtapa declaracao = Declaracao(2m, 5m, 10m, 0.5m, [new NivelDeAtendimento("Atendeu", 1m)]);

        Recusas(AptoInapto(), declaracao, declaraItens: true).Select(static recusa => recusa.Campo).Should().Equal(
            "notaMaxima", "notaMinima", "peso", "passoDoLancamento", "itensDeAvaliacao", "escalaDeAtendimento");
    }

    [Fact(DisplayName = "A nota máxima igual à escala máxima do conceito numérico é aceita")]
    public void NotaMaximaIgualAEscala_Aceita()
    {
        Recusas(Nota(), Declaracao(notaMaxima: 10m)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A nota máxima 0,0001 acima da escala máxima do conceito numérico é recusada")]
    public void NotaMaximaAcimaDaEscala_Recusa()
    {
        Recusas(Nota(), Declaracao(notaMaxima: 10.0001m)).Should().Equal(("notaMaxima", ConceitoDaEtapaErrorCodes.NotaMaximaAcimaDaEscala));
    }

    [Theory(DisplayName = "A nota máxima menor ou igual a zero não é conferida contra a escala do conceito")]
    [InlineData(0)]
    [InlineData(-1)]
    public void NotaMaximaNaoPositiva_NaoGeraARecusaDaEscala(int notaMaxima)
    {
        Recusas(Nota(), Declaracao(notaMaxima: notaMaxima)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A nota máxima sem nota declarada não gera recusa da escala")]
    public void SemNotaMaxima_NaoGeraARecusaDaEscala()
    {
        Recusas(Nota(), Declaracao(peso: 2m)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito não admitido e a nota máxima acima da escala acumulam as duas recusas, nessa ordem")]
    public void NaoAdmitidoENotaAcimaDaEscala_Acumula()
    {
        Recusas(Nota(), Declaracao(notaMaxima: 11m), admitidos: []).Should().Equal(
            ("conceito", ConceitoDaEtapaErrorCodes.NaoAdmitidoPeloTipo),
            ("notaMaxima", ConceitoDaEtapaErrorCodes.NotaMaximaAcimaDaEscala));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa o conceito e não repete as demais recusas")]
    public void EtapaDoEnem_RecusaOConceito()
    {
        Recusas(AptoInapto(), Declaracao(notaMaxima: 10m), admitidos: [], etapaDeNotaDoEnem: true)
            .Should().Equal(("conceito", ConceitoDaEtapaErrorCodes.EtapaDoEnemNaoDeclaraConceito));
    }

    [Fact(DisplayName = "A etapa de nota do ENEM sem conceito não tem recusa")]
    public void EtapaDoEnemSemConceito_Aceita()
    {
        Recusas(conceito: null, Declaracao(), etapaDeNotaDoEnem: true).Should().BeEmpty();
    }

    [Fact(DisplayName = "A mensagem da recusa não repete a nota declarada")]
    public void Mensagem_NaoRepeteOValorRecusado()
    {
        IReadOnlyList<FieldError> erros = CoerenciaDoConceitoDaEtapa.Validar(
            Nota(), AdmitidosNotaEAptoInapto, Declaracao(notaMaxima: 12.3456m), declaraItens: false, etapaDeNotaDoEnem: false);

        erros.Should().ContainSingle().Which.Error.Message.Should().NotContain("12");
    }

    [Fact(DisplayName = "O conceito declarado não confere o que o tipo admite: só a coerência com o resto da avaliação")]
    public void ValidarDeclarado_NaoConfereAAdmissao()
    {
        CoerenciaDoConceitoDaEtapa.ValidarDeclarado(Nota(), Declaracao(notaMaxima: 10m), declaraItens: false, etapaDeNotaDoEnem: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito declarado com teto acima da escala recusa a nota máxima, e sem nota máxima não recusa")]
    public void ValidarDeclarado_TetoAcimaDaEscala_Recusa()
    {
        CoerenciaDoConceitoDaEtapa.ValidarDeclarado(Nota(), Declaracao(notaMaxima: 10.01m), declaraItens: false, etapaDeNotaDoEnem: false)
            .Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(("notaMaxima", ConceitoDaEtapaErrorCodes.NotaMaximaAcimaDaEscala));
        CoerenciaDoConceitoDaEtapa.ValidarDeclarado(Nota(), Declaracao(), declaraItens: false, etapaDeNotaDoEnem: false).Should().BeEmpty();
    }

    [Fact(DisplayName = "O conceito categórico declarado recusa cada campo de nota declarado, inclusive os itens de avaliação")]
    public void ValidarDeclarado_CategoricoComCamposDeNota_RecusaCadaUm()
    {
        CoerenciaDoConceitoDaEtapa.ValidarDeclarado(AptoInapto(), Declaracao(peso: 1m, notaMinima: 5m), declaraItens: true, etapaDeNotaDoEnem: false)
            .Select(static erro => erro.Field).Should().Equal("notaMinima", "peso", "itensDeAvaliacao");
    }

    [Fact(DisplayName = "O conceito declarado na etapa do ENEM recusa só o conceito")]
    public void ValidarDeclarado_EtapaDoEnem_RecusaSoOConceito()
    {
        CoerenciaDoConceitoDaEtapa.ValidarDeclarado(AptoInapto(), Declaracao(notaMaxima: 10m), declaraItens: true, etapaDeNotaDoEnem: true)
            .Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(("conceito", ConceitoDaEtapaErrorCodes.EtapaDoEnemNaoDeclaraConceito));
    }
}

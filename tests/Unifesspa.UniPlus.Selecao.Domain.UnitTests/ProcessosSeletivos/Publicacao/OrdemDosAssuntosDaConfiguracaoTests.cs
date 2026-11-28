namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

/// <summary>
/// As duas transições que congelam uma versão recusam os assuntos da configuração na mesma ordem, e acumulam todas as causas
/// (ADR-0125). A ordem decide qual código o cliente vê primeiro: trocar dois assuntos deixaria o cliente vendo a causa errada.
/// </summary>
/// <remarks>
/// A sequência dos assuntos está escrita neste teste, sem ler a lista de produção: ela falha se dois assuntos trocarem de
/// posição e se a conferência ganhar ou perder um assunto. O cenário tem um defeito característico para cada assunto, pelo
/// nome, menos o da importação do resultado, que só existe no processo de resultado importado. Com os defeitos do primeiro
/// assunto em diante ligados, a primeira causa tem de ser a do assunto que vem a seguir, e a lista das causas é a concatenação dos assuntos na ordem.
/// </remarks>
public sealed class OrdemDosAssuntosDaConfiguracaoTests
{
    private static readonly string[] SequenciaEsperada =
    [
        "Identificacao", "Conformidade", "Declaracoes", "ReferenciasDeRegra", "Conjunto", "Completude", "Recursos",
        "Bancas", "CorrecaoIndependente", "TiposDeAto", "Cascata", "ConsequenciasDoDescumprimento", "CondicoesParaRegistrarConfiguracao", "MotivosDeDecisao",
        "ProvaPresencial",
        "ImportacaoDoResultado",
    ];


    private static readonly string[] AssuntosQueOCenarioQuebra =
        [.. SequenciaEsperada.Except(CenarioDeCongelamentoPorAssunto.AssuntosSoDoResultadoImportado)];

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar"];

    [Fact(DisplayName = "A conferência tem os assuntos na ordem única escrita no teste: trocar dois de lugar, ganhar ou perder um reprova")]
    public void Assuntos_SaoAsDaSequenciaEsperadaNaOrdem() =>
        CausasQueImpedemCongelar.Assuntos.Select(static assunto => assunto.Nome).Should().Equal(SequenciaEsperada);

    [Fact(DisplayName = "O cenário sabe quebrar exatamente os assuntos da sequência esperada, menos os do processo de resultado importado")]
    public void Cenario_QuebraExatamenteOsAssuntosDaSequencia() =>
        CenarioDeCongelamentoPorAssunto.AssuntosQueSabeQuebrar.Should().BeEquivalentTo(SequenciaEsperada.Except(CenarioDeCongelamentoPorAssunto.AssuntosSoDoResultadoImportado));

    [Theory(DisplayName = "Sem nenhum defeito, a transição tem sucesso e nenhum assunto tem causa")]
    [MemberData(nameof(Transicoes))]
    public void SemDefeito_TransicaoTemSucesso(string transicao)
    {
        CenarioDeCongelamentoPorAssunto cenario = CenarioDeCongelamentoPorAssunto.Para(transicao);

        cenario.CausasPorAssunto().Should().OnlyContain(static doAssunto => doAssunto.Causas.Count == 0);
        Result<VersaoConfiguracao> resultado = cenario.Congelar(new HashSet<string>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Com os defeitos do primeiro assunto em diante, a primeira causa é a do primeiro assunto com defeito, e cada correção revela a seguinte")]
    [MemberData(nameof(Transicoes))]
    public void PrimeiraCausa_SegueAOrdemDosAssuntos(string transicao)
    {
        foreach (string assunto in AssuntosQueOCenarioQuebra)
        {
            ConferirAPrimeiraCausaDoAssunto(transicao, assunto);
        }
    }

    private static void ConferirAPrimeiraCausaDoAssunto(string transicao, string assunto)
    {
        CenarioDeCongelamentoPorAssunto cenario = CenarioDeCongelamentoPorAssunto.Para(transicao);

        Result<VersaoConfiguracao> resultado = cenario.Congelar(AssuntosQueOCenarioQuebra.SkipWhile(outro => outro != assunto).ToHashSet(StringComparer.Ordinal));

        IReadOnlyList<(string Assunto, IReadOnlyList<FieldError> Causas)> porAssunto = cenario.CausasPorAssunto();
        (string Assunto, IReadOnlyList<FieldError> Causas) doAssunto = porAssunto.Single(causas => causas.Assunto == assunto);
        porAssunto.TakeWhile(anterior => anterior.Assunto != assunto).Should().OnlyContain(static anterior => anterior.Causas.Count == 0, $"só os assuntos de {assunto} em diante têm defeito");
        doAssunto.Causas.Should().NotBeEmpty($"o assunto {assunto} tem defeito");
        resultado.Error!.Code.Should().Be(doAssunto.Causas[0].Error.Code, $"a primeira causa é a do assunto {assunto}");
    }

    [Theory(DisplayName = "O processo que falha em todos os assuntos devolve as causas de cada assunto, na ordem única")]
    [MemberData(nameof(Transicoes))]
    public void ProcessoQueFalhaEmTodosOsAssuntos_DevolveAsCausasDeCadaAssuntoNaOrdemUnica(string transicao)
    {
        CenarioDeCongelamentoPorAssunto cenario = CenarioDeCongelamentoPorAssunto.Para(transicao);

        Result<VersaoConfiguracao> resultado = cenario.Congelar(AssuntosQueOCenarioQuebra.ToHashSet(StringComparer.Ordinal));

        IReadOnlyList<(string Assunto, IReadOnlyList<FieldError> Causas)> porAssunto = cenario.CausasPorAssunto();
        porAssunto.Where(static doAssunto => doAssunto.Causas.Count == 0).Select(static doAssunto => doAssunto.Assunto)
            .Should().Equal(CenarioDeCongelamentoPorAssunto.AssuntosSoDoResultadoImportado, "só o assunto do processo de resultado importado não tem causa neste cenário");
        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code))
            .Should().Equal(porAssunto.SelectMany(static doAssunto => doAssunto.Causas).Select(static causa => (causa.Field, causa.Error.Code)));
        resultado.Errors.Should().OnlyContain(static causa => !string.IsNullOrEmpty(causa.Field));
    }
}

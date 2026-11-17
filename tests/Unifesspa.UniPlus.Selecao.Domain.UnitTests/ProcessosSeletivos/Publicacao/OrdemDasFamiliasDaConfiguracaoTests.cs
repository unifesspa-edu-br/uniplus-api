namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

/// <summary>
/// As duas transições que congelam uma versão recusam as famílias da configuração na mesma ordem, e acumulam todas as causas
/// (ADR-0125). A ordem decide qual código o cliente vê primeiro: trocar duas famílias deixaria o cliente vendo a causa errada.
/// </summary>
/// <remarks>
/// A sequência das famílias está escrita neste teste, sem ler a lista de produção: ela falha se duas famílias trocarem de
/// posição e se a conferência ganhar ou perder uma família. O cenário tem um defeito característico para cada família, pelo
/// nome. Com os defeitos da primeira família em diante ligados, a primeira causa tem de ser a da família que vem a seguir, e a
/// lista das causas é a concatenação das famílias na ordem.
/// </remarks>
public sealed class OrdemDasFamiliasDaConfiguracaoTests
{
    private static readonly string[] SequenciaEsperada =
    [
        "Identificacao", "Conformidade", "Declaracoes", "Conjunto", "Completude", "Recursos",
        "Bancas", "CorrecaoIndependente", "TiposDeAto", "Cascata", "CondicoesDaCanonicalizacao", "MotivosDeDecisao",
    ];

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar"];

    [Fact(DisplayName = "A conferência tem as famílias na ordem única escrita no teste: trocar duas de lugar, ganhar ou perder uma reprova")]
    public void Familias_SaoAsDaSequenciaEsperadaNaOrdem() =>
        CausasQueImpedemCongelar.Familias.Select(static familia => familia.Nome).Should().Equal(SequenciaEsperada);

    [Fact(DisplayName = "O cenário sabe quebrar exatamente as famílias da sequência esperada")]
    public void Cenario_QuebraExatamenteAsFamiliasDaSequencia() =>
        CenarioDeCongelamentoPorFamilia.FamiliasQueSabeQuebrar.Should().BeEquivalentTo(SequenciaEsperada);

    [Theory(DisplayName = "Sem nenhum defeito, a transição tem sucesso e nenhuma família tem causa")]
    [MemberData(nameof(Transicoes))]
    public void SemDefeito_TransicaoTemSucesso(string transicao)
    {
        CenarioDeCongelamentoPorFamilia cenario = CenarioDeCongelamentoPorFamilia.Para(transicao);

        cenario.CausasPorFamilia().Should().OnlyContain(static daFamilia => daFamilia.Causas.Count == 0);
        Result<VersaoConfiguracao> resultado = cenario.Congelar(new HashSet<string>());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Theory(DisplayName = "Com os defeitos da primeira família em diante, a primeira causa é a da primeira família com defeito, e cada correção revela a seguinte")]
    [MemberData(nameof(Transicoes))]
    public void PrimeiraCausa_SegueAOrdemDasFamilias(string transicao)
    {
        for (int primeira = 0; primeira < SequenciaEsperada.Length; primeira++)
        {
            CenarioDeCongelamentoPorFamilia cenario = CenarioDeCongelamentoPorFamilia.Para(transicao);

            Result<VersaoConfiguracao> resultado = cenario.Congelar(SequenciaEsperada.Skip(primeira).ToHashSet(StringComparer.Ordinal));

            IReadOnlyList<(string Familia, IReadOnlyList<FieldError> Causas)> porFamilia = cenario.CausasPorFamilia();
            porFamilia.Take(primeira).Should().OnlyContain(static daFamilia => daFamilia.Causas.Count == 0, $"só as famílias de {SequenciaEsperada[primeira]} em diante têm defeito");
            porFamilia[primeira].Causas.Should().NotBeEmpty($"a família {SequenciaEsperada[primeira]} tem defeito");
            resultado.Error!.Code.Should().Be(porFamilia[primeira].Causas[0].Error.Code, $"a primeira causa é a da família {SequenciaEsperada[primeira]}");
        }
    }

    [Theory(DisplayName = "O processo que falha em todas as famílias devolve as causas de cada família, na ordem única")]
    [MemberData(nameof(Transicoes))]
    public void ProcessoQueFalhaEmTodasAsFamilias_DevolveAsCausasDeCadaFamiliaNaOrdemUnica(string transicao)
    {
        CenarioDeCongelamentoPorFamilia cenario = CenarioDeCongelamentoPorFamilia.Para(transicao);

        Result<VersaoConfiguracao> resultado = cenario.Congelar(SequenciaEsperada.ToHashSet(StringComparer.Ordinal));

        IReadOnlyList<(string Familia, IReadOnlyList<FieldError> Causas)> porFamilia = cenario.CausasPorFamilia();
        porFamilia.Where(static daFamilia => daFamilia.Causas.Count == 0).Select(static daFamilia => daFamilia.Familia)
            .Should().BeEmpty("todas as famílias têm causa característica no cenário");
        resultado.Errors.Select(static causa => (causa.Field, causa.Error.Code))
            .Should().Equal(porFamilia.SelectMany(static daFamilia => daFamilia.Causas).Select(static causa => (causa.Field, causa.Error.Code)));
        resultado.Errors.Should().OnlyContain(static causa => !string.IsNullOrEmpty(causa.Field));
    }
}

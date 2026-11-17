namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Nenhum tipo nem membro deste projeto de teste, auxiliares inclusive, leva "retrato" no nome.</summary>
public sealed class NomesSemMetaforaTests
{
    private const string Padrao = @".*";

    [Fact(DisplayName = "Nenhum tipo nem membro do projeto de teste leva retrato no nome")]
    public void Projeto_NaoLevaMetaforaNoNome() =>
        RegraSemMetaforaNosNomes.Violacoes(typeof(NomesSemMetaforaTests).Assembly, Padrao).Should().BeEmpty();

    [Fact(DisplayName = "A regra enxerga os tipos do projeto de teste, para não passar vazia")]
    public void Regra_EnxergaOsTiposDoProjeto() =>
        RegraSemMetaforaNosNomes.TiposVistos(typeof(NomesSemMetaforaTests).Assembly, Padrao).Should().BeGreaterThan(0);
}

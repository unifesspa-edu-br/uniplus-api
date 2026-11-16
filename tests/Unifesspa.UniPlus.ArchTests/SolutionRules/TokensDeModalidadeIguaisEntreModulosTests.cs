namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A natureza legal, a composição de vagas e a regra de remanejamento de uma modalidade têm um token
/// canônico único: o que o cadastro de modalidades (Configuração) grava e publica é o que a Seleção copia,
/// grava e congela no envelope. Duas listas que divergem fariam a cópia de uma modalidade perder o campo
/// sem erro.
/// </summary>
public sealed class TokensDeModalidadeIguaisEntreModulosTests
{
    private static string[] TokensDaSelecao<TEnum>(Func<TEnum, string?> paraToken)
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Where(static valor => !valor.ToString().Equals("Nenhuma", StringComparison.Ordinal))
            .Select(paraToken).OfType<string>()];

    [Fact(DisplayName = "A composição de vagas tem os mesmos tokens na Configuração e na Seleção")]
    public void Composicao_MesmosTokens() =>
        TokensDaSelecao<ComposicaoVagasModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(ComposicoesVagas.TokensCanonicos);

    [Fact(DisplayName = "A natureza legal tem os mesmos tokens na Configuração e na Seleção")]
    public void Natureza_MesmosTokens() =>
        TokensDaSelecao<NaturezaLegalModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(NaturezasLegais.TokensCanonicos);

    [Fact(DisplayName = "A regra de remanejamento tem os mesmos tokens na Configuração e na Seleção")]
    public void Remanejamento_MesmosTokens() =>
        TokensDaSelecao<RegraRemanejamentoModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(RegrasRemanejamento.TokensCanonicos);

    [Fact(DisplayName = "Os tokens de composição são os da lista fechada, sem abreviação")]
    public void Composicao_ListaFechada() =>
        ComposicoesVagas.TokensCanonicos.Should().BeEquivalentTo(
            "DENTRO_DA_RESERVA_LEGAL", "RESTANTE_DAS_VAGAS_OFERTADAS", "RETIRADA_DE_OUTRA_MODALIDADE", "SUPLEMENTAR_AO_TOTAL");
}

namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// A natureza legal, a composição de vagas e a regra de remanejamento de uma modalidade têm um código
/// canônico único: o que o cadastro de modalidades (Configuração) grava e publica é o que a Seleção copia,
/// grava e congela no envelope. Duas listas que divergem fariam a cópia de uma modalidade perder o campo
/// sem erro.
/// </summary>
public sealed class CodigosDeModalidadeIguaisEntreModulosTests
{
    private static string[] CodigosDaSelecao<TEnum>(Func<TEnum, string?> paraCodigo)
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Where(static valor => !valor.ToString().Equals("Nenhuma", StringComparison.Ordinal))
            .Select(paraCodigo).OfType<string>()];

    private static string[] CodigosDoCatalogo<TEnum>()
        where TEnum : struct, Enum =>
        [.. CatalogoDe<TEnum>.Todos().Select(static declarado => declarado.Codigo)];

    [Fact(DisplayName = "A composição de vagas tem os mesmos códigos na Configuração e na Seleção")]
    public void Composicao_MesmosCodigos() =>
        CodigosDaSelecao<ComposicaoVagasModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(CodigosDoCatalogo<ComposicaoVagas>());

    [Fact(DisplayName = "A natureza legal tem os mesmos códigos na Configuração e na Seleção")]
    public void Natureza_MesmosCodigos() =>
        CodigosDaSelecao<NaturezaLegalModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(CodigosDoCatalogo<NaturezaLegal>());

    [Fact(DisplayName = "A regra de remanejamento tem os mesmos códigos na Configuração e na Seleção")]
    public void Remanejamento_MesmosCodigos() =>
        CodigosDaSelecao<RegraRemanejamentoModalidade>(static valor => valor.ToCodigo())
            .Should().BeEquivalentTo(CodigosDoCatalogo<RegraRemanejamento>());

    [Fact(DisplayName = "Os códigos de composição são os da lista fechada, sem abreviação")]
    public void Composicao_ListaFechada() =>
        CodigosDoCatalogo<ComposicaoVagas>().Should().BeEquivalentTo(
            "DENTRO_DA_RESERVA_LEGAL", "RESTANTE_DAS_VAGAS_OFERTADAS", "RETIRADA_DE_OUTRA_MODALIDADE", "SUPLEMENTAR_AO_TOTAL");
}

namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;

public sealed class MecanismoDoVinculoCodigosTests
{
    [Fact(DisplayName = "Cada mecanismo do vínculo tem o código e o rótulo do contrato")]
    public void MecanismoDoVinculo_CadaValor_TemCodigoERotuloDeclarados() =>
        Enum.GetValues<MecanismoDoVinculo>()
            .ToDictionary(static valor => valor, static valor => (valor.Codigo(), valor.Rotulo()))
            .Should().Equal(new Dictionary<MecanismoDoVinculo, (string, string)>
            {
                [MecanismoDoVinculo.CampoFormulario] = ("CAMPO_FORMULARIO", "Campo do formulário"),
                [MecanismoDoVinculo.AtributoCandidato] = ("ATRIBUTO_CANDIDATO", "Atributo do candidato"),
                [MecanismoDoVinculo.RegraDeDerivacao] = ("REGRA_DE_CALCULO", "Regra de cálculo"),
                [MecanismoDoVinculo.Classificacao] = ("CLASSIFICACAO", "Classificação"),
                [MecanismoDoVinculo.Integracao] = ("INTEGRACAO", "Integração"),
                [MecanismoDoVinculo.ResumoDeGrupo] = ("RESUMO_DE_GRUPO", "Resumo de grupo"),
            });

    [Fact(DisplayName = "Os mecanismos do vínculo seguem a ordem de exibição e o código antigo ou desconhecido não é encontrado")]
    public void MecanismoDoVinculo_Catalogo_SegueAOrdemDeDeclaracaoERecusaCodigoDesconhecido()
    {
        CatalogoDe<MecanismoDoVinculo>.Valores().Should().Equal(
            MecanismoDoVinculo.CampoFormulario,
            MecanismoDoVinculo.AtributoCandidato,
            MecanismoDoVinculo.RegraDeDerivacao,
            MecanismoDoVinculo.Classificacao,
            MecanismoDoVinculo.Integracao,
            MecanismoDoVinculo.ResumoDeGrupo);
        CatalogoDe<MecanismoDoVinculo>.TentarObter("AGREGACAO_GRUPO", out _).Should().BeFalse();
        CatalogoDe<MecanismoDoVinculo>.TentarObter("campo_formulario", out _).Should().BeFalse();
        CatalogoDe<MecanismoDoVinculo>.TentarObter(null, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "O vínculo é o código do mecanismo, dois pontos e a referência, e só o mecanismo dele o reconhece")]
    public void VinculoDeFato_MontaELe_PeloCodigoDoMecanismo()
    {
        string vinculo = VinculoDeFato.De(MecanismoDoVinculo.ResumoDeGrupo, "MENOR_SOB_GUARDA");

        vinculo.Should().Be("RESUMO_DE_GRUPO:MENOR_SOB_GUARDA");
        VinculoDeFato.Usa(vinculo, MecanismoDoVinculo.ResumoDeGrupo).Should().BeTrue();
        VinculoDeFato.Usa(vinculo, MecanismoDoVinculo.CampoFormulario).Should().BeFalse();
        VinculoDeFato.Nomeado(vinculo, MecanismoDoVinculo.ResumoDeGrupo).Should().Be("MENOR_SOB_GUARDA");
        VinculoDeFato.Nomeado(vinculo, MecanismoDoVinculo.Classificacao).Should().BeNull();
        VinculoDeFato.Usa("RESUMO_DE_GRUPO:", MecanismoDoVinculo.ResumoDeGrupo).Should().BeFalse("o vínculo sem referência não nomeia o que o produz");
    }
}

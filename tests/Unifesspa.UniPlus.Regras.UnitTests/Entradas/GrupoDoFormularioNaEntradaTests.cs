namespace Unifesspa.UniPlus.Regras.UnitTests.Entradas;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>O grupo repetível na entrada: as ocorrências e as regras do grupo, de ida e de volta ao domínio.</summary>
public sealed class GrupoDoFormularioNaEntradaTests
{
    [Fact(DisplayName = "As ocorrências do domínio vão à entrada com o mínimo, o máximo e o candidato entre os membros")]
    public void ParaEntrada_Ocorrencias_LevaOsTresValores()
    {
        OcorrenciasDoGrupoInput entrada = EntradaDeRegras.ParaEntrada(new OcorrenciasDoGrupo(1, 5, IncluiCandidato: true));

        entrada.Should().Be(new OcorrenciasDoGrupoInput(1, 5, IncluiCandidato: true));
    }

    [Fact(DisplayName = "As ocorrências ausentes na entrada valem sem mínimo, sem máximo e sem o candidato entre os membros")]
    public void Ocorrencias_EntradaAusente_ValeOPadrao() =>
        EntradaDeRegras.Ocorrencias(null).Should().Be(new OcorrenciasDoGrupo(0, null));

    [Fact(DisplayName = "As ocorrências informadas na entrada chegam ao domínio como estão, inclusive as incoerentes, que o domínio recusa")]
    public void Ocorrencias_EntradaIncoerente_ChegaAoDominio() =>
        EntradaDeRegras.Ocorrencias(new OcorrenciasDoGrupoInput(3, 2, IncluiCandidato: true)).Should().Be(new OcorrenciasDoGrupo(3, 2, IncluiCandidato: true));

    [Fact(DisplayName = "A exibição e a obrigatoriedade do grupo vão à entrada com o token e o predicado da obrigatoriedade")]
    public void ParaEntrada_ExibicaoEObrigatoriedade_LevaTokenEPredicado()
    {
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("ANTERIOR", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

        ExibicaoEObrigatoriedadeInput entrada = EntradaDeRegras.ParaEntrada(new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Quando(predicado)));

        entrada.Exibicao.Should().BeNull();
        entrada.Obrigatoriedade!.Tipo.Should().Be("QUANDO");
        entrada.Obrigatoriedade.Predicado.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("ANTERIOR");
    }
}

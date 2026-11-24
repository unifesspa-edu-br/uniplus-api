namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.CenarioDeConformidadeLegal;

/// <summary>
/// Avaliação de <see cref="DesempateDeveIncluir"/> (CA-04): o critério é conferido pelo código do catálogo, não
/// pelo rótulo.
/// </summary>
public sealed class AvaliadorDesempateDeveIncluirTests
{
    [Fact(DisplayName = "CA-04 (DesempateDeveIncluir): aprova por código de catálogo, não pelo rótulo")]
    public void DesempateDeveIncluir_PorCodigoDeCatalogo_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ReferenciaRegra regraDesempate = ReferenciaRegra.De("DESEMPATE_IDOSO", "v1", new string('b', 64));
        processo.DefinirCriteriosDesempate(
            [CriterioDesempate.Criar(1, regraDesempate, new ParametrosDesempateIdoso(IdadeMinima: 60)).Value!]).IsSuccess.Should().BeTrue();

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("DESEMPATE", new DesempateDeveIncluir("DESEMPATE_IDOSO")));

        avaliada.Aprovada.Should().BeTrue();
    }
}

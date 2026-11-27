namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>O item copiado do modelo e recusado na leitura traz também as recusas da forma que não dependem do que foi recusado.</summary>
public sealed class AplicarModeloFormularioComItemRecusadoNaLeituraTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    [Fact(DisplayName = "Item do modelo com tipo recusado e ajuda acima do limite recusa as duas, cada uma uma vez")]
    public async Task Handle_ItemDoModeloRecusadoNaLeitura_TrazAsRecusasDaForma()
    {
        FatoColetadoInput item = Montar("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao)
            .ComRotulo("Quilombola").DoTipo("TIPO_QUE_NAO_EXISTE").ComAjuda(new string('a', 1001)).Construir();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [item]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => e.Error.Code).Should().Contain(ItemFormularioErrorCodes.AjudaTamanho);
        resultado.Errors.Count(static e => e.Error.Code == ItemFormularioErrorCodes.AjudaTamanho).Should().Be(1);
        resultado.Errors.Should().HaveCount(2);
    }
}

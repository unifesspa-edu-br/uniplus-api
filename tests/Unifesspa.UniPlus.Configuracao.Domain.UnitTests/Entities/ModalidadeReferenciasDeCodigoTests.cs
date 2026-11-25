namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

public sealed class ModalidadeReferenciasDeCodigoTests
{
    [Theory]
    [InlineData("composicaoOrigem")]
    [InlineData("remanejamento.destino")]
    [InlineData("remanejamento.par")]
    [InlineData("remanejamento.fallback")]
    public void ValidarCamposDoPayload_ReferenciaComEspacos_RecusaECodigoExatoAceita(string campo)
    {
        Result invalido = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(Declarar(d => ComReferencia(d, campo, " AC ")));
        Result valido = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(Declarar(d => ComReferencia(d, campo, "AC")));

        invalido.Errors.Should().ContainSingle().Which.Field.Should().Be(campo);
        invalido.Errors[0].Error.Code.Should().Be(ModalidadeErrorCodes.CodigoFormatoInvalido);
        invalido.Errors[0].Error.Message.Should().NotContain(" AC ");
        valido.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidarCamposDoPayload_ReferenciasInvalidas_AcumulaRecusas()
    {
        DeclaracaoDaModalidade declaracao = Declarar(d => d with
        {
            ComposicaoOrigem = " AC ",
            RemanejamentoDestino = " AC ",
            RemanejamentoPar = " AC ",
            RemanejamentoFallback = " AC ",
        });

        Result resultado = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(declaracao);

        resultado.Errors.Select(erro => erro.Field).Should().Equal("composicaoOrigem", "remanejamento.destino", "remanejamento.par", "remanejamento.fallback");
    }

    [Fact]
    public void ValidarCampos_ReferenciasEmBranco_ConsideraAusentes()
    {
        DeclaracaoDaModalidade declaracao = Declarar(d => d with
        {
            ComposicaoOrigem = "  ",
            RemanejamentoDestino = "  ",
            RemanejamentoPar = "  ",
            RemanejamentoFallback = "  ",
        });

        Result<CamposResolvidosDaModalidade> resultado = ValidacaoDosCamposDaModalidade.ValidarCampos(declaracao);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ComposicaoOrigem.Should().BeNull();
        resultado.Value.RemanejamentoParametros.TemAlgum.Should().BeFalse();
    }

    private static DeclaracaoDaModalidade ComReferencia(DeclaracaoDaModalidade declaracao, string campo, string valor) => campo switch
    {
        "composicaoOrigem" => declaracao with { ComposicaoOrigem = valor },
        "remanejamento.destino" => declaracao with { RemanejamentoDestino = valor },
        "remanejamento.par" => declaracao with { RemanejamentoPar = valor },
        "remanejamento.fallback" => declaracao with { RemanejamentoFallback = valor },
        _ => throw new ArgumentOutOfRangeException(nameof(campo)),
    };
}

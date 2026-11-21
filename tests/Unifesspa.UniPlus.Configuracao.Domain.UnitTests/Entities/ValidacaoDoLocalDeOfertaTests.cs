namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.MontagemDoLocalDeOferta;

public sealed class ValidacaoDoLocalDeOfertaTests
{
    [Fact(DisplayName = "Tipo fora do enum é recusado no campo tipo")]
    public void ValidarDeclaracao_TipoForaDoEnum_RecusaNoCampoTipo()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { Tipo = (TipoLocalOferta)999 }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("tipo");
        resultado.Errors[0].Error.Code.Should().Be(LocalOfertaErrorCodes.TipoInvalido);
    }

    [Fact(DisplayName = "Tipo Nenhum é recusado com a mensagem do tipo")]
    public void ValidarDeclaracao_TipoNenhum_RecusaComMensagemDoTipo()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { Tipo = TipoLocalOferta.Nenhum }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Error.Message.Should().Be(
            "Tipo de Local de Oferta inválido — use um valor definido em TipoLocalOferta, diferente de Nenhum.");
    }

    [Theory(DisplayName = "Cada campo da cidade ausente é recusado no próprio campo")]
    [InlineData("cidadeCodigoIbge", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("cidadeNome", CidadeReferenciaErrorCodes.NomeObrigatorio)]
    [InlineData("cidadeUf", CidadeReferenciaErrorCodes.UfObrigatoria)]
    public void ValidarDeclaracao_CampoDaCidadeAusente_RecusaNoProprioCampo(string campo, string codigoDoErro)
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => campo switch
        {
            "cidadeCodigoIbge" => d with { CidadeCodigoIbge = null },
            "cidadeNome" => d with { CidadeNome = null },
            _ => d with { CidadeUf = null },
        }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be(campo);
        resultado.Errors[0].Error.Code.Should().Be(codigoDoErro);
    }

    [Fact(DisplayName = "UF incoerente com o prefixo do código IBGE é recusada no campo cidadeUf")]
    public void ValidarDeclaracao_UfIncoerenteComOCodigo_RecusaNoCampoDaUf()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { CidadeUf = "SP" }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("cidadeUf");
        resultado.Errors[0].Error.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Código IBGE malformado é recusado e não confere a coerência do endereço")]
    public void ValidarDeclaracao_CodigoMalformadoComEndereco_RecusaSoACidade()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(
            Declaracao(d => d with { CidadeCodigoIbge = "ABCDEFG", Endereco = Endereco(cidadeCodigoIbge: "1501402") }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("cidadeCodigoIbge");
        resultado.Errors[0].Error.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Endereço de outra cidade é recusado no campo endereco")]
    public void ValidarDeclaracao_EnderecoDeOutraCidade_RecusaNoCampoEndereco()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { Endereco = Endereco(cidadeCodigoIbge: "1501402") }));

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("endereco");
        resultado.Errors[0].Error.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Sem endereço resolvido a coerência não é conferida")]
    public void ValidarDeclaracao_SemEndereco_NaoConfereACoerencia()
    {
        LocalOferta.ValidarDeclaracao(Declaracao()).IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Código e-MEC com mais de 20 caracteres úteis é recusado; com 20 é aceito")]
    [InlineData(21, false)]
    [InlineData(20, true)]
    public void ValidarDeclaracao_TamanhoDoCodigoEmec_RecusaSoAcimaDoLimite(int tamanho, bool aceito)
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { CodigoEmec = $" {new string('E', tamanho)} " }));

        resultado.IsSuccess.Should().Be(aceito);
        resultado.Errors.Should().HaveCount(aceito ? 0 : 1);
    }

    [Fact(DisplayName = "Código e-MEC longo é recusado no campo codigoEmec com a mensagem do limite")]
    public void ValidarDeclaracao_CodigoEmecLongo_RecusaNoCampoComMensagem()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with { CodigoEmec = new string('E', 21) }));

        resultado.Errors[0].Field.Should().Be("codigoEmec");
        resultado.Errors[0].Error.Code.Should().Be(LocalOfertaErrorCodes.CodigoEmecTamanho);
        resultado.Errors[0].Error.Message.Should().Be("Código e-MEC do Local de Oferta deve ter no máximo 20 caracteres.");
    }

    [Fact(DisplayName = "Tipo, cidade e código e-MEC inválidos acumulam as recusas na ordem tipo, cidade, código e-MEC")]
    public void ValidarDeclaracao_VariasViolacoes_AcumulaNaOrdem()
    {
        Result resultado = LocalOferta.ValidarDeclaracao(Declaracao(d => d with
        {
            Tipo = TipoLocalOferta.Nenhum,
            CidadeNome = null,
            CodigoEmec = new string('E', 21),
        }));

        resultado.Errors.Select(erro => erro.Field).Should().Equal("tipo", "cidadeNome", "codigoEmec");
    }
}

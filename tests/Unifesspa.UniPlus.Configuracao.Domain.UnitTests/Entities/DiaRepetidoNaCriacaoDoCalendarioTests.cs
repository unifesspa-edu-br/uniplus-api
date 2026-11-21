namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class DiaRepetidoNaCriacaoDoCalendarioTests
{
    private const string MensagemDeRepeticao = "Data duplicada no dataset (mesma abrangência, município e UF).";
    private static readonly DateOnly Data = new(2027, 1, 1);

    private static DiaNaoUtilCriacao Nacional() => new("NACIONAL", null, null, null, Data, "Confraternização Universal");

    private static DiaNaoUtilCriacao NacionalInvalido() => new("NACIONAL", null, null, null, Data, " ");

    private static void DeveRecusarRepeticaoEm(FieldError recusa, string campo)
    {
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(CalendarioDiasUteisErrorCodes.DataDuplicadaNoDataset);
        recusa.Error.Message.Should().Be(MensagemDeRepeticao);
    }

    [Fact(DisplayName = "Dia repetido na lista é recusado só na posição do repetido")]
    public void Criar_DiaRepetido_RecusaSoNaPosicaoDoRepetido()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar("2027.1", [Nacional(), Nacional()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        DeveRecusarRepeticaoEm(resultado.Errors[0], "diasNaoUteis[1]");
    }

    [Fact(DisplayName = "Três dias iguais geram recusa na segunda e na terceira posição")]
    public void Criar_TresDiasIguais_RecusaSegundoETerceiro()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar("2027.1", [Nacional(), Nacional(), Nacional()]);

        resultado.Errors.Should().HaveCount(2);
        DeveRecusarRepeticaoEm(resultado.Errors[0], "diasNaoUteis[1]");
        DeveRecusarRepeticaoEm(resultado.Errors[1], "diasNaoUteis[2]");
    }

    [Fact(DisplayName = "Dia inválido não faz o igual a ele, mas válido, ser recusado por repetição")]
    public void Criar_PrimeiroInvalidoESegundoValido_RecusaSoOPrimeiro()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar("2027.1", [NacionalInvalido(), Nacional()]);

        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("diasNaoUteis[0].descricao");
        resultado.Errors[0].Error.Code.Should().Be(CalendarioDiasUteisErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Versão inválida e dia repetido se acumulam na ordem versão, depois dia")]
    public void Criar_VersaoInvalidaEDiaRepetido_AcumulaNaOrdem()
    {
        Result<CalendarioDiasUteis> resultado = CalendarioDiasUteis.Criar(" ", [Nacional(), Nacional()]);

        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("versaoDataset");
        resultado.Errors[0].Error.Code.Should().Be(CalendarioDiasUteisErrorCodes.VersaoDatasetObrigatoria);
        DeveRecusarRepeticaoEm(resultado.Errors[1], "diasNaoUteis[1]");
    }
}

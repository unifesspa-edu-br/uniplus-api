namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class FormatoDosCodigosDoFormularioTests
{
    [Theory]
    [InlineData(" A")]
    [InlineData("A ")]
    [InlineData("a")]
    [InlineData("Á")]
    [InlineData("A__B")]
    [InlineData("_A")]
    [InlineData("A_")]
    [InlineData("1A")]
    public void Conferir_CodigoForaDoFormato_Recusa(string codigo)
    {
        Recusas(codigo).Select(static erro => erro.Error.Code).Should().Equal(
            EstruturaFormularioErrorCodes.PaginaCodigoInvalido, GrupoFormularioErrorCodes.CodigoInvalido, TermoFormularioErrorCodes.CodigoFormatoInvalido);
    }

    [Fact]
    public void Conferir_CodigoInvalido_NaoRepeteValorNaMensagem()
    {
        IReadOnlyList<FieldError> recusas = Recusas(" marcador-invalido ");
        recusas.Select(static erro => erro.Error.Code).Should().Equal(
            EstruturaFormularioErrorCodes.PaginaCodigoInvalido, GrupoFormularioErrorCodes.CodigoInvalido, TermoFormularioErrorCodes.CodigoFormatoInvalido);
        recusas.Should().OnlyContain(erro => !erro.Error.Message.Contains("marcador-invalido", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("LEI_12711")]
    [InlineData("ANALISE_DOCUMENTAL")]
    public void Conferir_CodigoValido_Aceita(string codigo) => Recusas(codigo).Should().BeEmpty();

    [Fact]
    public void Conferir_LimiteDeSessentaCaracteres_AceitaSessentaERecusaSessentaEUm()
    {
        Recusas(new string('A', 60)).Should().BeEmpty();
        Recusas(new string('A', 61)).Select(static erro => erro.Error.Code).Should().BeEquivalentTo(
            EstruturaFormularioErrorCodes.PaginaCodigoInvalido, GrupoFormularioErrorCodes.CodigoInvalido, TermoFormularioErrorCodes.CodigoTamanho);
    }

    [Fact]
    public void ValidarFormaBasica_CodigoDoTermo_RecusaNaOrdemPrevista()
    {
        FormaDoTermo.ValidarFormaBasica(" ", 0).Single().Error.Code.Should().Be(TermoFormularioErrorCodes.CodigoObrigatorio);
        FormaDoTermo.ValidarFormaBasica(new string('a', 61), 0).Single().Error.Code.Should().Be(TermoFormularioErrorCodes.CodigoTamanho);
        FormaDoTermo.ValidarFormaBasica("a", 0).Single().Error.Code.Should().Be(TermoFormularioErrorCodes.CodigoFormatoInvalido);
    }

    [Fact]
    public void ConferirUnicidade_CodigosValidosIguais_RecusaSomenteRepeticaoExata()
    {
        var termos = new (string? Codigo, int Ordem)?[] { ("MARCADOR_DISTINTIVO", 0), (" MARCADOR_DISTINTIVO", 1), (" MARCADOR_DISTINTIVO", 2), ("MARCADOR_DISTINTIVO", 3) };
        FieldError erro = FormaDoTermo.ConferirUnicidade(termos).Should().ContainSingle().Subject;
        erro.Field.Should().Be("termos[3].codigo");
        erro.Error.Code.Should().Be(TermoFormularioErrorCodes.CodigoDuplicado);
        erro.Error.Message.Should().NotContain("MARCADOR_DISTINTIVO");
    }

    [Fact]
    public void ValidarFormaBasica_CodigoDoDado_RespeitaLimiteDeCinquenta()
    {
        FormaDoItem.ValidarFormaBasica(new string('A', 50), 0, "Campo").Should().BeEmpty();
        FormaDoItem.ValidarFormaBasica(new string('A', 51), 0, "Campo").Should().ContainSingle()
            .Which.Error.Code.Should().Be(ItemFormularioErrorCodes.FatoCodigoTamanho);
    }

    [Fact]
    public void MesclarItens_PaginaBasicaComEspaco_NaoEhASecaoReservada()
    {
        FatoColetadoInput item = ConjuntoBasicoDaInscricao.Itens[0] with { PaginaCodigo = " DADOS_BASICOS" };
        (_, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens([item], [], ConjuntoBasicoDaInscricao.Itens);
        erros.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.DadoBasicoAlterado);
    }

    [Fact]
    public void Referencia_PaginaBasicaComEspaco_NaoEhASecaoReservada()
    {
        FatoColetadoInput item = ConjuntoBasicoDaInscricao.Itens[0] with
        {
            PaginaCodigo = " DADOS_BASICOS",
            Apresentacao = ConjuntoBasicoDaInscricao.Itens[0].Apresentacao! with { Ajuda = "Ajuda diferente" },
        };
        ConjuntoBasicoDaInscricao.Referencia([item])[0].Apresentacao!.Ajuda.Should().NotBe("Ajuda diferente");
    }

    private static List<FieldError> Recusas(string codigo) =>
    [
        .. FormaDaPagina.Conferir(codigo, 0, TipoPaginaFormulario.Secao, "Título", null, null, false),
        .. FormaDoGrupo.Conferir(codigo, 0, "Grupo", new OcorrenciasDoGrupo(0, 1, false), [("FATO", null, false)], new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre)),
        .. FormaDoTermo.ValidarFormaBasica(codigo, 0),
    ];
}

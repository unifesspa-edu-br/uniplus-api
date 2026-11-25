namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A forma da página aponta o título, a descrição e o aviso recusados no caminho da apresentação.</summary>
public sealed class CaminhoDasRecusasDaPaginaTests
{
    [Fact(DisplayName = "O título ausente é recusado no caminho da apresentação")]
    public void Conferir_TituloAusente_ApontaNaApresentacao()
    {
        List<FieldError> erros = FormaDaPagina.Conferir("DADOS", 0, TipoPaginaFormulario.Secao, titulo: null, descricao: null, aviso: null, temExibicao: false);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("apresentacao.titulo", EstruturaFormularioErrorCodes.PaginaTituloInvalido));
    }

    [Fact(DisplayName = "A descrição e o aviso acima do limite são recusados no caminho da apresentação")]
    public void Conferir_DescricaoEAvisoLongos_ApontamNaApresentacao()
    {
        string longo = new('a', FormaDaPagina.TextoMaxLength + 1);

        List<FieldError> erros = FormaDaPagina.Conferir("DADOS", 0, TipoPaginaFormulario.Secao, "Dados", longo, longo, temExibicao: false);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("apresentacao.descricao", EstruturaFormularioErrorCodes.PaginaTextoTamanho),
            ("apresentacao.aviso", EstruturaFormularioErrorCodes.PaginaTextoTamanho));
    }

    [Fact(DisplayName = "A exibição no bloco de sistema é recusada no caminho da página, fora da apresentação")]
    public void Conferir_ExibicaoNoBloco_ApontaNaPagina()
    {
        List<FieldError> erros = FormaDaPagina.Conferir("REVISAO", 1, TipoPaginaFormulario.Bloco, "Revisão", null, null, temExibicao: true);

        erros.Select(static e => e.Field).Should().Equal("exibicao");
    }
}

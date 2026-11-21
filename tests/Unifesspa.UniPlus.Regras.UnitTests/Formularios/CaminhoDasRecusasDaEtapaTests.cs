namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>A forma da etapa aponta o título, a descrição e o aviso recusados no caminho da apresentação.</summary>
public sealed class CaminhoDasRecusasDaEtapaTests
{
    [Fact(DisplayName = "O título ausente é recusado no caminho da apresentação")]
    public void Conferir_TituloAusente_ApontaNaApresentacao()
    {
        List<FieldError> erros = FormaDaEtapa.Conferir("DADOS", 0, TipoEtapaFormulario.Secao, titulo: null, descricao: null, aviso: null, temExibicao: false);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(("apresentacao.titulo", EstruturaFormularioErrorCodes.EtapaTituloInvalido));
    }

    [Fact(DisplayName = "A descrição e o aviso acima do limite são recusados no caminho da apresentação")]
    public void Conferir_DescricaoEAvisoLongos_ApontamNaApresentacao()
    {
        string longo = new('a', FormaDaEtapa.TextoMaxLength + 1);

        List<FieldError> erros = FormaDaEtapa.Conferir("DADOS", 0, TipoEtapaFormulario.Secao, "Dados", longo, longo, temExibicao: false);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("apresentacao.descricao", EstruturaFormularioErrorCodes.EtapaTextoTamanho),
            ("apresentacao.aviso", EstruturaFormularioErrorCodes.EtapaTextoTamanho));
    }

    [Fact(DisplayName = "A exibição no bloco de sistema é recusada no caminho da etapa, fora da apresentação")]
    public void Conferir_ExibicaoNoBloco_ApontaNaEtapa()
    {
        List<FieldError> erros = FormaDaEtapa.Conferir("REVISAO", 1, TipoEtapaFormulario.Bloco, "Revisão", null, null, temExibicao: true);

        erros.Select(static e => e.Field).Should().Equal("exibicao");
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// A cópia do modelo conforme a finalidade do formulário: o par inscrição e habilitação decide onde o fato
/// fica, e o modelo de inscrição é copiado com a seção dos dados básicos em primeiro.
/// </summary>
public sealed class AplicarModeloFormularioFinalidadeEConjuntoBasicoTests
{
    private readonly CenarioDaAplicacaoDoModeloDeFormulario _cenario = new();

    private static FatoColetadoInput ItemQuilombolaImpedidoParaPretos(string mensagem) =>
        Montar("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao).ComImpedimento(
            new ImpedimentoInput(
                [[
                    new CondicaoPrecondicaoInput("QUILOMBOLA", "IGUAL", JsonSerializer.SerializeToElement(true)),
                    new CondicaoPrecondicaoInput("COR_RACA", "IGUAL", JsonSerializer.SerializeToElement("PRETA")),
                ]],
                mensagem)).Construir();

    [Fact(DisplayName = "Fora da inscrição, o fato que a inscrição coleta fica nela e sai da cópia")]
    public async Task Handle_HabilitacaoComFatoDaInscricao_MantemNaInscricao()
    {
        _cenario.Processo.DefinirItens([FatoBooleanoJaColetado("QUILOMBOLA", "Quilombola")]).IsSuccess.Should().BeTrue();
        ModeloFormularioView modelo = Modelo(DeOutraFaseObrigatoria, [Item("QUILOMBOLA", 0), Item("CERTIFICADO", 1)]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosMantidosNaInscricao.Should().Equal("QUILOMBOLA");
        _cenario.Processo.FatosColetados.Should().Contain(static f => f.FatoCodigo == "QUILOMBOLA" && f.Finalidade == FinalidadeFormulario.Inscricao)
            .And.Contain(static f => f.FatoCodigo == "CERTIFICADO" && f.Finalidade == FinalidadeFormulario.Habilitacao);
    }

    [Fact(DisplayName = "Na inscrição, o fato de outra finalidade é trazido e relatado")]
    public async Task Handle_InscricaoComFatoDeOutraFinalidade_TrazERelata()
    {
        _cenario.Processo.DefinirItens([FatoBooleanoJaColetado("CERTIFICADO", "Certificado")], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("CERTIFICADO", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosTrazidosParaAInscricao.Should().Equal("CERTIFICADO");
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
    }

    [Fact(DisplayName = "O modelo de inscrição sem a seção dos dados básicos é copiado com ela em primeiro")]
    public async Task Handle_ModeloDeInscricaoSemASecao_CopiaComASecao()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Paginas.MinBy(static e => e.Ordem)!.Codigo.Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        _cenario.Processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Ordem.Should().Be(FormularioDeTeste.PrimeiraOrdemDeInscricao);
    }

    [Fact(DisplayName = "O modelo de inscrição gravado com a seção dos dados básicos e um item que cita um deles é aplicado")]
    public async Task Handle_ModeloDeInscricaoComASecao_CitaDadoBasico()
    {
        FatoColetadoInput porCor = ItemQueCita(Item("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao), "COR_RACA", "IGUAL", "PRETA");

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [.. ConjuntoBasicoDaInscricao.Itens, porCor]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.FatosCitados.Should().Equal("COR_RACA");
    }

    [Theory(DisplayName = "A finalidade com que o modelo é copiado vem da declaração dele: serve à inscrição, é inscrição; senão, obrigatório é habilitação e facultativo é isenção")]
    [InlineData(true, true, "INSCRICAO")]
    [InlineData(true, false, "INSCRICAO")]
    [InlineData(false, true, "HABILITACAO")]
    [InlineData(false, false, "ISENCAO_TAXA")]
    public async Task Handle_ModeloDeclarado_CopiaComAFinalidadeDaDeclaracao(bool serveAFaseDeInscricao, bool obrigatorio, string finalidadeEsperada)
    {
        DeclaracaoDoFormulario declaracao = new(serveAFaseDeInscricao, obrigatorio);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(declaracao, [Item("CERTIFICADO", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Finalidade.Codigo.Should().Be(finalidadeEsperada);
    }

    [Fact(DisplayName = "O modelo facultativo que serve à fase de inscrição é copiado com a seção dos dados básicos em primeiro")]
    public async Task Handle_ModeloFacultativoQueServeAInscricao_CopiaComASecao()
    {
        DeclaracaoDoFormulario declaracao = new(ServeAFaseDeInscricao: true, Obrigatorio: false);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(declaracao, [Item("QUILOMBOLA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.FormularioDe(FinalidadeFormulario.Inscricao)!.Paginas.MinBy(static e => e.Ordem)!.Codigo.Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
    }

    [Fact(DisplayName = "O impedimento do item do modelo é copiado, e só a resposta anterior que ele cita é dependência do campo")]
    public async Task Handle_ItemComImpedimento_CopiaOImpedimento()
    {
        const string Mensagem = "Quem é quilombola e se declarou preto concorre pelo processo próprio.";
        FatoColetadoInput impedido = ItemQuilombolaImpedidoParaPretos(Mensagem);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [.. ConjuntoBasicoDaInscricao.Itens, impedido]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FatoColetado copiado = _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Subject;
        copiado.Impedimento!.Mensagem.Should().Be(Mensagem);
        copiado.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "O modelo de inscrição que altera um dado básico é recusado na posição do item no modelo")]
    public async Task Handle_ModeloDeInscricaoComDadoBasicoAlterado_Recusa()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(DaInscricao, [Item("QUILOMBOLA", 0), Item("DATA_NASCIMENTO", 1, "DATA")]));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.itens[1]",
            Error = new { Code = EstruturaFormularioErrorCodes.DadoBasicoAlterado },
        });
    }
}

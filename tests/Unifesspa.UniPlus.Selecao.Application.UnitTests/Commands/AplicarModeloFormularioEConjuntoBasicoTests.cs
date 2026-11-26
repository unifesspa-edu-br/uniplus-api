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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaAplicacaoDoModeloDeFormulario;

/// <summary>
/// A cópia do modelo conforme o formulário: o par inscrição e habilitação decide onde o fato
/// fica, e o modelo de inscrição é copiado com a seção dos dados básicos em primeiro.
/// </summary>
public sealed class AplicarModeloFormularioEConjuntoBasicoTests
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
        ModeloFormularioView modelo = Modelo(PapelDoFormularioDeTeste.Habilitacao, [Item("QUILOMBOLA", 0), Item("CERTIFICADO", 1)]);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(PapelDoFormularioDeTeste.Habilitacao, modelo);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosMantidosNaInscricao.Should().Equal("QUILOMBOLA");
        Guid inscricao = _cenario.Processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id;
        Guid habilitacao = _cenario.Processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.Id;
        _cenario.Processo.FatosColetados.Should().Contain(f => f.FatoCodigo == "QUILOMBOLA" && f.FormularioId == inscricao)
            .And.Contain(f => f.FatoCodigo == "CERTIFICADO" && f.FormularioId == habilitacao);
    }

    [Fact(DisplayName = "Na inscrição, o fato de outro formulário é trazido e relatado")]
    public async Task Handle_InscricaoComFatoDeOutroFormulario_TrazERelata()
    {
        _cenario.Processo.DefinirItens([FatoBooleanoJaColetado("CERTIFICADO", "Certificado")], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("CERTIFICADO", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosTrazidosParaAInscricao.Should().Equal("CERTIFICADO");
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.FormularioId.Should().Be(_cenario.Processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id);
    }

    [Fact(DisplayName = "Na inscrição, o fato que outro formulário da inscrição coleta fica nele e não é trazido")]
    public async Task Handle_FatoEmOutroFormularioDaInscricao_MantemENaoTraz()
    {
        CodigoDoFormulario complementar = DeclararFormularioComplementarQueColeta("QUILOMBOLA", "Quilombola");

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.FatosTrazidosParaAInscricao.Should().BeEmpty();
        resultado.Value.FatosMantidosNaInscricao.Should().Equal("QUILOMBOLA");
        _cenario.Processo.FatosColetados.Where(static f => f.FatoCodigo == "QUILOMBOLA").Should().ContainSingle()
            .Which.FormularioId.Should().Be(_cenario.Processo.Declaracao().Coleta.FormularioDe(complementar)!.Id);
    }

    private CodigoDoFormulario DeclararFormularioComplementarQueColeta(string fato, string rotulo)
    {
        FormularioDeTeste.GarantirFormulario(_cenario.Processo, PapelDoFormularioDeTeste.Inscricao);
        FormularioProcesso inscricao = _cenario.Processo.Declaracao().Coleta.FormularioDe(CodigoDoFormulario.Criar(FormularioDeTeste.CodigoDaInscricao).Value!)!;
        CodigoDoFormulario complementar = CodigoDoFormulario.Criar("DADOS_COMPLEMENTARES").Value!;
        _cenario.Processo.DefinirFormulario(
            new FormularioDeclarado(complementar, inscricao.FaseId, Obrigatorio: true), null, [.. PaginasDeDados()]).IsSuccess.Should().BeTrue();
        _cenario.Processo.DefinirFatosColetados(
            complementar, [FatoColetadoDeTeste.Como(FatoBooleanoJaColetado(fato, rotulo)).NaSecao(FormularioDeTeste.Secao).Construir()])
            .IsSuccess.Should().BeTrue();
        return complementar;
    }

    private static IEnumerable<PaginaDoFormulario> PaginasDeDados() =>
        FormularioDeTeste.Paginas(PapelDoFormularioDeTeste.Habilitacao);

    [Fact(DisplayName = "O modelo de inscrição sem a seção dos dados básicos é copiado com ela em primeiro")]
    public async Task Handle_ModeloDeInscricaoSemASecao_CopiaComASecao()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0)]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Paginas.MinBy(static e => e.Ordem)!.Codigo.Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
        _cenario.Processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.Ordem.Should().Be(FormularioDeTeste.PrimeiraOrdemDeInscricao);
    }

    [Fact(DisplayName = "O modelo de inscrição gravado com a seção dos dados básicos e um item que cita um deles é aplicado")]
    public async Task Handle_ModeloDeInscricaoComASecao_CitaDadoBasico()
    {
        FatoColetadoInput porCor = ItemQueCita(Item("QUILOMBOLA", FormularioDeTeste.PrimeiraOrdemDeInscricao), "COR_RACA", "IGUAL", "PRETA");

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [.. ConjuntoBasicoDaInscricao.Itens, porCor]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Which.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "O impedimento do item do modelo é copiado, e só a resposta anterior que ele cita é dependência do campo")]
    public async Task Handle_ItemComImpedimento_CopiaOImpedimento()
    {
        const string Mensagem = "Quem é quilombola e se declarou preto concorre pelo processo próprio.";
        FatoColetadoInput impedido = ItemQuilombolaImpedidoParaPretos(Mensagem);

        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [.. ConjuntoBasicoDaInscricao.Itens, impedido]));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FatoColetado copiado = _cenario.Processo.FatosColetados.ForaDoConjuntoBasico().Should().ContainSingle().Subject;
        copiado.Impedimento!.Mensagem.Should().Be(Mensagem);
        copiado.FatosCitados.Should().Equal("COR_RACA");
    }

    [Fact(DisplayName = "O modelo de inscrição que altera um dado básico é recusado na posição do item no modelo")]
    public async Task Handle_ModeloDeInscricaoComDadoBasicoAlterado_Recusa()
    {
        Result<AplicacaoDeModeloDto> resultado = await _cenario.AplicarAsync(Modelo(PapelDoFormularioDeTeste.Inscricao, [Item("QUILOMBOLA", 0), Item("DATA_NASCIMENTO", 1, "DATA")]));

        resultado.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "modelo.itens[1]",
            Error = new { Code = EstruturaFormularioErrorCodes.DadoBasicoAlterado },
        });
    }
}

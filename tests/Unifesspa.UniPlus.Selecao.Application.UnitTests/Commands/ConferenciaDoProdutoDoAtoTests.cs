namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// A mensagem da recusa do papel desconhecido é a que a página do código no portal descreve: lista os papéis
/// aceitos e não repete o valor recusado.
/// </summary>
public sealed class ConferenciaDoProdutoDoAtoTests
{
    private const string PapelRecusado = "TESTE";
    private const string NomeFicticioDaEtapa = "Etapa fictícia zeta";

    [Fact]
    public async Task Handle_PapelDesconhecidoNaFase_ListaOsPapeisAceitosSemRepetirOValor()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho();
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDaClassificacao(PapelRecusado)] };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        ConferirRecusaDoPapel(resultado, "ProdutoDaFase.PapelDesconhecido");
    }

    [Fact]
    public async Task Handle_PapelDesconhecidoNaEtapa_ListaOsPapeisAceitosSemRepetirOValor()
    {
        EtapaProcessoInput etapa = EtapaInput(NomeFicticioDaEtapa, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1) with
        {
            Produtos = [new ProdutoDaEtapaInput(AtoDaClassificacao, PapelRecusado)],
        };

        Result resultado = await ExecutarSobre(ProcessoSemEtapas(), null, etapa);

        ConferirRecusaDoPapel(resultado, "ProdutoDaEtapa.PapelDesconhecido");
        resultado.Errors.Single(e => e.Error.Code == "ProdutoDaEtapa.PapelDesconhecido").Error.Message
            .Should().NotContainEquivalentOf("zeta");
    }

    [Fact]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_RecusaCitandoONomeDoTipo()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDoComunicado());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { Produtos = [ProdutoDoComunicado("DEFINITIVO")] };

        Result resultado = await cenario.DefinirFasesAsync(fase);

        DomainError erro = resultado.Errors.Single(e => e.Error.Code == "ProdutoDaFase.PapelEmAtoQueNaoEhResultado").Error;
        erro.Message.Should().Be("O tipo de ato 'Comunicado' não é resultado no catálogo — só resultado recebe papel preliminar ou definitivo.");
        erro.Message.Should().NotContain("COMUNICADO");
    }

    private static void ConferirRecusaDoPapel(Result resultado, string codigo)
    {
        DomainError erro = resultado.Errors.Single(e => e.Error.Code == codigo).Error;

        erro.Message.Should().Contain("'PRELIMINAR'").And.Contain("'DEFINITIVO'").And.Contain("ou nenhum");
        erro.Message.Should().NotContain(PapelRecusado);
    }
}

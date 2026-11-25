namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class CondicaoDeHomologacaoComDefeitoNaGravacaoTests
{
    private const string Campo = "fases[0].regraDeHomologacao.alternativas[0].condicoes[0]";

    [Fact]
    public async Task Handle_CondicaoEEnquadramentoInvalidos_AcumulaCausasNaOrdem()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        CondicaoPrecondicaoInput defeito = new(CondicaoDeHomologacaoComDefeitoDeTeste.Dado, "ALGUM_DE", JsonSerializer.SerializeToElement(CondicaoDeHomologacaoComDefeitoDeTeste.Valor));
        CondicaoDeHomologacaoInput condicao = new("RESULTADO_NO_RETORNO_DO_INEP", CondicaoDeHomologacaoComDefeitoDeTeste.Edicao, CondicaoDeHomologacaoComDefeitoDeTeste.Motivo, [[defeito]]);
        RegraDeHomologacaoInput regra = new(AtoDaClassificacao, [new([condicao])]);
        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { RegraDeHomologacao = regra });
        resultado.Errors.Select(erro => (erro.Error.Code, erro.Field)).Should().Equal(
            CondicaoDeHomologacaoComDefeitoDeTeste.Causas.Select(causa => (causa.Codigo, (string?)(Campo + "." + causa.Campo))));
    }
}

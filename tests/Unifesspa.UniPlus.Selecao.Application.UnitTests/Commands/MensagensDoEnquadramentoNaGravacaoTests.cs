namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class MensagensDoEnquadramentoNaGravacaoTests
{
    [Theory]
    [InlineData("", "IGUAL", "CondicaoDnf.FatoObrigatorio")]
    [InlineData("DADO_FICTICIO", "DESCONHECIDO", "CondicaoDnf.OperadorInvalido")]
    [InlineData("DADO_FICTICIO", "ALGUM_DE", "CondicaoDnf.FormaIncoerenteComOperador")]
    public async Task Handle_DefeitoDeForma_UsaMensagemDoDominio(string dado, string operador, string codigo)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        CondicaoPrecondicaoInput defeito = new(dado, operador, JsonSerializer.SerializeToElement("opção fictícia"));
        CondicaoDeHomologacaoInput condicao = new("RESULTADO_NO_RETORNO_DO_INEP", 2025, null, [[defeito]]);
        RegraDeHomologacaoInput regra = new(AtoDaClassificacao, [new([condicao])]);
        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { RegraDeHomologacao = regra });
        resultado.Errors.Should().ContainSingle().Which.Error.Should().Be(new DomainError(codigo, MensagensDoEnquadramento.DoDefeitoDeForma(codigo)!));
    }
}

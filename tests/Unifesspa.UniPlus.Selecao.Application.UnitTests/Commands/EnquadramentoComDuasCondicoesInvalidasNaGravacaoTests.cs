namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class EnquadramentoComDuasCondicoesInvalidasNaGravacaoTests
{
    [Fact]
    public async Task Handle_DuasCondicoesInvalidasNoEnquadramento_AcumulaCausasNaOrdem()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        JsonElement clausula = CondicaoDeHomologacaoComDefeitoDeTeste.Enquadramento[0];
        CondicaoPrecondicaoInput[] defeitos = [.. clausula.EnumerateArray().Select(static item => new CondicaoPrecondicaoInput(
            item.GetProperty("codigoDoDado").GetString()!, item.GetProperty("operador").GetString(), item.GetProperty("valor")))];
        CondicaoDeHomologacaoInput condicao = new("RESULTADO_NO_RETORNO_DO_INEP", 2025, null, [defeitos]);
        RegraDeHomologacaoInput regra = new(AtoDaClassificacao, [new([condicao])]);
        Result resultado = await cenario.DefinirFasesAsync(cenario.CadastrarFase(FaseCanonicaDoResultadoFinal()) with { RegraDeHomologacao = regra });
        const string campo = "fases[0].regraDeHomologacao.alternativas[0].condicoes[0].";
        resultado.Errors.Select(erro => (erro.Error.Code, erro.Field)).Should().Equal(
            CondicaoDeHomologacaoComDefeitoDeTeste.CausasDoEnquadramento.Select(causa => (causa.Codigo, (string?)(campo + causa.Campo))));
        resultado.Errors.Select(erro => erro.Error.Message).Should().Equal(Enumerable.Repeat("O formato do valor não corresponde ao operador informado.", 2));
    }
}

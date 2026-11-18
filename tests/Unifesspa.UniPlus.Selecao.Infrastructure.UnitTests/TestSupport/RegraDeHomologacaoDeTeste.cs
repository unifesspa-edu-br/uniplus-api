namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

internal static class RegraDeHomologacaoDeTeste
{
    internal const string TipoDoRetornoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";

    internal static RegraDeHomologacaoDasInscricoes Regra(string? resultadoAtoCodigo, params AlternativaDeCondicoes[] alternativas) =>
        RegraDeHomologacaoDasInscricoes.Declarar(resultadoAtoCodigo, alternativas);

    internal static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Declarar(condicoes).Value!;

    internal static CondicaoDeHomologacao Condicao(int edicao, string? motivo, PredicadoDnf? enquadramento) =>
        CondicaoDeHomologacao.Declarar(TipoDoRetornoDoInep, edicao, motivo, enquadramento).Value!;

    internal static PredicadoDnf EnquadramentoDaModalidade(params string[] modalidades) => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [.. modalidades.Select(static (modalidade, clausula) =>
            (clausula, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement(modalidade)).Value!))]).Value!;
}

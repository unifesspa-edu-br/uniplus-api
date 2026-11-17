namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

internal static class CondicoesDeHomologacaoParaTestes
{
    internal const string CodigoDoRetornoDoInep = "RESULTADO_NO_RETORNO_DO_INEP";

    internal static CondicaoDeHomologacao ConfirmacaoDoInep(int edicao, string motivo) =>
        CondicaoDeHomologacao.Declarar(CodigoDoRetornoDoInep, edicao, motivo, enquadramento: null).Value!;

    internal static CondicaoDeHomologacao ConfirmacaoDoInepSoParaAmpla(int edicao, string motivo) =>
        CondicaoDeHomologacao.Declarar(
            CodigoDoRetornoDoInep,
            edicao,
            motivo,
            PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AMPLA")).Value!)]).Value!).Value!;

    internal static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Declarar(condicoes).Value!;
}

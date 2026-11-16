namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

internal static class CondicoesDeHomologacaoParaTestes
{
    internal static CondicaoDeHomologacao ConfirmacaoDoInep(int edicao, string motivo) =>
        CondicaoDeHomologacao.Criar(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, edicao, motivo, enquadramento: null).Value!;

    internal static CondicaoDeHomologacao ConfirmacaoDoInepSoParaAmpla(int edicao, string motivo) =>
        CondicaoDeHomologacao.Criar(
            TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep,
            edicao,
            motivo,
            PredicadoDnf.CriarDeCondicoesAgrupadas(
                [(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AMPLA")).Value!)]).Value!).Value!;

    internal static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Criar(condicoes).Value!;
}

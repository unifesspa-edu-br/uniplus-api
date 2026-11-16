namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

internal static class CondicoesDeHomologacaoParaTestes
{
    internal static CondicaoDeHomologacao ConfirmacaoDoInep(int edicao, string motivo) =>
        CondicaoDeHomologacao.Criar(TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, edicao, motivo, enquadramento: null).Value!;

    internal static AlternativaDeCondicoes Alternativa(params CondicaoDeHomologacao[] condicoes) =>
        AlternativaDeCondicoes.Criar(condicoes).Value!;
}

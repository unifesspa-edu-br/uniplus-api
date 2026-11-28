namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>As recusas de cadastro com que a resolução de uma distribuição de vagas responde ao pedido.</summary>
internal static class RecusasDaResolucaoDeVagas
{
    internal static DomainError RegraDeDistribuicaoNaoEncontrada() => new(
        "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada",
        "A regra de distribuição na versão informada não foi encontrada no catálogo de regras.");

    internal static DomainError RegraDeDistribuicaoDeTipoInvalido() => new(
        "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido",
        "A regra informada não é uma regra de distribuição de vagas.");

    internal static DomainError RegraDeAjusteNaoEncontrada() => new(
        "ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada",
        "A regra de ajuste na versão informada não foi encontrada no catálogo de regras.");

    internal static DomainError RegraDeAjusteDeTipoInvalido() => new(
        "ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido",
        "A regra informada não é uma regra de ajuste da distribuição de vagas.");

    internal static DomainError QuadroModalidadeNaoSelecionada(string? citacaoDaModalidade) => new(
        "ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada",
        string.IsNullOrWhiteSpace(citacaoDaModalidade)
            ? "A modalidade do quadro deve estar entre as modalidades selecionadas da oferta."
            : $"A modalidade '{citacaoDaModalidade}' do quadro deve estar entre as modalidades selecionadas da oferta.");
}

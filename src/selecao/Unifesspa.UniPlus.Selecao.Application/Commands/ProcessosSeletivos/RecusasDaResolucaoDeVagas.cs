namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>As recusas de cadastro com que a resolução de uma distribuição de vagas responde ao pedido.</summary>
internal static class RecusasDaResolucaoDeVagas
{
    internal static DomainError OfertaNaoEncontrada(Guid ofertaCursoId) => new(
        "ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada",
        $"Oferta de curso {ofertaCursoId} não encontrada ou não está mais viva.");

    internal static DomainError RegraDeDistribuicaoNaoEncontrada(string codigo, string versao) => new(
        "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada",
        $"Regra de distribuição {codigo}/{versao} não encontrada no catálogo de regras.");

    internal static DomainError RegraDeDistribuicaoDeTipoInvalido(string codigo, string versao) => new(
        "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido",
        $"A regra {codigo}/{versao} não é do tipo regra_distribuicao_vagas.");

    internal static DomainError RegraDeAjusteNaoEncontrada(string codigo, string? versao) => new(
        "ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada",
        $"Regra de ajuste {codigo}/{versao} não encontrada no catálogo de regras.");

    internal static DomainError RegraDeAjusteDeTipoInvalido(string codigo, string? versao) => new(
        "ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido",
        $"A regra {codigo}/{versao} não é do tipo regra_ajuste_distribuicao_vagas.");

    internal static DomainError ReferenciaDemograficaNaoEncontrada(Guid referenciaId) => new(
        "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada",
        $"Referência de reserva demográfica {referenciaId} não encontrada ou não está mais viva.");

    internal static DomainError ModalidadeNaoEncontrada(Guid modalidadeId) => new(
        "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada",
        $"Modalidade {modalidadeId} não encontrada ou não está mais viva.");

    internal static DomainError QuadroModalidadeNaoSelecionada(Guid modalidadeId) => new(
        "ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada",
        $"A modalidade {modalidadeId} no quadro não está entre as modalidades selecionadas desta oferta.");
}

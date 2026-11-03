namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A guarda de mutação da configuração (ADR-0110 D4/D5/D9): quem pode editar e com que
/// precondição. Só olha o status do processo e a sessão editorial, nunca o conteúdo da configuração.
/// </summary>
internal static class GuardaDeEdicao
{
    /// <summary>
    /// O que impede mutar a configuração, ou nulo quando a mutação pode prosseguir. É uma lista de
    /// permissão: só o rascunho e o publicado com sessão aberta editam, e todo status futuro nasce
    /// bloqueado. A precondição só é exigida sob sessão, porque em rascunho não há ETag a fornecer.
    /// </summary>
    /// <remarks>
    /// Os handlers chamam a guarda logo depois de resolver o 404 e antes de validar o payload,
    /// porque a precondição precede a regra de negócio. Cada operação a chama de novo por dentro,
    /// para que um handler futuro que esqueça de antecipá-la não a contorne.
    /// </remarks>
    internal static DomainError? MutacaoBloqueada(StatusProcesso status, RascunhoRetificacao? sessao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        if (status == StatusProcesso.Publicado && sessao is null)
        {
            return new DomainError(
                "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
                "Processo publicado não aceita mutação direta da configuração — utilize a retificação.");
        }

        if (!MutacaoPermitida(status, sessao))
        {
            return new DomainError(
                "ProcessoSeletivo.MutacaoForaDeEstadoEditavel",
                $"Um processo em {status} não aceita mutação da configuração.");
        }

        return sessao?.ConferirPrecondicao(precondicao);
    }

    /// <summary>
    /// O que impede encerrar a sessão editorial (fechar ou descartar), ou nulo quando ela pode ser
    /// encerrada. A inexistência da sessão (409) precede a precondição (428/412): responder 412 para
    /// um rascunho que não existe mandaria o cliente recarregar um ETag inexistente. Aqui o If-Match
    /// é sempre obrigatório, porque as duas rotas existem para a sessão.
    /// </summary>
    internal static DomainError? PendenciaDaSessao(StatusProcesso status, RascunhoRetificacao? sessao, PrecondicaoIfMatch precondicao)
    {
        ArgumentNullException.ThrowIfNull(precondicao);

        return sessao is null ? RetificacaoNaoAberta() : MutacaoBloqueada(status, sessao, precondicao);
    }

    internal static DomainError RetificacaoJaAberta() => new(
        "RascunhoRetificacao.JaAberta",
        "Já existe uma retificação em curso neste processo — feche-a ou descarte-a antes de abrir outra.");

    internal static DomainError RetificacaoNaoAberta() => new(
        "RascunhoRetificacao.NaoAberta",
        "Não há retificação em curso neste processo.");

    private static bool MutacaoPermitida(StatusProcesso status, RascunhoRetificacao? sessao) =>
        status == StatusProcesso.Rascunho
        || (status == StatusProcesso.Publicado && sessao is not null);
}

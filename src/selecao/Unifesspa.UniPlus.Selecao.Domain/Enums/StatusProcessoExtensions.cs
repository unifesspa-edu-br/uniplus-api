namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quando o status deixa editar a configuração por dimensão. É uma lista de permissão: só o rascunho edita,
/// e todo status futuro nasce bloqueado. O processo publicado só muda por retificação.
/// </summary>
internal static class StatusProcessoExtensions
{
    /// <summary>O que impede editar a configuração por dimensão, ou nulo: só o rascunho edita.</summary>
    internal static DomainError? RecusaDeEdicaoForaDoRascunho(this StatusProcesso status) => status switch
    {
        StatusProcesso.Rascunho => null,
        StatusProcesso.Publicado => new DomainError(
            "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "Processo publicado não aceita mutação direta da configuração — utilize a retificação."),
        _ => new DomainError(
            "ProcessoSeletivo.MutacaoForaDeEstadoEditavel",
            $"Um processo em {status} não aceita mutação da configuração."),
    };
}

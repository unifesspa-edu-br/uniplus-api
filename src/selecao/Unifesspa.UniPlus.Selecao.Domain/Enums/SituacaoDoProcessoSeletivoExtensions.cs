namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quando a situação deixa editar a configuração por dimensão. É uma lista de permissão: só o rascunho edita,
/// e toda situação futura nasce bloqueada. O processo publicado só muda por retificação.
/// </summary>
internal static class SituacaoDoProcessoSeletivoExtensions
{
    /// <summary>O que impede editar a configuração por dimensão, ou nulo: só o rascunho edita.</summary>
    internal static DomainError? RecusaDeEdicaoForaDoRascunho(this SituacaoDoProcessoSeletivo situacao) => situacao switch
    {
        SituacaoDoProcessoSeletivo.Rascunho => null,
        SituacaoDoProcessoSeletivo.Publicado => new DomainError(
            "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "Processo publicado não aceita mutação direta da configuração — utilize a retificação."),
        _ => new DomainError(
            "ProcessoSeletivo.MutacaoForaDeEstadoEditavel",
            $"Um processo em {situacao.Rotulo()} não aceita mutação da configuração."),
    };
}

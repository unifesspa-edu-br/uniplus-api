namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quando o status deixa editar a configuração (ADR-0110 D4/D5/D9). É uma lista de permissão: só o
/// rascunho e o publicado com sessão aberta editam, e todo status futuro nasce bloqueado.
/// </summary>
internal static class StatusProcessoExtensions
{
    /// <summary>O que impede editar sem sessão editorial aberta, ou nulo: só o rascunho edita.</summary>
    internal static DomainError? RecusaDeEdicaoSemSessao(this StatusProcesso status) => status switch
    {
        StatusProcesso.Rascunho => null,
        StatusProcesso.Publicado => new DomainError(
            "ProcessoSeletivo.MutacaoPosPublicacaoBloqueada",
            "Processo publicado não aceita mutação direta da configuração — utilize a retificação."),
        _ => ForaDeEstadoEditavel(status),
    };

    /// <summary>O que impede editar com a sessão aberta, ou nulo: rascunho e publicado editam.</summary>
    internal static DomainError? RecusaDeEdicaoSobSessao(this StatusProcesso status) =>
        status is StatusProcesso.Rascunho or StatusProcesso.Publicado ? null : ForaDeEstadoEditavel(status);

    private static DomainError ForaDeEstadoEditavel(StatusProcesso status) => new(
        "ProcessoSeletivo.MutacaoForaDeEstadoEditavel",
        $"Um processo em {status} não aceita mutação da configuração.");
}

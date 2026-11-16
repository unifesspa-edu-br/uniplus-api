namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os anexos do pedido: o documento declarado na criação, como o de designação que o perfil de governança exige.</summary>
public sealed partial class PedidoDeConcessao
{
    /// <summary>
    /// Cria o anexo e devolve-o para o handler emitir a URL de envio. Só antes da primeira decisão e, no pedido protegido,
    /// só o documento declarado na criação, enquanto o pedido o aguarda.
    /// </summary>
    public Result<AnexoDoPedido> NovoAnexo(long tamanhoDoPdf)
    {
        DomainError? recusa = PodeSerAlteradoPeloSolicitante
            ? Declaracao.RecusaDoDocumento(Termo, Andamento.Situacao)
            : SituacaoInvalida("O anexo só se envia antes da primeira decisão.");
        return recusa is null ? _anexos.Novo(Id, tamanhoDoPdf) : Result<AnexoDoPedido>.Failure(recusa);
    }

    /// <summary>Põe o anexo em verificação, com o ETag que o PUT devolveu. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> ConfirmarAnexo(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se confirma antes da primeira decisão."));
        }

        return _anexos.Confirmar(anexoId, etag, instante);
    }

    /// <summary>
    /// Libera o pedido que espera o documento declarado quando já há anexo verificado: o pedido segue para o aceite do
    /// beneficiário, se ele é outra pessoa ou se o pedido tem termo, ou para o gestor. Em qualquer outra situação, ou sem o anexo
    /// verificado, nada muda e devolve falso.
    /// </summary>
    public bool LiberarAoVerificarDocumento()
    {
        if (Andamento.Situacao != SituacaoDoPedido.AguardandoDocumento || !_anexos.TemVerificado)
        {
            return false;
        }

        Andamento = Andamento.Em(EtapaSeguinteAoDocumento());
        return true;
    }
}

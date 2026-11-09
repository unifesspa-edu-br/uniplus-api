namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os anexos do pedido: o documento de designação que o perfil de governança exige.</summary>
public sealed partial class PedidoDeConcessao
{
    public bool TemAnexoVerificado => _anexos.Exists(a => a.Situacao == SituacaoDoAnexo.Verificado);

    /// <summary>Cria o anexo e devolve-o para o handler emitir a URL de envio. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> NovoAnexo(long tamanhoDoPdf)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se envia antes da primeira decisão."));
        }

        Result<AnexoDoPedido> anexo = AnexoDoPedido.Criar(Id, tamanhoDoPdf);
        if (anexo.IsSuccess)
        {
            _anexos.Add(anexo.Value!);
        }

        return anexo;
    }

    /// <summary>Põe o anexo em verificação, com o ETag que o PUT devolveu. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> ConfirmarAnexo(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se confirma antes da primeira decisão."));
        }

        AnexoDoPedido? anexo = _anexos.Find(a => a.Id == anexoId);

        return anexo is null
            ? Result<AnexoDoPedido>.Failure(new DomainError(PedidoErrorCodes.NaoEncontrado, "Anexo do pedido não encontrado."))
            : ConfirmarEnvio(anexo, etag, instante);
    }

    /// <summary>
    /// Libera o pedido que espera o documento de designação quando já há anexo verificado: o pedido segue para o
    /// aceite do beneficiário, se ele é outra pessoa, ou para o gestor. Em qualquer outra situação, ou sem o anexo
    /// verificado, nada muda e devolve falso.
    /// </summary>
    public bool LiberarAoVerificarDocumento()
    {
        if (Situacao != SituacaoDoPedido.AguardandoDocumento || !TemAnexoVerificado)
        {
            return false;
        }

        Situacao = EtapaSeguinteAoDocumento(Beneficiario is not null);
        return true;
    }

    private static Result<AnexoDoPedido> ConfirmarEnvio(AnexoDoPedido anexo, string? etag, DateTimeOffset instante)
    {
        Result confirmado = anexo.ConfirmarEnvio(etag, instante);

        return confirmado.IsSuccess
            ? Result<AnexoDoPedido>.Success(anexo)
            : Result<AnexoDoPedido>.ValidationFailure(confirmado.Errors);
    }
}

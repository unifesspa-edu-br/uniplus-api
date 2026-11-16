namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using System.Collections.ObjectModel;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os anexos de um pedido e o que se faz com eles: criar o documento declarado, pô-lo em verificação e saber se algum já foi
/// verificado. É o campo que o agregado guarda e o EF preenche; as condições do pedido para aceitar a alteração são dele.
/// </summary>
internal sealed class AnexosDoPedido : Collection<AnexoDoPedido>
{
    internal bool TemVerificado => Items.Any(a => a.Situacao == SituacaoDoAnexo.Verificado);

    internal Result<AnexoDoPedido> Novo(Guid pedidoId, long tamanhoDoPdf)
    {
        Result<AnexoDoPedido> anexo = AnexoDoPedido.Criar(pedidoId, tamanhoDoPdf);
        if (anexo.IsSuccess)
        {
            Add(anexo.Value!);
        }

        return anexo;
    }

    internal Result<AnexoDoPedido> Confirmar(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        AnexoDoPedido? anexo = Items.FirstOrDefault(a => a.Id == anexoId);
        if (anexo is null)
        {
            return Result<AnexoDoPedido>.Failure(new DomainError(PedidoErrorCodes.NaoEncontrado, "Anexo do pedido não encontrado."));
        }

        Result confirmado = anexo.ConfirmarEnvio(etag, instante);

        return confirmado.IsSuccess
            ? Result<AnexoDoPedido>.Success(anexo)
            : Result<AnexoDoPedido>.ValidationFailure(confirmado.Errors);
    }
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os anexos de um pedido e o que se faz com eles: criar o documento declarado, pô-lo em verificação e saber se algum já foi
/// verificado. Opera sobre a coleção que o agregado guarda; as condições do pedido para aceitar a alteração são dele.
/// </summary>
internal readonly record struct AnexosDoPedido(Guid PedidoId, List<AnexoDoPedido> Itens)
{
    internal bool TemVerificado => Itens.Exists(a => a.Situacao == SituacaoDoAnexo.Verificado);

    internal Result<AnexoDoPedido> Novo(long tamanhoDoPdf)
    {
        Result<AnexoDoPedido> anexo = AnexoDoPedido.Criar(PedidoId, tamanhoDoPdf);
        if (anexo.IsSuccess)
        {
            Itens.Add(anexo.Value!);
        }

        return anexo;
    }

    internal Result<AnexoDoPedido> Confirmar(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        AnexoDoPedido? anexo = Itens.Find(a => a.Id == anexoId);
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

namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Grava o PDF como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no outbox na
/// mesma transação: não há documento em verificação sem pedido, nem pedido sem documento (ADR-0146).
/// </summary>
public static class ConfirmarDocumentoDaInstalacaoCommandHandler
{
    public static async Task<(Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarDocumentoDaInstalacaoCommand command,
        IInstalacaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Recusa(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        Instalacao? instalacao = await repository
            .ObterDoExecutorAsync(command.Id, executor, cancellationToken)
            .ConfigureAwait(false);
        if (instalacao is null)
        {
            return Recusa(InstalacaoHandlerErros.NaoEncontrada());
        }

        Result confirmado = instalacao.ConfirmarDocumento(command.ETag);
        if (confirmado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<InstalacaoDto>.ValidationFailure(confirmado.Errors), []);
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(InstalacaoHandlerErros.ConflitoDeConcorrencia());
        }

        PedidoDeVerificacaoDeArquivo pedido = new(
            instalacao.Id,
            instalacao.ChaveDeEnvio,
            instalacao.DocumentoETag!,
            instalacao.ChaveDefinitiva,
            FormatoDoArquivo.Pdf,
            instalacao.TamanhoDoPdf,
            DestinoDoResultado.Autorizacao);

        return (Result<InstalacaoDto>.Success(InstalacaoMapping.ToDto(instalacao)), [pedido]);
    }

    private static (Result<InstalacaoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<InstalacaoDto>.Failure(erro), []);
}

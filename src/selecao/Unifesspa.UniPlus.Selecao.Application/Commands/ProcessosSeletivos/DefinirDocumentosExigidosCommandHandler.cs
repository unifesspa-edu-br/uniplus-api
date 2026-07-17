namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDocumentosExigidosCommand"/> (Story #554, PR-a): resolve
/// o snapshot-copy de <c>TipoDocumento</c> (módulo Configuração, ADR-0056) para cada item
/// e delega a montagem/validação ao domínio.
/// </summary>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(tipoDocumentoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição precede a resolução de leituras externas (ADR-0110 D9) — mesma
        // nota dos demais Definir*.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<DocumentoExigido> itens = [];
        foreach (ItemDocumentoExigidoInput input in command.Itens)
        {
            TipoDocumentoView? tipoDocumento = await tipoDocumentoReader
                .ObterPorIdAsync(input.TipoDocumentoId, cancellationToken)
                .ConfigureAwait(false);
            if (tipoDocumento is null)
            {
                return Result<MutacaoAceita>.Failure(new DomainError(
                    "DocumentoExigido.TipoDocumentoNaoEncontrado",
                    $"Tipo de documento {input.TipoDocumentoId} não encontrado ou não está mais vivo."));
            }

            Aplicabilidade aplicabilidade = input.Aplicabilidade switch
            {
                "GERAL" => Aplicabilidade.Geral,
                "CONDICIONAL" => Aplicabilidade.Condicional,
                _ => Aplicabilidade.Nenhuma,
            };

            Result<DocumentoExigido> itemResult = DocumentoExigido.Criar(
                input.ExigidoNaFaseId,
                tipoDocumento.Id,
                tipoDocumento.Codigo,
                tipoDocumento.Nome,
                tipoDocumento.Categoria,
                aplicabilidade,
                input.Obrigatorio,
                input.ConsequenciaIndeferimento,
                input.GrupoSatisfacaoId);
            if (itemResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(itemResult.Error!);
            }

            itens.Add(itemResult.Value!);
        }

        Result definirResult = processo.DefinirDocumentosExigidos(itens, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}

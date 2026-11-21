namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDocumentosExigidosCommand"/>: resolve a cópia do tipo de documento (módulo
/// Configuração, ADR-0056) e, quando alguma folha declara gatilho, o vocabulário fechado de fatos do candidato
/// estendido pelo domínio dinâmico da oferta do próprio processo, monta a árvore E/OU (ver
/// <see cref="DefinicaoDosDocumentosExigidos"/>) e delega a validação final ao domínio.
/// </summary>
/// <remarks>
/// Escopo parcial do ADR-0125: a forma de um MESMO nó acumula (aplicabilidade, consequência de
/// indeferimento, tamanho máximo, coerência entre geral e condição, bases legais), e a árvore inteira é
/// percorrida de forma pura antes de qualquer leitura externa. Entre nós e folhas a recursão falha no
/// primeiro, e as checagens estruturais do grupo (ciclo, fase comum dos filhos, repetição de entidade
/// aninhada) ficam no domínio.
/// </remarks>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await DefinirBonusRegionalCommandHandler
            .ObterParaEdicaoAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        CadastrosDosDocumentosExigidos cadastros = new(tipoDocumentoReader, fatoCandidatoReader, modeloDeDocumentoRepository);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(command, obtido.Value!, cadastros, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        DefinirDocumentosExigidosCommand command,
        ProcessoSeletivo processo,
        CadastrosDosDocumentosExigidos cadastros,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(processo, command.Raizes, [], cadastros, cancellationToken)
            .ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return Result.ValidationFailure(definido.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}

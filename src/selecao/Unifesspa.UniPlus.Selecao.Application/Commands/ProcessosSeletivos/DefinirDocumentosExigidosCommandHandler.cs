namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDocumentosExigidosCommand"/>: resolve o snapshot-copy de
/// <c>TipoDocumento</c> (módulo Configuração, ADR-0056) e, quando alguma folha declara gatilho, o
/// vocabulário fechado de fatos do candidato estendido pelo domínio dinâmico da oferta do próprio processo,
/// monta a árvore E/OU (ver <see cref="DefinicaoDosDocumentosExigidos"/>) e delega a validação final ao
/// domínio.
/// </summary>
/// <remarks>
/// <para>
/// O relógio é injetado por convenção do módulo, ainda que a idade máxima de emissão seja avaliada só em
/// runtime, fora desta definição.
/// </para>
/// <para>
/// Escopo parcial do ADR-0125: a forma de um MESMO nó acumula (aplicabilidade, consequência de
/// indeferimento, tamanho máximo, coerência entre geral e condição, bases legais), e a árvore inteira é
/// percorrida de forma pura antes de qualquer leitura externa. Entre nós e folhas a recursão falha no
/// primeiro, e as checagens estruturais do grupo (ciclo, fase comum dos filhos, repetição de entidade
/// aninhada) ficam no domínio.
/// </para>
/// </remarks>
public static class DefinirDocumentosExigidosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirDocumentosExigidosCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ITipoDocumentoReader tipoDocumentoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // A precondição precede a resolução de leituras externas (ADR-0110).
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        Result definido = await DefinicaoDosDocumentosExigidos.DefinirAsync(
                processo,
                command.Raizes,
                [],
                command.Precondicao,
                new CadastrosDosDocumentosExigidos(tipoDocumentoReader, fatoCandidatoReader, modeloDeDocumentoRepository),
                cancellationToken)
            .ConfigureAwait(false);
        if (definido.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definido.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}

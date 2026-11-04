namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Handler do <see cref="DefinirCronogramaFasesCommand"/>: monta as fases (ver <see cref="MontagemDasFases"/>)
/// com a data do relógio injetado lida <b>uma vez</b> por operação (ADR-0068), entrega ao domínio o grafo de
/// precedências vigente como parâmetro, e não como navegação (ADR-0042), e delega a validação final ao
/// agregado. A recusa do domínio sai como LOTE: a conclusão do ciclo recursal acumula uma violação por fase
/// mal declarada, e reduzi-la a um <c>DomainError</c> apagaria os campos que localizam cada uma.
/// </summary>
public static class DefinirCronogramaFasesCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCronogramaFasesCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFaseCanonicaReader faseCanonicaReader,
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        ITipoBancaReader tipoBancaReader,
        ICategoriaDocumentoReader categoriaDocumentoReader,
        IPrecedenciaFaseReader precedenciaFaseReader,
        IRegraCatalogoReader regraCatalogoReader,
        ITipoAtoPublicadoReader tipoAtoPublicadoReader,
        IRegistroCodecsEnvelope registroCodecs,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(precedenciaFaseReader);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // A precondição precede a resolução de regras externas (ADR-0110).
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        CadastrosDasFases cadastros = new(
            faseCanonicaReader, unidadeResponsavelResolvedor, tipoBancaReader, categoriaDocumentoReader, regraCatalogoReader);
        AtosConsultadosNaEdicao atos = new(
            tipoAtoPublicadoReader, processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, timeProvider);
        Result definida = await DefinirAsync(
                processo, command, new MontagemDasFases(processo, cadastros, atos), precedenciaFaseReader, cancellationToken)
            .ConfigureAwait(false);
        if (definida.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    // O grafo de precedências é parâmetro do domínio, e não navegação (ADR-0042): é lido depois de as fases
    // serem montadas, e só se elas vieram sem recusa.
    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        DefinirCronogramaFasesCommand command,
        MontagemDasFases montagem,
        IPrecedenciaFaseReader precedenciaFaseReader,
        CancellationToken cancellationToken)
    {
        Result<List<FaseCronograma>> fases = await montagem.MontarAsync(command.Fases, cancellationToken).ConfigureAwait(false);
        if (fases.IsFailure)
        {
            return Result.ValidationFailure(fases.Errors);
        }

        IReadOnlyList<PrecedenciaFaseView> arestasVivas = await precedenciaFaseReader
            .ListarVivasAsync(cancellationToken)
            .ConfigureAwait(false);
        List<ArestaPrecedencia> precedencias = [.. arestasVivas
            .Select(static a => new ArestaPrecedencia(a.AntecessoraCodigo, a.SucessoraCodigo, a.PermiteSobreposicao))];
        return processo.DefinirCronogramaFases(fases.Value!, precedencias, command.Precondicao);
    }
}

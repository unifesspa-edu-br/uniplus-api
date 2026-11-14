namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarOfertaCursoCommand"/> (convention-based Wolverine):
/// valida os campos regulatórios do payload primeiro (422, sem I/O — validação
/// sempre vence I/O), depois a existência viva do curso e do local de oferta (o
/// filtro global de soft-delete já exclui removidos), depois resolve a Unidade
/// viva via <see cref="IUnidadeReader"/> (ADR-0056) e a congela por snapshot-copy
/// (<see cref="UnidadeOfertante"/>, ADR-0061) — sem identidade viva não há o que
/// congelar, então unidade inexistente/removida é rejeitada. Sem chave natural
/// única: não há checagem de unicidade nem tradução de 23505.
/// </summary>
public static class CriarOfertaCursoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarOfertaCursoCommand command,
        IOfertaCursoRepository repository,
        ICursoRepository cursoRepository,
        ILocalOfertaRepository localOfertaRepository,
        IUnidadeReader unidadeReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cursoRepository);
        ArgumentNullException.ThrowIfNull(localOfertaRepository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        // Validação sempre vence I/O: os dez campos regulatórios do payload não
        // dependem de curso, local nem unidade — falham rápido antes de qualquer
        // busca no próprio módulo ou em módulo cruzado.
        Result preCheck = OfertaCurso.ValidarCamposDoPayload(
            command.ProgramaDeOferta,
            command.FormatoPedagogico,
            command.RegimeDeFuncionamento,
            command.RegimeDeTurno,
            command.Turnos,
            command.EMecCodigo,
            command.CodigoSga,
            command.VagasAnuaisAutorizadas,
            command.BaseLegal,
            command.AtoAutorizacaoMec);

        if (preCheck.IsFailure)
        {
            return Result<Guid>.ValidationFailure(preCheck.Errors);
        }

        // Existência viva do curso: o query filter global de soft-delete já
        // exclui mortos — inexistente e removido caem no mesmo erro.
        Curso? curso = await cursoRepository
            .ObterPorIdParaLeituraAsync(command.CursoId, cancellationToken)
            .ConfigureAwait(false);
        if (curso is null)
        {
            return Result<Guid>.Failure(new DomainError(
                OfertaCursoErrorCodes.CursoInexistenteOuRemovido,
                "Curso inexistente ou removido — a oferta exige um curso vivo."));
        }

        LocalOferta? localOferta = await localOfertaRepository
            .ObterPorIdParaLeituraAsync(command.LocalOfertaId, cancellationToken)
            .ConfigureAwait(false);
        if (localOferta is null)
        {
            return Result<Guid>.Failure(new DomainError(
                OfertaCursoErrorCodes.LocalOfertaInexistenteOuRemovido,
                "Local de oferta inexistente ou removido — a oferta exige um local vivo."));
        }

        // Snapshot-copy da unidade ofertante (ADR-0061): resolve a Unidade VIVA
        // via reader cross-módulo (ADR-0056) e congela sigla/nome/tipo. Reader
        // devolve null para inexistente/soft-deleted — não há identidade viva
        // para congelar, então a criação é rejeitada.
        UnidadeView? unidadeView = await unidadeReader
            .ObterPorIdAsync(command.UnidadeOfertanteOrigemId, cancellationToken)
            .ConfigureAwait(false);
        if (unidadeView is null)
        {
            return Result<Guid>.Failure(new DomainError(
                OfertaCursoErrorCodes.UnidadeOfertanteInexistente,
                "Unidade ofertante inexistente ou removida — não há identidade viva para congelar."));
        }

        // O Tipo da UnidadeView já é a string estável do contrato (desacoplada do
        // enum interno de Organização) — congela como veio.
        Result<UnidadeOfertante> unidadeOfertanteResult = UnidadeOfertante.Criar(
            unidadeView.Id, unidadeView.Sigla, unidadeView.Nome, unidadeView.Tipo);
        if (unidadeOfertanteResult.IsFailure)
        {
            return Result<Guid>.Failure(unidadeOfertanteResult.Error!);
        }

        Result<OfertaCurso> ofertaResult = OfertaCurso.Criar(
            command.CursoId,
            command.LocalOfertaId,
            unidadeOfertanteResult.Value!,
            command.ProgramaDeOferta,
            command.FormatoPedagogico,
            command.RegimeDeFuncionamento,
            command.RegimeDeTurno,
            command.Turnos,
            command.EMecCodigo,
            command.CodigoSga,
            command.VagasAnuaisAutorizadas,
            command.BaseLegal,
            command.AtoAutorizacaoMec);

        if (ofertaResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(ofertaResult.Errors);
        }

        OfertaCurso oferta = ofertaResult.Value!;
        await repository.AdicionarAsync(oferta, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(oferta.Id);
    }
}

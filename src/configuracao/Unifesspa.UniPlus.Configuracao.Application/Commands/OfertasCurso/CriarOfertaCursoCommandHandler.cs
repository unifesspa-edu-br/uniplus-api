namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// Handler do <see cref="CriarOfertaCursoCommand"/> (convention-based Wolverine):
/// valida a existência viva do curso e do local de oferta (o filtro global de
/// soft-delete já exclui removidos), resolve a Unidade viva via
/// <see cref="IUnidadeReader"/> (ADR-0056) e a congela por snapshot-copy
/// (<see cref="UnidadeOfertante"/>, ADR-0061) — sem identidade viva não há o que
/// congelar, então unidade inexistente/removida é rejeitada. Sem chave natural
/// única: não há checagem de unicidade nem tradução de 23505.
/// </summary>
public static class CriarOfertaCursoCommandHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c> é necessário porque este é o primeiro handler do
    /// monólito que injeta um reader cross-módulo (<see cref="IUnidadeReader"/>,
    /// implementado em Organização, dependente de
    /// <c>OrganizacaoInstitucionalDbContext</c>) dentro de um handler que TAMBÉM
    /// escreve no seu próprio módulo (via <see cref="IConfiguracaoUnitOfWork"/>,
    /// dependente de <c>ConfiguracaoDbContext</c>). O detector de transação do
    /// Wolverine.EntityFrameworkCore (<c>AutoApplyTransactions</c>) enumera TODAS
    /// as dependências transitivas dos parâmetros do handler em busca de um único
    /// <c>DbContext</c> a enrolar na transação do outbox — encontra os dois e
    /// falha por ambiguidade (<c>InvalidOperationException</c> no boot do host).
    /// A persistência continua correta sem o enrolamento automático: o handler já
    /// chama <see cref="IConfiguracaoUnitOfWork.SalvarAlteracoesAsync"/>
    /// explicitamente (mesmo padrão de todo handler de Configuração), e o módulo
    /// não emite domain events — não há atomicidade write+evento de outbox a
    /// perder (ADR-0004 não se aplica aqui). O reader é só leitura, sem
    /// participação na transação de escrita.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<Guid>> Handle(
        CriarOfertaCursoCommand command,
        IOfertaCursoRepository repository,
        ICursoRepository cursoRepository,
        ILocalOfertaRepository localOfertaRepository,
        IUnidadeReader unidadeReader,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(cursoRepository);
        ArgumentNullException.ThrowIfNull(localOfertaRepository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

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
            command.Turno,
            command.EMecCodigo,
            command.CodigoSga,
            command.VagasAnuaisAutorizadas,
            command.BaseLegal,
            command.AtoAutorizacaoMec);

        if (ofertaResult.IsFailure)
        {
            return Result<Guid>.Failure(ofertaResult.Error!);
        }

        OfertaCurso oferta = ofertaResult.Value!;
        await repository.AdicionarAsync(oferta, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(oferta.Id);
    }
}

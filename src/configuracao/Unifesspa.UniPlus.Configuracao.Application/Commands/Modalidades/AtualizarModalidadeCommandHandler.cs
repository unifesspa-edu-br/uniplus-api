namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler do <see cref="AtualizarModalidadeCommand"/>. Pré-checa só os campos
/// independentes de coerência cruzada (parse, tamanhos — 422, sem I/O) ANTES de
/// buscar a modalidade por Id — validação sempre vence 404. As três invariantes
/// cruzadas NÃO entram no pré-check: precisam vir depois da guarda do catálogo
/// legal fixo, que só <see cref="Modalidade.Atualizar"/> pode avaliar, pois
/// depende do <c>Codigo</c> já persistido — reportar uma incoerência antes do
/// fetch preemptiria "esta modalidade não se edita" com "corrija a coerência do
/// payload" quando as duas violações coexistem. Depois do fetch (404),
/// <see cref="Modalidade.Atualizar"/> aplica a guarda e só então as coerências, na
/// ordem certa. Por fim, revalida a integridade referencial dos códigos citados
/// (422, DB) e commita.
/// </summary>
public static class AtualizarModalidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarModalidadeCommand command,
        IModalidadeRepository repository,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<PredicadoDnf?> regra = RegraDeAplicabilidadeDaEntrada.Montar(command.RegraDeAplicabilidade);
        DeclaracaoDaModalidade declaracao = command.Declaracao(regra.IsSuccess ? regra.Value : null);
        List<FieldError> recusasDoPayload = RecusasDoPayload(regra, declaracao);

        return recusasDoPayload.Count > 0
            ? Result.ValidationFailure(recusasDoPayload)
            : await AtualizarAsync(command.Id, declaracao, repository, fatoRepository, precedenciaRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static List<FieldError> RecusasDoPayload(Result<PredicadoDnf?> regra, DeclaracaoDaModalidade declaracao)
    {
        Result preCheck = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(declaracao);

        return RegraDeAplicabilidadeDaEntrada.ComAsRecusasDaMontagem(regra, preCheck.IsFailure ? preCheck.Errors : []);
    }

    private static async Task<Result> AtualizarAsync(
        Guid id,
        DeclaracaoDaModalidade declaracao,
        IModalidadeRepository repository,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Modalidade? modalidade = await repository.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (modalidade is null)
        {
            return NaoEncontrada();
        }

        Result atualizada = await AplicarEConferirAsync(
            modalidade, declaracao, fatoRepository, precedenciaRepository, unitOfWork, cancellationToken).ConfigureAwait(false);

        return atualizada.IsFailure
            ? atualizada
            : await GravarSeAsReferenciasExistemAsync(modalidade, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A vigente é lida antes de <see cref="Modalidade.Atualizar"/> trocá-la: é ela que diz quais fatos e valores
    /// desativados a regra já citava. A regra da modalidade legal fixa só muda por semente, e a guarda de
    /// Atualizar recusa a estrutura inteira: conferi-la contra o catálogo responderia recusa que não se aplica.
    /// </summary>
    private static async Task<Result> AplicarEConferirAsync(
        Modalidade modalidade,
        DeclaracaoDaModalidade declaracao,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        PredicadoDnf vigente = modalidade.RegraDeAplicabilidade;
        Result atualizada = modalidade.Atualizar(declaracao);

        return modalidade.Codigo.EhLegalFixa
            ? atualizada
            : await ConferirContraOCatalogoAsync(
                atualizada, declaracao.RegraDeAplicabilidade!, vigente, fatoRepository, precedenciaRepository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result> ConferirContraOCatalogoAsync(
        Result atualizada,
        PredicadoDnf regra,
        PredicadoDnf vigente,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        CatalogoDeFatos catalogo = await RegraDeAplicabilidadeDaEntrada
            .CarregarCatalogoAsync(fatoRepository, precedenciaRepository, cancellationToken).ConfigureAwait(false);
        List<FieldError> erros = atualizada.IsFailure ? [.. atualizada.Errors] : [];
        erros.AddRange(ConferenciaDaRegraDeAplicabilidade.Conferir(regra, vigente, catalogo));
        if (erros.Count == 0)
        {
            return Result.Success();
        }

        // Atualizar pode ter mutado o agregado rastreado antes da recusa do catálogo.
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Integridade referencial (invariante 7): todos os códigos citados após a atualização devem existir como
    /// modalidade viva. <see cref="Modalidade.Atualizar"/> já mutou o agregado rastreado — descarta antes de
    /// devolver a falha, senão o save automático do Wolverine persistiria a alteração cuja referência acabou de
    /// ser recusada.
    /// </summary>
    private static async Task<Result> GravarSeAsReferenciasExistemAsync(
        Modalidade modalidade, IModalidadeRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (!await ReferenciasDeModalidade.TodasExistemAsync(modalidade, repository, cancellationToken).ConfigureAwait(false))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(ReferenciasDeModalidade.RecusaDaReferenciaInexistente());
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Result NaoEncontrada() =>
        Result.Failure(new DomainError(ModalidadeErrorCodes.NaoEncontrada, "Modalidade de concorrência não encontrada."));
}

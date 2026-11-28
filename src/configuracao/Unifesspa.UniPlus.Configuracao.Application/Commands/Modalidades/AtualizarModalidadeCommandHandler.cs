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
/// ordem certa. Por fim, recusa o nome já usado por outra modalidade viva (409, DB), revalida a
/// integridade referencial dos códigos citados (422, DB) e commita, traduzindo a violação do índice único do
/// nome numa corrida em <c>NomeJaExiste</c>.
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
        Result conferenciaDaDeclaracao = ValidacaoDosCamposDaModalidade.ValidarCamposDoPayload(declaracao);

        return RegraDeAplicabilidadeDaEntrada.ComAsRecusasDaMontagem(regra, conferenciaDaDeclaracao.IsFailure ? conferenciaDaDeclaracao.Errors : []);
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
            : await GravarSeNaoHouverRecusaAsync(modalidade, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
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
    /// <see cref="Modalidade.Atualizar"/> já mutou o agregado rastreado: toda recusa que vem depois dele
    /// descarta a alteração antes de devolver a falha, senão o save automático do Wolverine a persistiria. A
    /// recusa do nome vem antes da integridade referencial dos códigos citados (invariante 7).
    /// </summary>
    private static async Task<Result> GravarSeNaoHouverRecusaAsync(
        Modalidade modalidade, IModalidadeRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        DomainError? recusa = await RecusaDoNomeAsync(modalidade, repository, cancellationToken).ConfigureAwait(false);
        recusa ??= await RecusaDasReferenciasAsync(modalidade, repository, cancellationToken).ConfigureAwait(false);
        if (recusa is null)
        {
            return await SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
        }

        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result.Failure(recusa);
    }

    private static async Task<DomainError?> RecusaDoNomeAsync(
        Modalidade modalidade, IModalidadeRepository repository, CancellationToken cancellationToken) =>
        await repository.NomeExisteEntreVivosAsync(modalidade.Nome, modalidade.Id, cancellationToken).ConfigureAwait(false)
            ? UniqueConstraintViolation.NomeJaExisteErro()
            : null;

    private static async Task<DomainError?> RecusaDasReferenciasAsync(
        Modalidade modalidade, IModalidadeRepository repository, CancellationToken cancellationToken) =>
        await ReferenciasDeModalidade.TodasExistemAsync(modalidade, repository, cancellationToken).ConfigureAwait(false)
            ? null
            : ReferenciasDeModalidade.RecusaDaReferenciaInexistente();

    /// <summary>
    /// Protege a corrida entre a consulta do nome e o UPDATE: o índice único parcial dispara 23505 e vira o mesmo
    /// <c>NomeJaExiste</c> do caminho sem corrida. O filtro do <c>when</c> deixa as demais exceções propagarem.
    /// </summary>
    private static async Task<Result> SalvarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.ErroDoIndiceViolado(ex) is { } erro)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(erro);
        }

        return Result.Success();
    }

    private static Result NaoEncontrada() =>
        Result.Failure(new DomainError(ModalidadeErrorCodes.NaoEncontrada, "Modalidade de concorrência não encontrada."));
}

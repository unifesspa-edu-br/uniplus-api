namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler do <see cref="CriarModalidadeCommand"/> (convention-based Wolverine).
/// Orquestra, nessa ordem: construção do agregado (validação de campos e coerência,
/// 422) com a conferência da regra de aplicabilidade contra o catálogo de fatos, as
/// recusas das duas acumuladas (ADR-0125); reserva dos códigos do catálogo legal
/// fixo (409, sem I/O), unicidade do código e do nome entre vivos (409, DB), integridade
/// referencial dos códigos citados em composição/remanejamento (422, DB),
/// persistência e commit. Protege a corrida check-then-act traduzindo a violação dos
/// índices únicos parciais em <c>CodigoJaExiste</c> ou <c>NomeJaExiste</c>.
/// </summary>
public static class CriarModalidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarModalidadeCommand command,
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

        Result<Modalidade> nova = await NovaModalidadeAsync(command, fatoRepository, precedenciaRepository, cancellationToken).ConfigureAwait(false);

        return nova.IsFailure
            ? Result<Guid>.ValidationFailure(nova.Errors)
            : await CadastrarAsync(nova.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Modalidade>> NovaModalidadeAsync(
        CriarModalidadeCommand command,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        CancellationToken cancellationToken)
    {
        Result<PredicadoDnf?> regra = RegraDeAplicabilidadeDaEntrada.Montar(command.RegraDeAplicabilidade);
        PredicadoDnf? regraMontada = regra.IsSuccess ? regra.Value : null;
        Result<Modalidade> modalidade = Modalidade.Criar(command.Codigo, command.Declaracao(regraMontada));

        List<FieldError> erros = RegraDeAplicabilidadeDaEntrada.ComAsRecusasDaMontagem(
            regra, modalidade.IsFailure ? modalidade.Errors : []);
        if (regraMontada is not null)
        {
            erros.AddRange(await RecusasDaRegraContraOCatalogoAsync(regraMontada, fatoRepository, precedenciaRepository, cancellationToken).ConfigureAwait(false));
        }

        return erros.Count > 0 ? Result<Modalidade>.ValidationFailure(erros) : modalidade;
    }

    private static async Task<IReadOnlyList<FieldError>> RecusasDaRegraContraOCatalogoAsync(
        PredicadoDnf regra,
        IFatoCandidatoRepository fatoRepository,
        IPrecedenciaFaseRepository precedenciaRepository,
        CancellationToken cancellationToken)
    {
        CatalogoDeFatos catalogo = await RegraDeAplicabilidadeDaEntrada
            .CarregarCatalogoAsync(fatoRepository, precedenciaRepository, cancellationToken).ConfigureAwait(false);
        return ConferenciaDaRegraDeAplicabilidade.Conferir(regra, vigente: null, catalogo);
    }

    private static async Task<Result<Guid>> CadastrarAsync(
        Modalidade modalidade, IModalidadeRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        DomainError? recusa = await RecusaDoCadastroAsync(modalidade, repository, cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? await GravarAsync(modalidade, repository, unitOfWork, cancellationToken).ConfigureAwait(false)
            : Result<Guid>.Failure(recusa);
    }

    private static async Task<DomainError?> RecusaDoCadastroAsync(
        Modalidade modalidade, IModalidadeRepository repository, CancellationToken cancellationToken)
    {
        DomainError? recusa = await RecusaDoCodigoAsync(modalidade.Codigo, repository, cancellationToken).ConfigureAwait(false);
        recusa ??= await RecusaDoNomeAsync(modalidade.Nome, repository, cancellationToken).ConfigureAwait(false);

        return recusa ?? await RecusaDasReferenciasAsync(modalidade, repository, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Os 12 códigos legais fixos só nascem do seed. A reserva é do cadastro, não do domínio (a factory os aceita —
    /// reidratação administrativa depende disso), e só é checada depois que o payload já está confirmado coerente:
    /// 409 é sobre QUEM pode nascer, não sobre a FORMA do payload.
    /// </summary>
    private static async Task<DomainError?> RecusaDoCodigoAsync(
        CodigoModalidade codigo, IModalidadeRepository repository, CancellationToken cancellationToken)
    {
        if (codigo.EhLegalFixa)
        {
            return CodigoReservadoErro();
        }

        return await repository.CodigoExisteEntreVivosAsync(codigo.Valor, null, cancellationToken).ConfigureAwait(false)
            ? UniqueConstraintViolation.CodigoJaExisteErro()
            : null;
    }

    private static async Task<DomainError?> RecusaDoNomeAsync(
        string nome, IModalidadeRepository repository, CancellationToken cancellationToken) =>
        await repository.NomeExisteEntreVivosAsync(nome, null, cancellationToken).ConfigureAwait(false)
            ? UniqueConstraintViolation.NomeJaExisteErro()
            : null;

    /// <summary>
    /// Integridade referencial (invariante 7): todos os códigos citados (origem + remanejamento) devem existir
    /// como modalidade viva.
    /// </summary>
    private static async Task<DomainError?> RecusaDasReferenciasAsync(
        Modalidade modalidade, IModalidadeRepository repository, CancellationToken cancellationToken) =>
        await ReferenciasDeModalidade.TodasExistemAsync(modalidade, repository, cancellationToken).ConfigureAwait(false)
            ? null
            : ReferenciasDeModalidade.RecusaDaReferenciaInexistente();

    /// <summary>
    /// Protege a corrida entre a consulta de unicidade e o INSERT (check-then-act): o índice único parcial dispara
    /// 23505 e vira o mesmo <c>CodigoJaExiste</c> ou <c>NomeJaExiste</c> do caminho sem corrida — 409 consistente. O filtro do <c>when</c>
    /// garante que outras exceções propagam intactas. A entidade que não foi persistida sai do rastreamento, senão
    /// o save automático do Wolverine repetiria a violação fora deste <c>catch</c>.
    /// </summary>
    private static async Task<Result<Guid>> GravarAsync(
        Modalidade modalidade, IModalidadeRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(modalidade, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.ErroDoIndiceViolado(ex) is { } erro)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(erro);
        }

        return Result<Guid>.Success(modalidade.Id);
    }

    private static DomainError CodigoReservadoErro() =>
        new(ModalidadeErrorCodes.CriacaoBloqueadaCodigoProtegido,
            "Este código é reservado ao catálogo legal fixo e não pode ser criado por cadastro.");
}

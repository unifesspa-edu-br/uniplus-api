namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Permissoes;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Handler convention-based do <see cref="CriarProcessoSeletivoCommand"/>:
/// resolve a Unidade administradora viva via <see cref="IUnidadeReader"/> (ADR-0056),
/// congela por snapshot-copy (<see cref="UnidadeAdministradoraSnapshot"/>, ADR-0061),
/// cria o agregado-raiz em rascunho, persiste via
/// <see cref="IProcessoSeletivoRepository"/> e retorna o id.
/// </summary>
public static class CriarProcessoSeletivoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IVerificadorDeAcesso verificador,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(tipoProcessoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> novo = await NovoProcessoAsync(
            command, processoSeletivoRepository, verificador, unidadeReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);

        return novo.IsFailure
            ? Result<Guid>.ValidationFailure(novo.Errors)
            : await GravarAsync(novo.Value!, processoSeletivoRepository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ProcessoSeletivo>> NovoProcessoAsync(
        CriarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IVerificadorDeAcesso verificador,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        Result<IdentificadorLegivel> identificador = await IdentificadorDeQuemPodeCriarAsync(command, verificador, cancellationToken).ConfigureAwait(false);
        if (identificador.IsFailure)
        {
            return Result<ProcessoSeletivo>.ValidationFailure(identificador.Errors);
        }

        Result<ReferenciasDoNovoProcesso> referencias = await ReferenciasDoNovoProcesso
            .ResolverAsync(command, unidadeReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);

        return referencias.IsFailure
            ? Result<ProcessoSeletivo>.ValidationFailure(referencias.Errors)
            : await CriarSeOIdentificadorEstaLivreAsync(
                command, identificador.Value!, referencias.Value!, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// O processo nasce na unidade que o comando nomeia, então é contra ela que a permissão se decide, antes de
    /// qualquer conferência dos dados e de qualquer leitura: quem não pode configurar naquela unidade recebe a recusa
    /// de acesso, qualquer que seja o estado do pedido, e não descobre os campos exigidos nem se a unidade existe ou
    /// tem cidade. Sem unidade informada não há contra o que decidir, e a falta dela segue como dado do pedido.
    /// Depois da permissão, os dados do pedido e o identificador legível são conferidos antes de qualquer consulta.
    /// </summary>
    private static async Task<Result<IdentificadorLegivel>> IdentificadorDeQuemPodeCriarAsync(
        CriarProcessoSeletivoCommand command, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ResultadoDoAcesso acesso = command.UnidadeAdministradoraOrigemId == Guid.Empty
            ? ResultadoDoAcesso.Permitido
            : await AcessoAoProcesso
                .ExigirNaUnidadeAsync(
                    verificador, command.UnidadeAdministradoraOrigemId, PermissaoSelecao.ProcessosConfigurar,
                    unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoId: null), cancellationToken)
                .ConfigureAwait(false);

        return acesso == ResultadoDoAcesso.Permitido
            ? DadosEIdentificadorDeclarados(command)
            : Result<IdentificadorLegivel>.Failure(AcessoAoProcesso.Recusa(acesso));
    }

    private static Result<IdentificadorLegivel> DadosEIdentificadorDeclarados(CriarProcessoSeletivoCommand command)
    {
        IReadOnlyList<FieldError> recusas = CamposDoNovoProcesso.Conferir(command);

        return recusas.Count > 0
            ? Result<IdentificadorLegivel>.ValidationFailure(recusas)
            : IdentificadorLegivelDeclarado(command.IdentificadorLegivel);
    }

    private static async Task<Result<ProcessoSeletivo>> CriarSeOIdentificadorEstaLivreAsync(
        CriarProcessoSeletivoCommand command,
        IdentificadorLegivel identificador,
        ReferenciasDoNovoProcesso referencias,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        bool emUso = await processoSeletivoRepository
            .IdentificadorLegivelEmUsoAsync(identificador, excluirId: null, cancellationToken).ConfigureAwait(false);

        return emUso
            ? Result<ProcessoSeletivo>.Failure(IdentificadorLegivelEmUso(identificador))
            : Result<ProcessoSeletivo>.Success(ProcessoSeletivo.Criar(
                command.Nome!, referencias.Tipo, CatalogoDe<OrigemCandidatos>.Obter(command.OrigemCandidatos!), referencias.UnidadeId, referencias.Unidade,
                referencias.Localidade, identificador));
    }

    /// <summary>
    /// Se outro processo gravou o mesmo identificador entre a consulta e esta gravação, o processo recusado sai do
    /// contexto: sem isso ele continuaria Added no escopo, e uma gravação posterior no mesmo contexto tentaria
    /// inseri-lo de novo.
    /// </summary>
    private static async Task<Result<Guid>> GravarAsync(
        ProcessoSeletivo processo,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        await processoSeletivoRepository.AdicionarAsync(processo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (EhConflitoDeIdentificadorLegivel(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(IdentificadorLegivelEmUso(processo.IdentificadorLegivel!.Value));
        }

        return Result<Guid>.Success(processo.Id);
    }

    private const string IndiceDoIdentificadorLegivel = "ux_processos_seletivos_identificador_legivel";

    internal static bool EhConflitoDeIdentificadorLegivel(Exception ex) => string.Equals(
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex), IndiceDoIdentificadorLegivel, StringComparison.Ordinal);

    /// <summary>
    /// O identificador declarado pelo cliente, já no formato do domínio. A ausência é erro
    /// nomeado, e não regra do validator, pelo mesmo motivo da localidade: o consumidor precisa
    /// da página que explica a causa.
    /// </summary>
    internal static Result<IdentificadorLegivel> IdentificadorLegivelDeclarado(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? Result<IdentificadorLegivel>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente,
                "O identificador legível é obrigatório."))
            : IdentificadorLegivel.Criar(valor);

    internal static DomainError IdentificadorLegivelEmUso(IdentificadorLegivel identificador) => new(
        ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso,
        $"O identificador legível '{identificador}' já é usado por outro processo seletivo.");
}

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

    /// <summary>
    /// O processo nasce na unidade que o comando nomeia, então é contra ela que a permissão se decide, antes de
    /// qualquer conferência dos dados e de qualquer leitura: quem não pode configurar naquela unidade recebe a recusa
    /// de acesso, qualquer que seja o estado do pedido, e não descobre os campos exigidos nem se a unidade existe ou
    /// tem cidade. Sem unidade informada não há contra o que decidir: a falta dela segue como dado do pedido, junto
    /// das demais recusas que não precisam de leitura, e nada é lido.
    /// </summary>
    private static async Task<Result<ProcessoSeletivo>> NovoProcessoAsync(
        CriarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IVerificadorDeAcesso verificador,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        ResultadoDoAcesso acesso = await AcessoNaUnidadeAsync(command, verificador, cancellationToken).ConfigureAwait(false);
        if (acesso != ResultadoDoAcesso.Permitido)
        {
            return Result<ProcessoSeletivo>.Failure(AcessoAoProcesso.Recusa(acesso));
        }

        return command.UnidadeAdministradoraOrigemId == Guid.Empty
            ? Result<ProcessoSeletivo>.ValidationFailure([
                .. CamposDoNovoProcesso.Conferir(command),
                .. IdentificadorLegivelDeclarado.Conferir(command.IdentificadorLegivel).Errors,
                .. ReferenciasDoNovoProcesso.RecusasSemLeitura(command)])
            : await ConferirECriarAsync(command, processoSeletivoRepository, unidadeReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ResultadoDoAcesso> AcessoNaUnidadeAsync(
        CriarProcessoSeletivoCommand command, IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        command.UnidadeAdministradoraOrigemId == Guid.Empty
            ? ResultadoDoAcesso.Permitido
            : await AcessoAoProcesso
                .ExigirNaUnidadeAsync(
                    verificador, command.UnidadeAdministradoraOrigemId, PermissaoSelecao.ProcessosConfigurar,
                    unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoId: null), cancellationToken)
                .ConfigureAwait(false);

    /// <summary>
    /// Com a permissão decidida, todas as recusas independentes saem juntas (ADR-0125), na ordem em que o cliente as
    /// corrige: os campos, o identificador, a unidade, a localidade, o tipo e, por último, o identificador já usado,
    /// que só é consultado quando o identificador foi aceito.
    /// </summary>
    private static async Task<Result<ProcessoSeletivo>> ConferirECriarAsync(
        CriarProcessoSeletivoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldError> campos = CamposDoNovoProcesso.Conferir(command);
        Result<IdentificadorLegivel> identificador = IdentificadorLegivelDeclarado.Conferir(command.IdentificadorLegivel);
        Result<ReferenciasDoNovoProcesso> referencias = await ReferenciasDoNovoProcesso
            .ResolverAsync(command, unidadeReader, tipoProcessoReader, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> uso = await RecusaDoUsoAsync(identificador, processoSeletivoRepository, cancellationToken).ConfigureAwait(false);

        FieldError[] recusas = [.. campos, .. identificador.Errors, .. referencias.Errors, .. uso];

        return recusas.Length > 0
            ? Result<ProcessoSeletivo>.ValidationFailure(recusas)
            : Result<ProcessoSeletivo>.Success(Criar(command, identificador.Value!, referencias.Value!));
    }

    private static async Task<IReadOnlyList<FieldError>> RecusaDoUsoAsync(
        Result<IdentificadorLegivel> identificador, IProcessoSeletivoRepository processoSeletivoRepository, CancellationToken cancellationToken)
    {
        if (identificador.IsFailure)
        {
            return [];
        }

        bool emUso = await processoSeletivoRepository
            .IdentificadorLegivelEmUsoAsync(identificador.Value!, excluirId: null, cancellationToken).ConfigureAwait(false);

        return emUso ? [IdentificadorLegivelDeclarado.RecusaDoUso()] : [];
    }

    private static ProcessoSeletivo Criar(
        CriarProcessoSeletivoCommand command, IdentificadorLegivel identificador, ReferenciasDoNovoProcesso referencias)
    {
        string nome = command.Nome ?? throw new InvalidOperationException("O nome do processo foi conferido antes de criar.");

        return ProcessoSeletivo.Criar(
            nome, referencias.Tipo, CatalogoDe<OrigemCandidatos>.Obter(command.OrigemCandidatos!), referencias.UnidadeId, referencias.Unidade,
            referencias.Localidade, identificador);
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
            return Result<Guid>.Failure(IdentificadorLegivelDeclarado.EmUso());
        }

        return Result<Guid>.Success(processo.Id);
    }

    private const string IndiceDoIdentificadorLegivel = "ux_processos_seletivos_identificador_legivel";

    internal static bool EhConflitoDeIdentificadorLegivel(Exception ex) => string.Equals(
        ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex), IndiceDoIdentificadorLegivel, StringComparison.Ordinal);
}

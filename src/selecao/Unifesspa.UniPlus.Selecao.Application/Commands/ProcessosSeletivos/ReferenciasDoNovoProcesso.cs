namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Domain.Cidades;
using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// O que a criação do processo congela dos cadastros: a unidade administradora, a localidade que rege a
/// contagem dos prazos e o tipo de processo. Nenhuma conferência impede a seguinte, e as recusas saem juntas (ADR-0125)
/// na ordem em que o cliente as corrige: a unidade, a localidade declarada e, por fim, o tipo.
/// </summary>
internal sealed record ReferenciasDoNovoProcesso(
    Guid UnidadeId,
    UnidadeAdministradoraSnapshot Unidade,
    LocalidadeRegente Localidade,
    TipoProcessoSnapshot Tipo)
{
    private const string CampoUnidade = "unidadeAdministradoraOrigemId";
    private const string CampoTipo = "tipoProcessoOrigemId";
    private const string CampoLocalidadeCodigoIbge = "localidadeCodigoIbge";

    private static readonly CamposDaCidade CamposDaLocalidade = new(CampoLocalidadeCodigoIbge, "localidadeNome", "localidadeUf");

    /// <summary>
    /// Com a unidade informada e a permissão já decidida, lê a unidade e o tipo, um depois do outro, e junta as
    /// recusas das três conferências.
    /// </summary>
    internal static async Task<Result<ReferenciasDoNovoProcesso>> ResolverAsync(
        CriarProcessoSeletivoCommand command,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        Result<(Guid Id, UnidadeAdministradoraSnapshot Copia)> unidade = await UnidadeConferidaAsync(
            command.UnidadeAdministradoraOrigemId, unidadeReader, cancellationToken).ConfigureAwait(false);
        Result<LocalidadeRegente> localidade = LocalidadeDeclarada(command);
        Result<TipoProcessoSnapshot> tipo = await TipoAtivoAsync(
            command.TipoProcessoOrigemId, tipoProcessoReader, cancellationToken).ConfigureAwait(false);

        return unidade.IsSuccess && localidade.IsSuccess && tipo.IsSuccess
            ? Result<ReferenciasDoNovoProcesso>.Success(new ReferenciasDoNovoProcesso(unidade.Value.Id, unidade.Value.Copia, localidade.Value!, tipo.Value!))
            : Result<ReferenciasDoNovoProcesso>.ValidationFailure([.. unidade.Errors, .. localidade.Errors, .. tipo.Errors]);
    }

    /// <summary>
    /// Sem unidade informada não há o que ler: a falta dela, as causas da localidade e a falta do tipo saem juntas,
    /// sem consulta a nenhum cadastro.
    /// </summary>
    internal static IReadOnlyList<FieldError> RecusasSemLeitura(CriarProcessoSeletivoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return
        [
            Obrigatorio(CampoUnidade, "A unidade administradora do processo seletivo é obrigatória."),
            .. LocalidadeDeclarada(command).Errors,
            .. RecusaDoTipoNaoInformado(command.TipoProcessoOrigemId),
        ];
    }

    /// <summary>
    /// A cidade da Unidade administradora descreve onde ela fica, e é copiada para o snapshot dela — não é a
    /// localidade que rege a contagem de prazos, que o processo declara por conta própria (UNI-REQ-0111). A
    /// cidade é opcional no cadastro da Unidade e exigida aqui, ao usá-la como administradora do processo,
    /// porque o snapshot a carrega (UNI-REQ-0119).
    /// </summary>
    private static async Task<Result<(Guid Id, UnidadeAdministradoraSnapshot Copia)>> UnidadeConferidaAsync(
        Guid unidadeId, IUnidadeReader unidadeReader, CancellationToken cancellationToken)
    {
        UnidadeView? unidade = await unidadeReader.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);
        Result<UnidadeView> lida = UnidadeComCidade(unidade);

        return lida.IsFailure
            ? Result<(Guid, UnidadeAdministradoraSnapshot)>.ValidationFailure(lida.Errors)
            : CopiaDaUnidade(lida.Value!);
    }

    private static Result<UnidadeView> UnidadeComCidade(UnidadeView? unidade) => unidade switch
    {
        null => RecusaDaUnidade(
            ProcessoSeletivoErrorCodes.UnidadeAdministradoraNaoEncontrada,
            "A unidade administradora deve existir no cadastro de unidades."),
        _ when string.IsNullOrWhiteSpace(unidade.CidadeCodigoIbge) => RecusaDaUnidade(
            ProcessoSeletivoErrorCodes.UnidadeAdministradoraSemCidade,
            "A unidade administradora escolhida não tem cidade cadastrada, que é obrigatória para criar o processo seletivo."),
        _ => Result<UnidadeView>.Success(unidade),
    };

    private static Result<UnidadeView> RecusaDaUnidade(string codigo, string mensagem) =>
        Result<UnidadeView>.ValidationFailure([new FieldError(CampoUnidade, new DomainError(codigo, mensagem))]);

    private static Result<(Guid Id, UnidadeAdministradoraSnapshot Copia)> CopiaDaUnidade(UnidadeView unidade)
    {
        Result<UnidadeAdministradoraSnapshot> copia = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Codigo, unidade.Slug, unidade.Nome, unidade.Tipo,
            new CidadeCopiadaDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf));

        return copia.IsFailure
            ? Result<(Guid, UnidadeAdministradoraSnapshot)>.ValidationFailure([.. copia.Errors.Select(static causa => causa with { Field = CampoUnidade })])
            : Result<(Guid, UnidadeAdministradoraSnapshot)>.Success((unidade.Id, copia.Value!));
    }

    /// <summary>
    /// A ausência é erro nomeado, e não validação de contrato: é este o código que o catálogo público publica para
    /// a causa (uniplus.selecao.processo_seletivo.localidade_ausente). Exigir os campos no validator devolveria
    /// uniplus.validacao e o consumidor perderia a página que explica a causa.
    /// </summary>
    private static Result<LocalidadeRegente> LocalidadeDeclarada(CriarProcessoSeletivoCommand command)
    {
        if (command.LocalidadeNaoDeclarada)
        {
            return Result<LocalidadeRegente>.ValidationFailure([new FieldError(CampoLocalidadeCodigoIbge, new DomainError(
                ProcessoSeletivoErrorCodes.LocalidadeAusente,
                "A localidade que rege a contagem dos prazos é obrigatória para criar o processo seletivo."))]);
        }

        Result<LocalidadeRegente> criada = LocalidadeRegente.Criar(command.LocalidadeCodigoIbge, command.LocalidadeNome, command.LocalidadeUf);

        return criada.IsFailure
            ? Result<LocalidadeRegente>.ValidationFailure(CamposDaLocalidade.Apontar(criada.Errors))
            : criada;
    }

    private static async Task<Result<TipoProcessoSnapshot>> TipoAtivoAsync(
        Guid tipoProcessoId, ITipoProcessoReader tipoProcessoReader, CancellationToken cancellationToken)
    {
        if (RecusaDoTipoNaoInformado(tipoProcessoId) is { Length: > 0 } naoInformado)
        {
            return Result<TipoProcessoSnapshot>.ValidationFailure(naoInformado);
        }

        TipoProcessoView? tipo = await tipoProcessoReader.ObterAtivoPorIdAsync(tipoProcessoId, cancellationToken).ConfigureAwait(false);

        return tipo is null
            ? Result<TipoProcessoSnapshot>.ValidationFailure([new FieldError(CampoTipo, new DomainError(
                ProcessoSeletivoErrorCodes.TipoProcessoNaoEncontradoOuInativo,
                "O tipo de processo seletivo deve existir no cadastro de tipos de processo e estar ativo."))])
            : TipoProcessoSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
    }

    private static FieldError[] RecusaDoTipoNaoInformado(Guid tipoProcessoId) =>
        tipoProcessoId == Guid.Empty ? [Obrigatorio(CampoTipo, "O tipo do processo seletivo é obrigatório.")] : [];

    private static FieldError Obrigatorio(string campo, string mensagem) =>
        new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem));
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// O que a criação do processo congela dos cadastros: a unidade administradora, a localidade que rege a
/// contagem dos prazos e o tipo de processo. Cada recusa sai na ordem em que o cliente a corrige: a unidade
/// primeiro, depois a localidade declarada e, por fim, o tipo.
/// </summary>
internal sealed record ReferenciasDoNovoProcesso(
    Guid UnidadeId,
    UnidadeAdministradoraSnapshot Unidade,
    LocalidadeRegente Localidade,
    TipoProcessoSnapshot Tipo)
{
    internal static async Task<Result<ReferenciasDoNovoProcesso>> ResolverAsync(
        CriarProcessoSeletivoCommand command,
        IUnidadeReader unidadeReader,
        ITipoProcessoReader tipoProcessoReader,
        CancellationToken cancellationToken)
    {
        Result<(UnidadeView Unidade, LocalidadeRegente Localidade)> origem = await UnidadeELocalidadeAsync(command, unidadeReader, cancellationToken)
            .ConfigureAwait(false);
        if (origem.IsFailure)
        {
            return Result<ReferenciasDoNovoProcesso>.Failure(origem.Error!);
        }

        Result<TipoProcessoSnapshot> tipo = await TipoAtivoAsync(command.TipoProcessoOrigemId, tipoProcessoReader, cancellationToken)
            .ConfigureAwait(false);

        return tipo.IsFailure
            ? Result<ReferenciasDoNovoProcesso>.Failure(tipo.Error!)
            : Montar(origem.Value.Unidade, origem.Value.Localidade, tipo.Value!);
    }

    private static async Task<Result<(UnidadeView Unidade, LocalidadeRegente Localidade)>> UnidadeELocalidadeAsync(
        CriarProcessoSeletivoCommand command, IUnidadeReader unidadeReader, CancellationToken cancellationToken)
    {
        Result<UnidadeView> unidade = await UnidadeVivaComCidadeAsync(command.UnidadeAdministradoraOrigemId, unidadeReader, cancellationToken)
            .ConfigureAwait(false);
        if (unidade.IsFailure)
        {
            return Result<(UnidadeView, LocalidadeRegente)>.Failure(unidade.Error!);
        }

        Result<LocalidadeRegente> localidade = LocalidadeDeclarada(command);

        return localidade.IsFailure
            ? Result<(UnidadeView, LocalidadeRegente)>.Failure(localidade.Error!)
            : Result<(UnidadeView, LocalidadeRegente)>.Success((unidade.Value!, localidade.Value!));
    }

    /// <summary>
    /// A cidade da Unidade administradora descreve onde ela fica, e é copiada para o snapshot dela — não é a
    /// localidade que rege a contagem de prazos, que o processo declara por conta própria (UNI-REQ-0111). A
    /// exigência de a Unidade ter cidade permanece como regra do cadastro dela, e é avaliada aqui porque o
    /// snapshot a carrega; a justificativa original, que a apoiava na contagem de prazo, deixou de valer quando
    /// a localidade passou a ser declarada.
    /// </summary>
    private static async Task<Result<UnidadeView>> UnidadeVivaComCidadeAsync(
        Guid unidadeId, IUnidadeReader unidadeReader, CancellationToken cancellationToken)
    {
        UnidadeView? unidade = await unidadeReader.ObterPorIdAsync(unidadeId, cancellationToken).ConfigureAwait(false);

        return unidade switch
        {
            null => Result<UnidadeView>.Failure(new DomainError(
                "ProcessoSeletivo.UnidadeAdministradoraNaoEncontrada",
                $"Unidade administradora {unidadeId} não encontrada ou não está mais viva.")),
            _ when string.IsNullOrWhiteSpace(unidade.CidadeCodigoIbge) => Result<UnidadeView>.Failure(new DomainError(
                "ProcessoSeletivo.UnidadeAdministradoraSemCidade",
                $"Unidade administradora {unidadeId} não tem cidade cadastrada — obrigatória para criar processo seletivo.")),
            _ => Result<UnidadeView>.Success(unidade),
        };
    }

    /// <summary>
    /// A ausência é erro nomeado, e não validação de contrato: é este o código que o catálogo público publica para
    /// a causa (uniplus.selecao.processo_seletivo.localidade_ausente). Exigir os campos no validator devolveria
    /// uniplus.validacao e o consumidor perderia a página que explica a causa.
    /// </summary>
    private static Result<LocalidadeRegente> LocalidadeDeclarada(CriarProcessoSeletivoCommand command) =>
        command.LocalidadeNaoDeclarada
            ? Result<LocalidadeRegente>.Failure(new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para criar o processo seletivo."))
            : LocalidadeRegente.Criar(command.LocalidadeCodigoIbge, command.LocalidadeNome, command.LocalidadeUf);

    private static async Task<Result<TipoProcessoSnapshot>> TipoAtivoAsync(
        Guid tipoProcessoId, ITipoProcessoReader tipoProcessoReader, CancellationToken cancellationToken)
    {
        TipoProcessoView? tipo = await tipoProcessoReader.ObterAtivoPorIdAsync(tipoProcessoId, cancellationToken).ConfigureAwait(false);

        return tipo is null
            ? Result<TipoProcessoSnapshot>.Failure(new DomainError(
                "ProcessoSeletivo.TipoProcessoNaoEncontradoOuInativo",
                $"Tipo de processo seletivo {tipoProcessoId} não encontrado ou não está ativo."))
            : TipoProcessoSnapshot.Criar(tipo.Id, tipo.Codigo, tipo.Nome);
    }

    private static Result<ReferenciasDoNovoProcesso> Montar(UnidadeView unidade, LocalidadeRegente localidade, TipoProcessoSnapshot tipo)
    {
        Result<UnidadeAdministradoraSnapshot> snapshot = UnidadeAdministradoraSnapshot.Criar(
            unidade.Sigla, unidade.Codigo, unidade.Slug, unidade.Nome, unidade.Tipo,
            new CidadeCopiadaDaUnidade(unidade.CidadeCodigoIbge, unidade.CidadeNome, unidade.CidadeUf));

        return snapshot.IsFailure
            ? Result<ReferenciasDoNovoProcesso>.Failure(snapshot.Error!)
            : Result<ReferenciasDoNovoProcesso>.Success(new ReferenciasDoNovoProcesso(unidade.Id, snapshot.Value!, localidade, tipo));
    }
}

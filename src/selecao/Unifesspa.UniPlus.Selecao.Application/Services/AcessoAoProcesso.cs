namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O ponto único em que a Seleção decide uma permissão que vale na unidade dona do processo (ADR-0078,
/// ADR-0151): as fábricas montam o resource com a unidade, o processo e o alvo, e a decisão é entregue ao ponto de
/// decisão. A permissão só vale na unidade em que foi concedida, então a decisão precisa do processo carregado — por
/// isso não cabe ao filtro de permissão do catálogo, que roda antes de saber qual processo é.
/// </summary>
public static class AcessoAoProcesso
{
    // A sensibilidade do resource é a base da decisão: vale a maior entre ela e a da permissão. O processo parte da
    // menor, de modo que vale a da permissão; a inscrição guarda dado pessoal, então parte de pessoal.

    /// <summary>O resource do processo na unidade dona; sem <paramref name="processoId"/>, o alvo é a unidade (a criação de processo).</summary>
    public static ResourceContext ResourceDoProcesso(Guid unidadeId, Guid? processoId) =>
        ResourceContext.From(ResourceTiposSelecao.ProcessoSeletivo, ClassificacaoDeProtecao.Publico, unidadeId, processoId).Value!;

    /// <summary>
    /// O resource de uma inscrição, ou de todas as do processo quando <paramref name="inscricaoId"/> é nulo. É dado
    /// pessoal por natureza, então a sensibilidade é pessoal qualquer que seja a classe do item pedido: toda decisão
    /// sobre ele entra no registro de acessos. É a única definição de que a inscrição é pessoal.
    /// </summary>
    public static ResourceContext ResourceDaInscricao(Guid unidadeId, Guid processoId, Guid? inscricaoId) =>
        DaInscricao(unidadeId, processoId, inscricaoId, fatoCodigo: null, ocorrencia: null);

    /// <summary>
    /// O resource de um campo da inscrição: o registro do acesso nomeia o fato e, no grupo que se repete, a ocorrência.
    /// </summary>
    public static ResourceContext ResourceDoCampo(
        Guid unidadeId, Guid processoId, Guid inscricaoId, string fatoCodigo, string? ocorrencia) =>
        DaInscricao(unidadeId, processoId, inscricaoId, fatoCodigo, ocorrencia);

    /// <summary>
    /// O resource de um documento da inscrição, com a sensibilidade da classificação congelada da exigência a que o
    /// arquivo responde.
    /// </summary>
    public static ResourceContext ResourceDoDocumento(
        Guid unidadeId, Guid processoId, Guid inscricaoId, Guid arquivoId, ClassificacaoDeProtecao classificacao) =>
        ResourceContext.From(
            ResourceTiposSelecao.DocumentoDaInscricao, classificacao, unidadeId, processoId, resourceId: arquivoId,
            inscricaoId: inscricaoId).Value!;

    private static ResourceContext DaInscricao(
        Guid unidadeId, Guid processoId, Guid? inscricaoId, string? fatoCodigo, string? ocorrencia) =>
        ResourceContext.From(
            ResourceTiposSelecao.Inscricao, ClassificacaoDeProtecao.Pessoal, unidadeId, processoId, resourceId: inscricaoId,
            fatoCodigo: fatoCodigo, ocorrencia: ocorrencia, inscricaoId: inscricaoId).Value!;

    /// <summary>
    /// Decide o pedido da permissão sobre o resource que a unidade dona monta. Sem unidade (processo inexistente) não
    /// há decisão a tomar sobre o resource: o acesso é negado sem consultar o ponto de decisão, que registraria como
    /// permitida uma operação sobre um resource que não existe.
    /// </summary>
    public static async Task<ResultadoDoAcesso> ExigirNaUnidadeAsync(
        IVerificadorDeAcesso verificador,
        Guid? unidadeId,
        PermissaoSelecao permissao,
        Func<Guid, ResourceContext> resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(resource);

        return unidadeId is { } unidade
            ? await verificador.ExigirAsync(permissao.Requisito, resource(unidade), cancellationToken).ConfigureAwait(false)
            : ResultadoDoAcesso.Negado;
    }

    /// <summary>
    /// Pergunta se a pessoa poderia exercer a permissão sobre o resource da unidade, sem registrar nada. Serve a mostrar
    /// ou filtrar o que ela pode fazer; quem entrega dado decide por <see cref="ExigirNaUnidadeAsync"/>.
    /// </summary>
    public static async Task<ResultadoDoAcesso> PodeNaUnidadeAsync(
        IVerificadorDeAcesso verificador,
        Guid? unidadeId,
        PermissaoSelecao permissao,
        Func<Guid, ResourceContext> resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(resource);

        return unidadeId is { } unidade
            ? await verificador.PodeAsync(permissao.Requisito, resource(unidade), cancellationToken).ConfigureAwait(false)
            : ResultadoDoAcesso.Negado;
    }

    /// <summary>
    /// Decide a permissão sobre o processo. O processo que não existe nunca é permitido: não há unidade dona, e a
    /// resposta é a mesma de quem não tem a permissão, para não revelar que o processo existe.
    /// </summary>
    public static async Task<ResultadoDoAcesso> VerificarAsync(
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processos,
        Guid processoId,
        PermissaoSelecao permissao,
        Func<Guid, ResourceContext> resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processos);

        Guid? unidade = await processos.ObterUnidadeAdministradoraAsync(processoId, cancellationToken).ConfigureAwait(false);
        return await ExigirNaUnidadeAsync(verificador, unidade, permissao, resource, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// O resource da ação interna de uma fase, na unidade que conduz a fase. Leva sempre o processo: sem ele o
    /// impedimento por inscrição aprovaria quem está inscrito.
    /// </summary>
    public static ResourceContext ResourceDaFase(UnidadeDaFase fase, Guid processoId)
    {
        ArgumentNullException.ThrowIfNull(fase);

        return ResourceContext.From(
            ResourceTiposSelecao.FaseDoProcesso, ClassificacaoDeProtecao.Publico, fase.UnidadeId, processoId,
            resourceId: fase.FaseId).Value!;
    }

    /// <summary>
    /// Decide a ação interna de uma fase na unidade que a conduz na versão publicada vigente (ADR-0079), e não na dona
    /// do processo. A fase que não se resolve não tem unidade sobre a qual decidir: o acesso é negado sem recorrer à
    /// dona e sem consultar o ponto de decisão, como no processo inexistente. A ação que por natureza não tem fase
    /// decide na dona, por <see cref="VerificarAsync"/>.
    /// </summary>
    public static async Task<ResultadoDoAcesso> ExigirNaFaseAsync(
        IVerificadorDeAcesso verificador,
        IUnidadeDaFaseNaVersaoVigente fases,
        Guid processoId,
        Guid faseId,
        PermissaoSelecao permissao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fases);

        UnidadeDaFase? fase = await fases.DaFaseAsync(processoId, faseId, cancellationToken).ConfigureAwait(false);
        return await ExigirNaUnidadeDaFaseAsync(verificador, fase, processoId, permissao, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decide a ação interna de uma etapa na unidade da fase que a contém. A etapa sem fase resolvida na versão
    /// vigente é negada como a fase que não se resolve.
    /// </summary>
    public static async Task<ResultadoDoAcesso> ExigirNaEtapaAsync(
        IVerificadorDeAcesso verificador,
        IUnidadeDaFaseNaVersaoVigente fases,
        Guid processoId,
        Guid etapaId,
        PermissaoSelecao permissao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fases);

        UnidadeDaFase? fase = await fases.DaEtapaAsync(processoId, etapaId, cancellationToken).ConfigureAwait(false);
        return await ExigirNaUnidadeDaFaseAsync(verificador, fase, processoId, permissao, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pergunta se a pessoa poderia exercer a ação da fase na unidade dela, sem registrar nada: serve a mostrar ou
    /// filtrar o que ela pode fazer, e quem entrega dado decide por <see cref="ExigirNaFaseAsync"/>.
    /// </summary>
    public static async Task<ResultadoDoAcesso> PodeNaFaseAsync(
        IVerificadorDeAcesso verificador,
        IUnidadeDaFaseNaVersaoVigente fases,
        Guid processoId,
        Guid faseId,
        PermissaoSelecao permissao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(fases);

        UnidadeDaFase? fase = await fases.DaFaseAsync(processoId, faseId, cancellationToken).ConfigureAwait(false);
        return fase is null
            ? ResultadoDoAcesso.Negado
            : await verificador.PodeAsync(permissao.Requisito, ResourceDaFase(fase, processoId), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ResultadoDoAcesso> ExigirNaUnidadeDaFaseAsync(
        IVerificadorDeAcesso verificador,
        UnidadeDaFase? fase,
        Guid processoId,
        PermissaoSelecao permissao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return fase is null
            ? ResultadoDoAcesso.Negado
            : await verificador.ExigirAsync(permissao.Requisito, ResourceDaFase(fase, processoId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decide a leitura do processo: o que já saiu de rascunho é aberto a qualquer pessoa, inclusive sem identificação,
    /// e não registra decisão; o rascunho só abre com <see cref="PermissaoSelecao.ProcessosConfigurar"/> na unidade
    /// dona. O processo que não existe nunca é permitido, e a resposta é a de quem não pode ler.
    /// </summary>
    public static async Task<ResultadoDoAcesso> VerificarLeituraAsync(
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processos,
        Guid processoId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processos);

        EstadoDoProcesso? estado = await processos.ObterEstadoAsync(processoId, cancellationToken).ConfigureAwait(false);
        if (estado is { LeituraAberta: true })
        {
            return ResultadoDoAcesso.Permitido;
        }

        return await ExigirNaUnidadeAsync(
                verificador, estado?.UnidadeId, PermissaoSelecao.ProcessosConfigurar,
                unidade => ResourceDoProcesso(unidade, processoId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Se quem lê configura o processo na unidade dona: o que separa o administrativo do que é público.</summary>
    public static async Task<bool> ConfiguraAsync(
        IVerificadorDeAcesso verificador, Guid unidadeId, Guid processoId, CancellationToken cancellationToken) =>
        await PodeNaUnidadeAsync(
                verificador, unidadeId, PermissaoSelecao.ProcessosConfigurar, unidade => ResourceDoProcesso(unidade, processoId),
                cancellationToken)
            .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;

    /// <summary>O erro com que um handler recusa o acesso que o ponto de decisão não permitiu.</summary>
    public static DomainError Recusa(ResultadoDoAcesso resultado) => resultado switch
    {
        ResultadoDoAcesso.IdentidadeIncompleta => new DomainError(
            ProcessoSeletivoErrorCodes.IdentidadeIncompleta,
            "O token não identifica o solicitante por completo; autentique-se de novo."),
        ResultadoDoAcesso.Impedido => ImpedidoPorInscricao,
        _ => new DomainError(
            ProcessoSeletivoErrorCodes.SemPermissaoNaUnidade,
            "Token autenticado, mas sem permissão para esta operação na unidade."),
    };

    /// <summary>
    /// A recusa de quem tem a permissão mas está inscrito no processo. Orienta a operação por outra pessoa da equipe e
    /// não cita a inscrição: a mensagem não revela dado da candidatura.
    /// </summary>
    public static DomainError ImpedidoPorInscricao { get; } = new(
        ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso,
        "Você não pode atuar neste processo seletivo. Peça a outra pessoa da equipe que realize a operação.");
}

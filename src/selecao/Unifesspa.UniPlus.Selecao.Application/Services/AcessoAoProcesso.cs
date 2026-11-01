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
/// ADR-0151): monta o resource com a unidade, o processo e a sensibilidade, e entrega a pergunta ao ponto de
/// decisão. A permissão só vale na unidade em que foi concedida, então a decisão precisa do processo carregado — por
/// isso não cabe ao filtro de permissão do catálogo, que roda antes de saber qual processo é.
/// </summary>
public static class AcessoAoProcesso
{
    /// <summary>
    /// Decide a permissão sobre o processo. O processo que não existe nunca é permitido: não há unidade dona, e a
    /// resposta é a mesma de quem não tem a permissão, para não revelar que o processo existe.
    /// </summary>
    /// <param name="sensibilidadeDoResource">A sensibilidade do dado alcançado, quando difere da que a permissão declara.</param>
    public static async Task<ResultadoDoAcesso> VerificarAsync(
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processos,
        Guid processoId,
        PermissaoSelecao permissao,
        string resourceTipo,
        ClassificacaoDeProtecao? sensibilidadeDoResource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processos);

        Guid? unidade = await processos.ObterUnidadeAdministradoraAsync(processoId, cancellationToken).ConfigureAwait(false);
        return await VerificarNaUnidadeAsync(
                verificador, unidade, processoId, permissao, resourceTipo, sensibilidadeDoResource, cancellationToken)
            .ConfigureAwait(false);
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

        return await VerificarNaUnidadeAsync(
                verificador, estado?.UnidadeId, processoId, PermissaoSelecao.ProcessosConfigurar, ResourceTiposSelecao.ProcessoSeletivo,
                sensibilidadeDoResource: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Se quem lê configura o processo na unidade dona: o que separa o administrativo do que é público.</summary>
    public static async Task<bool> ConfiguraAsync(
        IVerificadorDeAcesso verificador, Guid unidadeId, Guid processoId, CancellationToken cancellationToken) =>
        await VerificarNaUnidadeAsync(
                verificador, unidadeId, processoId, PermissaoSelecao.ProcessosConfigurar, ResourceTiposSelecao.ProcessoSeletivo,
                sensibilidadeDoResource: null, cancellationToken)
            .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;

    /// <summary>
    /// Decide a permissão sobre a unidade. Sem unidade (processo inexistente) não há decisão a tomar sobre o resource:
    /// o acesso é negado sem consultar o ponto de decisão, que registraria como permitida uma operação sobre um resource
    /// que não existe.
    /// <paramref name="resourceId"/> é o registro alvo quando a decisão recai sobre um só (a inscrição cujo CPF se revela).
    /// <paramref name="fatoCodigo"/> é o campo revelado, quando é o caso, e fica no registro do acesso.
    /// <paramref name="inscricaoId"/> é a inscrição a que o acesso se refere quando o resource é ela ou um item dela.
    /// <paramref name="ocorrencia"/> é a ocorrência do campo revelado, quando ele está em grupo.
    /// </summary>
    public static async Task<ResultadoDoAcesso> VerificarNaUnidadeAsync(
        IVerificadorDeAcesso verificador,
        Guid? unidadeId,
        Guid? processoId,
        PermissaoSelecao permissao,
        string resourceTipo,
        ClassificacaoDeProtecao? sensibilidadeDoResource,
        CancellationToken cancellationToken,
        Guid? resourceId = null,
        string? fatoCodigo = null,
        Guid? inscricaoId = null,
        int? ocorrencia = null)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        ResourceContext? resource = MontarResource(
            unidadeId, processoId, permissao, resourceTipo, sensibilidadeDoResource, resourceId, fatoCodigo, inscricaoId, ocorrencia);

        return resource is null
            ? ResultadoDoAcesso.Negado
            : await verificador.VerificarAsync(permissao.Requisito, resource, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pergunta se a pessoa poderia exercer a permissão na unidade, sem registrar nada. Serve a mostrar ou filtrar o que
    /// ela pode fazer; quem entrega dado decide por <see cref="VerificarNaUnidadeAsync"/>.
    /// </summary>
    public static async Task<ResultadoDoAcesso> PodeNaUnidadeAsync(
        IVerificadorDeAcesso verificador,
        Guid? unidadeId,
        Guid? processoId,
        PermissaoSelecao permissao,
        string resourceTipo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        ResourceContext? resource = MontarResource(
            unidadeId, processoId, permissao, resourceTipo, sensibilidadeDoResource: null, resourceId: null, fatoCodigo: null,
            inscricaoId: null, ocorrencia: null);

        return resource is null
            ? ResultadoDoAcesso.Negado
            : await verificador.PodeAsync(permissao.Requisito, resource, cancellationToken).ConfigureAwait(false);
    }

    private static ResourceContext? MontarResource(
        Guid? unidadeId,
        Guid? processoId,
        PermissaoSelecao permissao,
        string resourceTipo,
        ClassificacaoDeProtecao? sensibilidadeDoResource,
        Guid? resourceId,
        string? fatoCodigo,
        Guid? inscricaoId,
        int? ocorrencia)
    {
        if (!ResourceTipos.SegueAConvencao(resourceTipo))
        {
            throw new ArgumentException(
                $"O tipo de resource '{resourceTipo}' está fora da convenção kebab-case; use ResourceTiposSelecao.", nameof(resourceTipo));
        }

        if (unidadeId is null)
        {
            return null;
        }

        PermissionRequirement requisito = permissao.Requisito;
        Result<ResourceContext> resource = ResourceContext.From(
            resourceTipo, sensibilidadeDoResource ?? requisito.Sensibilidade, unidadeId, processoId, resourceId: resourceId,
            fatoCodigo: fatoCodigo, ocorrencia: ocorrencia, inscricaoId: inscricaoId);

        return resource.IsSuccess ? resource.Value : null;
    }

    /// <summary>
    /// O resource de uma inscrição, ou de todas as do processo quando <paramref name="inscricaoId"/> é nulo. É dado
    /// pessoal por natureza, então a sensibilidade é pessoal qualquer que seja a classe do item pedido: toda decisão
    /// sobre ele entra no registro de acessos.
    /// </summary>
    /// <returns>Nulo sem unidade dona, que é o processo inexistente.</returns>
    public static ResourceContext? ResourceDaInscricao(Guid? unidadeId, Guid? processoId, Guid? inscricaoId) =>
        MontarResource(
            unidadeId, processoId, PermissaoSelecao.InscricoesConsultar, ResourceTiposSelecao.Inscricao,
            ClassificacaoDeProtecao.Pessoal, inscricaoId, fatoCodigo: null, inscricaoId, ocorrencia: null);

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

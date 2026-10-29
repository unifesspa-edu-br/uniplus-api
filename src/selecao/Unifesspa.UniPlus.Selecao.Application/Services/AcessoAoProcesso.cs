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
/// ADR-0151): monta o recurso com a unidade, o processo e a sensibilidade, e entrega a pergunta ao ponto de
/// decisão. A permissão só vale na unidade em que foi concedida, então a decisão precisa do processo carregado — por
/// isso não cabe ao filtro de permissão do catálogo, que roda antes de saber qual processo é.
/// </summary>
public static class AcessoAoProcesso
{
    /// <summary>Tipo do recurso das operações sobre o processo seletivo.</summary>
    public const string RecursoProcesso = "processo-seletivo";

    /// <summary>Tipo do recurso das operações sobre as inscrições do processo.</summary>
    public const string RecursoInscricao = "inscricao";

    /// <summary>Tipo do recurso das operações sobre os documentos das inscrições.</summary>
    public const string RecursoDocumento = "documento-da-inscricao";

    /// <summary>
    /// Decide a permissão sobre o processo. O processo que não existe nunca é permitido: não há unidade dona, e a
    /// resposta é a mesma de quem não tem a permissão, para não revelar que o processo existe.
    /// </summary>
    /// <param name="sensibilidadeDoRecurso">A sensibilidade do dado alcançado, quando difere da que a permissão declara.</param>
    public static async Task<ResultadoDoAcesso> VerificarAsync(
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processos,
        Guid processoId,
        PermissaoSelecao permissao,
        string recursoTipo,
        ClassificacaoDeProtecao? sensibilidadeDoRecurso,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processos);

        Guid? unidade = await processos.ObterUnidadeAdministradoraAsync(processoId, cancellationToken).ConfigureAwait(false);
        return await VerificarNaUnidadeAsync(
                verificador, unidade, processoId, permissao, recursoTipo, sensibilidadeDoRecurso, cancellationToken)
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
                verificador, estado?.UnidadeId, processoId, PermissaoSelecao.ProcessosConfigurar, RecursoProcesso,
                sensibilidadeDoRecurso: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Se quem lê configura o processo na unidade dona: o que separa o administrativo do que é público.</summary>
    public static async Task<bool> ConfiguraAsync(
        IVerificadorDeAcesso verificador, Guid unidadeId, Guid processoId, CancellationToken cancellationToken) =>
        await VerificarNaUnidadeAsync(
                verificador, unidadeId, processoId, PermissaoSelecao.ProcessosConfigurar, RecursoProcesso,
                sensibilidadeDoRecurso: null, cancellationToken)
            .ConfigureAwait(false) == ResultadoDoAcesso.Permitido;

    /// <summary>
    /// Decide a permissão sobre a unidade. Sem unidade (processo inexistente) não há decisão a tomar sobre o recurso:
    /// a consulta sem recurso serve só para distinguir quem não foi identificado, e o resto é negado.
    /// </summary>
    public static async Task<ResultadoDoAcesso> VerificarNaUnidadeAsync(
        IVerificadorDeAcesso verificador,
        Guid? unidadeId,
        Guid? processoId,
        PermissaoSelecao permissao,
        string recursoTipo,
        ClassificacaoDeProtecao? sensibilidadeDoRecurso,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        PermissionRequirement requisito = permissao.Requisito;
        Result<ResourceContext>? recurso = unidadeId is null
            ? null
            : ResourceContext.From(recursoTipo, sensibilidadeDoRecurso ?? requisito.Sensibilidade, unidadeId, processoId);

        if (recurso?.IsSuccess != true)
        {
            ResultadoDoAcesso semRecurso = await verificador.VerificarAsync(requisito, null, cancellationToken).ConfigureAwait(false);
            return semRecurso == ResultadoDoAcesso.IdentidadeIncompleta ? semRecurso : ResultadoDoAcesso.Negado;
        }

        return await verificador.VerificarAsync(requisito, recurso.Value, cancellationToken).ConfigureAwait(false);
    }

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

namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Providência sobre uma concessão: o Encarregado de dados pede, com fundamento, a retirada ou a revisão do acesso, e a
/// gestão de acessos ou o Representante do controlador responde com motivo ou atende pela revogação vinculada
/// (UNI-REQ-0375, UNI-REQ-0376, ADR-0164).
/// </summary>
/// <remarks>
/// <para>
/// <b>Só os atos se gravam.</b> O estado é aberta, respondida ou atendida. A providência aberta cuja concessão deixou de
/// valer, por revogação feita por outro caminho ou por vencimento, aparece como prejudicada na leitura
/// (<see cref="SituacaoEm"/>), sem gravação e sem rotina: a revogação por outro caminho não conhece a providência.
/// </para>
/// <para>
/// <b>Registro de auditoria.</b> A providência não se edita, não se exclui e não tem soft delete; o desfecho é escrito uma
/// vez. Quem chama trava a concessão antes de qualquer ato e lê a providência e o instante depois da trava.
/// </para>
/// </remarks>
public sealed class Providencia : EntityBase
{
    public Guid ConcessaoId { get; private set; }

    public TipoDaProvidencia Tipo { get; private set; }

    public string Fundamento { get; private set; } = string.Empty;

    public EstadoDaProvidencia Estado { get; private set; }

    public DateTimeOffset AbertaEm { get; private set; }

    public string SolicitadaPorEmissor { get; private set; } = string.Empty;

    public string SolicitadaPorSub { get; private set; } = string.Empty;

    public string? SolicitadaPorNome { get; private set; }

    /// <summary>
    /// O e-mail verificado de quem solicitou, em minúsculas, gravado na abertura só para os avisos da resposta e do
    /// atendimento; nulo quando o token não o trouxe verificado. Nunca sai na API nem na trilha.
    /// </summary>
    public string? SolicitadaPorEmail { get; private set; }

    /// <summary>O instante da resposta ou do atendimento; nulo enquanto aberta.</summary>
    public DateTimeOffset? DesfechoEm { get; private set; }

    public string? DesfechoPorEmissor { get; private set; }

    public string? DesfechoPorSub { get; private set; }

    public string? DesfechoPorNome { get; private set; }

    public string? MotivoDoDesfecho { get; private set; }

    public Identidade SolicitadaPor => new(SolicitadaPorEmissor, SolicitadaPorSub, SolicitadaPorNome);

    /// <summary>Quem respondeu ou atendeu; nulo enquanto aberta.</summary>
    public Identidade? DesfechoPor =>
        DesfechoPorEmissor is null || DesfechoPorSub is null ? null : new(DesfechoPorEmissor, DesfechoPorSub, DesfechoPorNome);

    // Construtor de materialização do EF Core.
    private Providencia()
    {
    }

    /// <summary>
    /// Abre a providência sobre a concessão. As recusas por estado saem antes das de dados: concessão que não está ativa,
    /// depois providência já aberta para ela; só então o tipo e o fundamento, que acumulam (ADR-0125). Grava quem
    /// solicitou como o token o traz e, se verificado, o e-mail dele.
    /// </summary>
    /// <param name="concessao">A concessão alvo, lida sob a trava compartilhada.</param>
    /// <param name="abertaNaConcessao">A providência ainda aberta sobre a mesma concessão, lida sob a mesma trava; nula se não há.</param>
    /// <param name="solicitacao">O tipo e o fundamento pedidos.</param>
    /// <param name="solicitante">Quem pede, como o token o traz.</param>
    /// <param name="instante">O instante lido depois da trava.</param>
    public static Result<Providencia> Abrir(
        Concessao concessao, Providencia? abertaNaConcessao, SolicitacaoDaProvidencia solicitacao, PessoaNoToken solicitante, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(solicitacao);
        ArgumentNullException.ThrowIfNull(solicitante);

        return RecusaDaAbertura(concessao, abertaNaConcessao, instante) is { } recusa
            ? Result<Providencia>.Failure(recusa)
            : Nova(concessao.Id, solicitacao, solicitante, instante);
    }

    /// <summary>
    /// Responde com motivo, sem revogar. Recusas, uma de cada vez e nesta ordem: providência já respondida ou atendida,
    /// concessão que não está ativa no instante do ato, quem responde impedido; só então o motivo.
    /// </summary>
    /// <param name="concessao">A concessão alvo, lida sob a trava.</param>
    /// <param name="ato">Quem responde e o instante lido depois da trava.</param>
    /// <param name="motivo">Por que a providência não leva à revogação.</param>
    public Result Responder(Concessao concessao, AtoDoAgente ato, string? motivo)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(ato);

        if (RecusaDoDesfecho(concessao, ato) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        return Concessao.NormalizarMotivo(motivo) is { } motivoLido
            ? RegistrarDesfecho(EstadoDaProvidencia.Respondida, ato, motivoLido)
            : Result.ValidationFailure([MotivoInvalido()]);
    }

    /// <summary>
    /// Atende pela revogação vinculada, que segue a revogação comum. Depois das mesmas recusas da resposta, o e-mail
    /// digitado e o motivo acumulam; só então a concessão é revogada e a providência passa a atendida. A regra do mínimo
    /// de gestores de acessos é de quem chama, que a aplica em volta deste ato.
    /// </summary>
    /// <param name="concessao">A concessão alvo, travada para alterar.</param>
    /// <param name="ato">Quem atende e o instante lido depois da trava.</param>
    /// <param name="declaracao">O motivo e o e-mail do beneficiário digitado.</param>
    public Result Atender(Concessao concessao, AtoDoAgente ato, DeclaracaoDoAtendimento declaracao)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(declaracao);

        if (RecusaDoDesfecho(concessao, ato) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        List<FieldError> erros = [];
        declaracao.Validar(concessao, erros);

        return erros.Count > 0 ? Result.ValidationFailure(erros) : RevogarEAtender(concessao, ato, declaracao.MotivoLido!);
    }

    /// <summary>A situação no instante: a providência aberta cuja concessão já não está ativa aparece como prejudicada.</summary>
    public SituacaoDaProvidencia SituacaoEm(Concessao concessao, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(concessao);
        ExigirAConcessaoDaProvidencia(concessao);

        return Estado switch
        {
            EstadoDaProvidencia.Aberta when !concessao.EstaAtiva(instante) => SituacaoDaProvidencia.Prejudicada,
            EstadoDaProvidencia.Aberta => SituacaoDaProvidencia.Aberta,
            EstadoDaProvidencia.Respondida => SituacaoDaProvidencia.Respondida,
            _ => SituacaoDaProvidencia.Atendida,
        };
    }

    /// <summary>A pessoa não responde nem atende esta providência: é quem a solicitou ou o beneficiário da concessão.</summary>
    public bool ImpedeResposta(Identidade pessoa, Concessao concessao)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        ArgumentNullException.ThrowIfNull(concessao);

        return pessoa.Equals(SolicitadaPor) || pessoa.Equals(concessao.Beneficiario);
    }

    /// <summary>A recusa do motivo da resposta e do atendimento, no campo <c>motivo</c>.</summary>
    internal static FieldError MotivoInvalido() =>
        new("motivo", new DomainError(
            ProvidenciaErrorCodes.MotivoInvalido, $"O motivo é obrigatório e deve ter até {Concessao.MotivoMaxLength} caracteres."));

    private static DomainError? RecusaDaAbertura(Concessao concessao, Providencia? abertaNaConcessao, DateTimeOffset instante)
    {
        if (!concessao.EstaAtiva(instante))
        {
            return ConcessaoNaoAtiva();
        }

        return abertaNaConcessao is { Estado: EstadoDaProvidencia.Aberta } ? ProvidenciaErros.JaHaAberta() : null;
    }

    private DomainError? RecusaDoDesfecho(Concessao concessao, AtoDoAgente ato)
    {
        ExigirAConcessaoDaProvidencia(concessao);

        DomainError? recusa = Estado switch
        {
            EstadoDaProvidencia.Respondida => new(ProvidenciaErrorCodes.JaRespondida, "A providência já foi respondida."),
            EstadoDaProvidencia.Atendida => new(ProvidenciaErrorCodes.JaAtendida, "A providência já foi atendida."),
            _ when !concessao.EstaAtiva(ato.Instante) => ConcessaoNaoAtiva(),
            _ => null,
        };

        return recusa ?? RecusaDoImpedido(concessao, ato.Agente);
    }

    private DomainError? RecusaDoImpedido(Concessao concessao, Identidade quemAge)
    {
        if (quemAge.Equals(SolicitadaPor))
        {
            return new DomainError(ProvidenciaErrorCodes.SolicitanteNaoResponde, "Quem solicitou a providência não a responde nem a atende.");
        }

        return quemAge.Equals(concessao.Beneficiario)
            ? new DomainError(ProvidenciaErrorCodes.BeneficiarioNaoResponde, "Ninguém responde providência sobre a própria concessão.")
            : null;
    }

    private static DomainError ConcessaoNaoAtiva() =>
        new(ProvidenciaErrorCodes.ConcessaoNaoAtiva, "A concessão da providência já não está ativa: foi revogada ou venceu.");

    private Result RevogarEAtender(Concessao concessao, AtoDoAgente ato, string motivo)
    {
        Result revogada = concessao.Revogar(ato, motivo);

        return revogada.IsFailure ? revogada : RegistrarDesfecho(EstadoDaProvidencia.Atendida, ato, motivo);
    }

    private Result RegistrarDesfecho(EstadoDaProvidencia estado, AtoDoAgente ato, string motivo)
    {
        Estado = estado;
        DesfechoEm = ato.Instante;
        DesfechoPorEmissor = ato.Agente.Emissor;
        DesfechoPorSub = ato.Agente.Sub;
        DesfechoPorNome = ato.Agente.Nome;
        MotivoDoDesfecho = motivo;

        return Result.Success();
    }

    private void ExigirAConcessaoDaProvidencia(Concessao concessao)
    {
        if (concessao.Id != ConcessaoId)
        {
            throw new InvalidOperationException($"A concessão {concessao.Id} não é a da providência {Id}, que é a {ConcessaoId}.");
        }
    }

    // O tipo e o fundamento acumulam no mesmo lote (ADR-0125); só a solicitação inteira válida abre a providência.
    private static Result<Providencia> Nova(Guid concessaoId, SolicitacaoDaProvidencia solicitacao, PessoaNoToken solicitante, DateTimeOffset instante)
    {
        List<FieldError> erros = [];
        solicitacao.Validar(erros);

        return erros.Count > 0
            ? Result<Providencia>.ValidationFailure(erros)
            : Result<Providencia>.Success(Aberta(concessaoId, solicitacao, solicitante, instante));
    }

    private static Providencia Aberta(Guid concessaoId, SolicitacaoDaProvidencia solicitacao, PessoaNoToken solicitante, DateTimeOffset instante) =>
        new()
        {
            ConcessaoId = concessaoId,
            Tipo = solicitacao.TipoLido!.Value,
            Fundamento = solicitacao.FundamentoLido!,
            Estado = EstadoDaProvidencia.Aberta,
            AbertaEm = instante,
            SolicitadaPorEmissor = solicitante.Identidade.Emissor,
            SolicitadaPorSub = solicitante.Identidade.Sub,
            SolicitadaPorNome = solicitante.Nome,
            SolicitadaPorEmail = solicitante.EmailVerificado ? PedidoDeConcessao.EmailNormalizado(solicitante.Email) : null,
        };
}

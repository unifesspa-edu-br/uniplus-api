namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pedido de concessão de um perfil de acesso a uma pessoa, num escopo. Toda concessão nasce de um
/// pedido aprovado: o gestor de acessos aprova a adequação funcional e, quando o perfil reúne
/// permissão pessoal ou sensível, a proteção de dados aprova depois, a adequação à proteção de dados.
/// </summary>
/// <remarks>
/// <para>
/// <b>Só a identidade do token é gravada.</b> Nunca CPF; o nome e o e-mail do beneficiário existem para
/// que o aprovador saiba quem é a pessoa (aprovar exige julgar a pessoa), e o nome vem do token dele, no
/// aceite. O e-mail do solicitante é o verificado do token e serve só para avisá-lo do desfecho. A sensibilidade
/// do perfil também não se grava: deriva do catálogo.
/// </para>
/// <para>
/// <b>Nenhuma decisão acontece sem a identidade completa do beneficiário.</b> O pedido para outra
/// pessoa espera o aceite dela, que grava emissor, <c>sub</c> e nome do token.
/// </para>
/// <para>
/// <b>As decisões só se acrescentam</b> e guardam o retrato do que o aprovador avaliou. Os estados
/// terminais não voltam.
/// </para>
/// </remarks>
public sealed partial class PedidoDeConcessao : EntityBase
{
    public const int JustificativaMinLength = 10;
    public const int JustificativaMaxLength = 2000;
    public const int MotivoMaxLength = 500;
    public const int EmailMaxLength = 254;

    /// <summary>As situações em que o pedido ainda pode avançar: o mesmo pedido não se repete enquanto numa delas.</summary>
    public static readonly IReadOnlyList<SituacaoDoPedido> SituacoesAtivas =
    [
        SituacaoDoPedido.AguardandoDocumento,
        SituacaoDoPedido.AguardandoBeneficiario,
        SituacaoDoPedido.AguardandoGestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados,
    ];

    private readonly List<DecisaoDoPedido> _decisoes = [];
    private readonly List<AnexoDoPedido> _anexos = [];

    public string SolicitanteEmissor { get; private set; } = string.Empty;

    public string SolicitanteSub { get; private set; } = string.Empty;

    public string? SolicitanteNome { get; private set; }

    /// <summary>E-mail verificado do token do solicitante, em minúsculas; destino do aviso do desfecho. Nulo no pedido anterior ao registro.</summary>
    public string? SolicitanteEmail { get; private set; }

    /// <summary>E-mail institucional do beneficiário, em minúsculas; é como o pedido o encontra até ele aceitar.</summary>
    public string BeneficiarioEmail { get; private set; } = string.Empty;

    public string? BeneficiarioEmissor { get; private set; }

    public string? BeneficiarioSub { get; private set; }

    public string? BeneficiarioNome { get; private set; }

    /// <summary>Instante do aceite do beneficiário; nulo antes dele e no pedido para si mesmo que não passa pelo aceite.</summary>
    public DateTimeOffset? BeneficiarioAceitouEm { get; private set; }

    public Guid PerfilId { get; private set; }

    /// <summary>Unidade do escopo; nula só no perfil de governança, de escopo institucional.</summary>
    public Guid? UnidadeId { get; private set; }

    public string Justificativa { get; private set; } = string.Empty;

    public SituacaoDoPedido Situacao { get; private set; }

    /// <summary>O fim de vigência que o solicitante sugere ao gestor; nulo, sem sugestão. Só informa: quem define é o gestor.</summary>
    public DateTimeOffset? ValidaAteSugerida { get; private set; }

    /// <summary>O fim de vigência que o gestor definiu ao aprovar e que a concessão herda; nulo, sem prazo.</summary>
    public DateTimeOffset? ValidaAte { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    /// <summary>A concessão que a última aprovação gerou; nula até lá.</summary>
    public Guid? ConcessaoId { get; private set; }

    public IReadOnlyList<DecisaoDoPedido> Decisoes => _decisoes;

    public IReadOnlyList<AnexoDoPedido> Anexos => _anexos;

    public Identidade Solicitante => new(SolicitanteEmissor, SolicitanteSub, SolicitanteNome);

    /// <summary>A identidade do beneficiário; nula enquanto ele não aceitou.</summary>
    public Identidade? Beneficiario => BeneficiarioEmissor is { } emissor && BeneficiarioSub is { } sub
        ? new Identidade(emissor, sub, BeneficiarioNome)
        : null;

    public EscopoDaConcessao Escopo => UnidadeId is { } unidade
        ? EscopoDaConcessao.DaUnidade(unidade)
        : EscopoDaConcessao.DaInstituicao();

    public bool Ativo => SituacoesAtivas.Contains(Situacao);

    /// <summary>A etapa que o pedido aguarda; nula quando ele não espera aprovador.</summary>
    public EtapaDoPedido? EtapaAtual => Situacao switch
    {
        SituacaoDoPedido.AguardandoGestor => EtapaDoPedido.Gestor,
        SituacaoDoPedido.AguardandoProtecaoDeDados => EtapaDoPedido.ProtecaoDeDados,
        _ => null,
    };

    // Construtor de materialização do EF Core.
    private PedidoDeConcessao()
    {
    }

    /// <summary>Cancela o pedido, só antes da primeira decisão.</summary>
    public Result Cancelar(DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result.Failure(SituacaoInvalida("O pedido só se cancela antes da primeira decisão."));
        }

        Encerrar(SituacaoDoPedido.Cancelado, instante);
        return Result.Success();
    }

    /// <summary>Registra a concessão que a última aprovação gerou.</summary>
    public void RegistrarConcessao(Guid concessaoId)
    {
        if (Situacao != SituacaoDoPedido.Aprovado)
        {
            throw new InvalidOperationException("Só o pedido aprovado tem concessão.");
        }

        ConcessaoId = concessaoId;
    }

    private bool PodeSerAlteradoPeloSolicitante =>
        Situacao is SituacaoDoPedido.AguardandoDocumento or SituacaoDoPedido.AguardandoBeneficiario or SituacaoDoPedido.AguardandoGestor
        && _decisoes.Count == 0;

    private void Encerrar(SituacaoDoPedido situacao, DateTimeOffset instante)
    {
        Situacao = situacao;
        EncerradoEm = instante;
    }

    // O caractere nulo cabe no tamanho, mas a coluna textual do banco não o armazena.
    private static string? NormalizarTexto(string? texto, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string normalizado = texto.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length < min || normalizado.Length > max
            ? null
            : normalizado;
    }

    private static DomainError SituacaoInvalida(string mensagem) => new(PedidoErrorCodes.SituacaoInvalida, mensagem);
}

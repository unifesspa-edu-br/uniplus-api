namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessão de um perfil de acesso a uma pessoa, num escopo.
/// </summary>
/// <remarks>
/// <para>
/// <b>A concessão não se edita.</b> O único estado novo que ela admite é a revogação, escrita
/// uma vez; sem soft delete e sem histórico por entidade, porque a trilha deriva das próprias
/// colunas.
/// </para>
/// <para>
/// <b>Estar ativa não é gravado.</b> Deriva da revogação e da vigência a cada leitura
/// (<see cref="EstaAtiva"/>): gravar o resultado o deixaria envelhecer quando a vigência vence.
/// </para>
/// <para>
/// <b>Só a identidade do token e o nome de exibição de quem age são gravados</b>, nunca o CPF. O nome é o que a trilha
/// mostra, gravado no instante da ação.
/// </para>
/// </remarks>
public sealed class Concessao : EntityBase
{
    private const int MotivoMaxLength = 500;

    public string BeneficiarioEmissor { get; private set; } = string.Empty;

    public string BeneficiarioSub { get; private set; } = string.Empty;

    /// <summary>Nome do beneficiário no instante da concessão; nulo quando a origem não o tinha.</summary>
    public string? BeneficiarioNome { get; private set; }

    /// <summary>
    /// E-mail institucional do beneficiário, em minúsculas, que a revogação pede de volta para confirmar a pessoa; nulo
    /// quando a origem não o tinha. Nunca sai na listagem.
    /// </summary>
    public string? BeneficiarioEmail { get; private set; }

    public Guid PerfilId { get; private set; }

    public TipoDeEscopo EscopoTipo { get; private set; }

    /// <summary>Unidade do escopo; nula quando o escopo é a instituição.</summary>
    public Guid? UnidadeId { get; private set; }

    public TipoDeOrigem OrigemTipo { get; private set; }

    public Guid OrigemId { get; private set; }

    public DateTimeOffset ConcedidaEm { get; private set; }

    public string ConcedidaPorEmissor { get; private set; } = string.Empty;

    public string ConcedidaPorSub { get; private set; } = string.Empty;

    public string? ConcedidaPorNome { get; private set; }

    /// <summary>Fim da vigência, quando a concessão é por prazo; nulo quando não expira.</summary>
    public DateTimeOffset? ValidaAte { get; private set; }

    public DateTimeOffset? RevogadaEm { get; private set; }

    public string? RevogadaPorEmissor { get; private set; }

    public string? RevogadaPorSub { get; private set; }

    public string? RevogadaPorNome { get; private set; }

    public string? MotivoRevogacao { get; private set; }

    // Construtor de materialização do EF Core.
    private Concessao()
    {
    }

    /// <summary>
    /// Concede o perfil. Chamado só pelo tratamento do pedido aprovado e da instalação, nunca por
    /// endpoint. Acumula toda violação independente (ADR-0125). A concessão ativa repetida é do
    /// índice único, traduzido por quem grava.
    /// </summary>
    public static Result<Concessao> Conceder(
        Identidade beneficiario,
        Perfil perfil,
        EscopoDaConcessao escopo,
        OrigemDaConcessao origem,
        Identidade concedidaPor,
        DateTimeOffset instante,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset? validaAte = null,
        string? beneficiarioEmail = null)
    {
        ArgumentNullException.ThrowIfNull(beneficiario);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(concedidaPor);
        ArgumentNullException.ThrowIfNull(catalogo);

        List<FieldError> erros = [];

        if (!perfil.Ativo)
        {
            erros.Add(new("perfil", new DomainError(
                ConcessaoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita concessão nova.")));
        }

        string? incoerencia = ConferirEscopo(perfil, escopo, catalogo);
        if (incoerencia is not null)
        {
            erros.Add(new("escopo", new DomainError(ConcessaoErrorCodes.EscopoInvalido, incoerencia)));
        }

        if (validaAte is not null && !perfil.AdmitePrazo)
        {
            erros.Add(new("validaAte", new DomainError(
                ConcessaoErrorCodes.ValidadeInvalida,
                "Perfil de governança não admite concessão com prazo: gestor de acessos e proteção de dados não têm vigência limitada.")));
        }
        else if (validaAte is { } fim && fim <= instante)
        {
            erros.Add(new("validaAte", new DomainError(
                ConcessaoErrorCodes.ValidadeInvalida,
                "O fim da vigência deve ser posterior ao instante da concessão.")));
        }

        if (erros.Count > 0)
        {
            return Result<Concessao>.ValidationFailure(erros);
        }

        return Result<Concessao>.Success(new Concessao
        {
            BeneficiarioEmissor = beneficiario.Emissor,
            BeneficiarioSub = beneficiario.Sub,
            BeneficiarioNome = beneficiario.Nome,
            BeneficiarioEmail = PedidoDeConcessao.EmailNormalizado(beneficiarioEmail),
            PerfilId = perfil.Id,
            EscopoTipo = escopo.Tipo,
            UnidadeId = escopo.UnidadeId,
            OrigemTipo = origem.Tipo,
            OrigemId = origem.Id,
            ConcedidaEm = instante,
            ConcedidaPorEmissor = concedidaPor.Emissor,
            ConcedidaPorSub = concedidaPor.Sub,
            ConcedidaPorNome = concedidaPor.Nome,
            ValidaAte = validaAte,
        });
    }

    /// <summary>
    /// O e-mail digitado para confirmar a revogação é o do beneficiário: compara sem diferenciar maiúsculas nem espaços nas
    /// pontas. A concessão sem e-mail gravado não tem com o que conferir e nunca confirma.
    /// </summary>
    public bool ConfirmaEmailDoBeneficiario(string? confirmacaoEmail) =>
        BeneficiarioEmail is not null
        && string.Equals(BeneficiarioEmail, confirmacaoEmail?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Ativa quando não foi revogada e a vigência, se houver, ainda não venceu.</summary>
    public bool EstaAtiva(DateTimeOffset agora) =>
        RevogadaEm is null && (ValidaAte is null || ValidaAte > agora);

    /// <summary>
    /// Revoga a concessão, com efeito imediato. A recusa por concessão já revogada sai antes da
    /// do motivo: com a concessão encerrada, corrigir o motivo não leva a nada.
    /// </summary>
    public Result Revogar(Identidade revogadaPor, string? motivo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(revogadaPor);

        if (RevogadaEm is not null)
        {
            return Result.Failure(new DomainError(
                ConcessaoErrorCodes.JaRevogada,
                "A concessão já foi revogada."));
        }

        string? motivoNormalizado = NormalizarMotivo(motivo);
        if (motivoNormalizado is null)
        {
            return Result.ValidationFailure([new FieldError("motivo", new DomainError(
                ConcessaoErrorCodes.MotivoInvalido,
                $"O motivo da revogação é obrigatório e deve ter até {MotivoMaxLength} caracteres."))]);
        }

        RevogadaEm = instante;
        RevogadaPorEmissor = revogadaPor.Emissor;
        RevogadaPorSub = revogadaPor.Sub;
        RevogadaPorNome = revogadaPor.Nome;
        MotivoRevogacao = motivoNormalizado;

        return Result.Success();
    }

    // Também confere o escopo do pedido, que não pode pedir o que a concessão recusaria. O escopo
    // admitido é o alcance do perfil, derivado do catálogo: o perfil de permissões institucionais só
    // vale na instituição, e o de permissões de unidade só numa unidade, sem herança entre as duas
    // (ADR-0079).
    internal static string? ConferirEscopo(Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo)
    {
        bool coerente = escopo.Tipo switch
        {
            TipoDeEscopo.Unidade => escopo.UnidadeId is { } id && id != Guid.Empty,
            TipoDeEscopo.Instituicao => escopo.UnidadeId is null,
            _ => false,
        };

        if (!coerente)
        {
            return "O escopo da unidade exige a unidade, e o escopo da instituição não admite unidade.";
        }

        return perfil.Alcance(catalogo) switch
        {
            null => "O perfil mistura permissões de unidade e institucionais e não tem escopo que o conceda.",
            TipoDeEscopo admitido when admitido == escopo.Tipo => null,
            TipoDeEscopo.Instituicao => "O perfil só pode ser concedido no escopo da instituição.",
            _ => "O perfil só pode ser concedido no escopo de uma unidade.",
        };
    }

    // O caractere nulo cabe no tamanho, mas a coluna textual do banco não o armazena: sem esta
    // recusa o valor só falharia ao gravar e responderia 500 em vez da validação de domínio.
    private static string? NormalizarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return null;
        }

        string normalizado = motivo.Trim();

        return normalizado.Contains('\0', StringComparison.Ordinal) || normalizado.Length > MotivoMaxLength
            ? null
            : normalizado;
    }
}

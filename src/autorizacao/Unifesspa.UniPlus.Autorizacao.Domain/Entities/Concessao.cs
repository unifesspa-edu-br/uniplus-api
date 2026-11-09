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

    /// <summary>
    /// A versão do perfil que a concessão alcança: a em vigor quando foi concedida, ou a que a substituiu sem acrescentar
    /// permissão. Nula se, e somente se, o perfil é de governança, que não tem versão.
    /// </summary>
    public Guid? VersaoId { get; private set; }

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

    /// <summary>
    /// A janela de migração que revogou a concessão, pelo sistema; nula quando a revogação foi de uma pessoa ou a
    /// concessão está ativa. A pessoa que recusou a migração fica na trilha da janela.
    /// </summary>
    public Guid? RevogadaPelaJanelaId { get; private set; }

    public Identidade Beneficiario => new(BeneficiarioEmissor, BeneficiarioSub, BeneficiarioNome);

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

        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo), .. ErrosDaValidade(perfil, validaAte, instante)];

        return erros.Count > 0
            ? Result<Concessao>.ValidationFailure(erros)
            : Result<Concessao>.Success(Nova(beneficiario, beneficiarioEmail, perfil, escopo, origem).ConcedidaPor(concedidaPor, instante, validaAte));
    }

    /// <summary>
    /// Passa a concessão para a versão que substituiu a que ela alcançava, sem acréscimo de permissão: troca só a versão e
    /// mantém pessoa, escopo, validade e origem. Quem chama trava o perfil e as concessões da versão que sai, então a
    /// que está revogada, é de governança ou segue outra versão é defeito de quem chama.
    /// </summary>
    /// <param name="versaoNova">A versão em vigor que a concessão passa a alcançar.</param>
    /// <param name="versaoEsperada">A versão que a concessão deve estar alcançando agora.</param>
    public void MigrarParaVersao(Guid versaoNova, Guid versaoEsperada)
    {
        if (RevogadaEm is not null || VersaoId != versaoEsperada)
        {
            throw new InvalidOperationException(
                $"A concessão {Id} não pode migrar da versão {versaoEsperada}: está revogada, é de governança ou alcança a versão {VersaoId}.");
        }

        VersaoId = versaoNova;
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

    /// <summary>Estava ativa no instante, mesmo que tenha sido revogada ou vencido depois dele.</summary>
    public bool EstavaAtivaEm(DateTimeOffset instante) =>
        ConcedidaEm <= instante && (RevogadaEm is null || RevogadaEm > instante) && (ValidaAte is null || ValidaAte > instante);

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

    /// <summary>
    /// Revoga a concessão pelo sistema, em nome da janela de migração que a encerrou ou a recusou. Quem chama trava a
    /// concessão e confere que não está revogada.
    /// </summary>
    public void RevogarPelaMigracao(Guid janelaId, string motivo, DateTimeOffset instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        if (RevogadaEm is not null)
        {
            throw new InvalidOperationException($"A concessão {Id} já está revogada e não pode ser revogada pela migração.");
        }

        RevogadaEm = instante;
        RevogadaPelaJanelaId = janelaId;
        MotivoRevogacao = motivo;
    }

    private static Concessao Nova(
        Identidade beneficiario, string? beneficiarioEmail, Perfil perfil, EscopoDaConcessao escopo, OrigemDaConcessao origem) =>
        new()
        {
            BeneficiarioEmissor = beneficiario.Emissor,
            BeneficiarioSub = beneficiario.Sub,
            BeneficiarioNome = beneficiario.Nome,
            BeneficiarioEmail = PedidoDeConcessao.EmailNormalizado(beneficiarioEmail),
            PerfilId = perfil.Id,
            VersaoId = perfil.VersaoVigente?.Id,
            EscopoTipo = escopo.Tipo,
            UnidadeId = escopo.UnidadeId,
            OrigemTipo = origem.Tipo,
            OrigemId = origem.Id,
        };

    private Concessao ConcedidaPor(Identidade concedidaPor, DateTimeOffset instante, DateTimeOffset? validaAte)
    {
        ConcedidaEm = instante;
        ConcedidaPorEmissor = concedidaPor.Emissor;
        ConcedidaPorSub = concedidaPor.Sub;
        ConcedidaPorNome = concedidaPor.Nome;
        ValidaAte = validaAte;

        return this;
    }

    private static IEnumerable<FieldError> ErrosDoEscopo(Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo)
    {
        string? incoerencia = ConferirEscopo(perfil, escopo, catalogo);
        if (incoerencia is not null)
        {
            yield return new("escopo", new DomainError(ConcessaoErrorCodes.EscopoInvalido, incoerencia));
        }
    }

    private static IEnumerable<FieldError> ErrosDaValidade(Perfil perfil, DateTimeOffset? validaAte, DateTimeOffset instante)
    {
        if (validaAte is not null && !perfil.AdmitePrazo)
        {
            yield return new("validaAte", new DomainError(
                ConcessaoErrorCodes.ValidadeInvalida,
                "Perfil de governança não admite concessão com prazo: gestor de acessos e proteção de dados não têm vigência limitada."));
        }
        else if (validaAte is { } fim && fim <= instante)
        {
            yield return new("validaAte", new DomainError(
                ConcessaoErrorCodes.ValidadeInvalida,
                "O fim da vigência deve ser posterior ao instante da concessão."));
        }
    }

    private static IEnumerable<FieldError> ErrosDoPerfil(Perfil perfil)
    {
        if (!perfil.Ativo)
        {
            yield return new("perfil", new DomainError(
                ConcessaoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita concessão nova."));
        }

        if (!perfil.TemComposicaoEmVigor)
        {
            yield return new("perfil", new DomainError(
                ConcessaoErrorCodes.PerfilSemVersaoEmVigor,
                "O perfil de acesso não tem versão em vigor e não aceita concessão."));
        }
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

        // Sem composição em vigor a recusa é a do perfil sem versão; o escopo não tem o que conferir.
        return perfil.TemComposicaoEmVigor ? ConferirAlcance(perfil, escopo, catalogo) : null;
    }

    private static string? ConferirAlcance(Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo) =>
        perfil.Alcance(catalogo) switch
        {
            null => "O perfil mistura permissões de unidade e institucionais e não tem escopo que o conceda.",
            TipoDeEscopo admitido when admitido == escopo.Tipo => null,
            TipoDeEscopo.Instituicao => "O perfil só pode ser concedido no escopo da instituição.",
            _ => "O perfil só pode ser concedido no escopo de uma unidade.",
        };

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

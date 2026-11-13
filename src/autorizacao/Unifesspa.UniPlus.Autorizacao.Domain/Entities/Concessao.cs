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

    private readonly List<BaseDaPermissaoNaConcessao> _bases = [];

    /// <summary>As bases das permissões na concessão, quando o chamador as trouxe; só se acrescenta.</summary>
    public IReadOnlyList<BaseDaPermissaoNaConcessao> Bases => _bases;

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
    /// <param name="titular">A pessoa a quem a concessão se dá, com o e-mail que a revogação pede de volta.</param>
    /// <param name="perfil">O perfil concedido.</param>
    /// <param name="escopo">A instituição ou a unidade em que a concessão vale.</param>
    /// <param name="origem">O pedido ou a instalação que gerou a concessão.</param>
    /// <param name="ato">Quem concede e quando.</param>
    /// <param name="catalogo">O catálogo que dá o alcance das permissões do perfil.</param>
    /// <param name="validaAte">Fim da vigência, quando a concessão é por prazo.</param>
    public static Result<Concessao> Conceder(
        TitularDaConcessao titular,
        Perfil perfil,
        EscopoDaConcessao escopo,
        OrigemDaConcessao origem,
        AtoDoAgente ato,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset? validaAte = null)
    {
        ArgumentNullException.ThrowIfNull(titular);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(ato);
        ArgumentNullException.ThrowIfNull(catalogo);

        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo), .. ErrosDaValidade(perfil, validaAte, ato.Instante)];

        return erros.Count > 0
            ? Result<Concessao>.ValidationFailure(erros)
            : Result<Concessao>.Success(Nova(titular, perfil, escopo, origem).ConcedidaPor(ato, validaAte));
    }

    /// <summary>
    /// Passa a concessão para a versão que substituiu a que ela alcançava, sem acréscimo de permissão: troca só a versão e
    /// mantém pessoa, escopo, validade e origem. É o caminho único da troca de versão da concessão. Antes da troca, grava a
    /// base de cada código mantido que ainda não tem base (o retrato da versão que sai) e encerra a de cada código que sai,
    /// para que "sem base" signifique sempre que a versão atual é aquela em que o código chegou por último. Quem chama trava
    /// o perfil e as concessões da versão que sai e traz as bases da concessão, então a que está revogada, é de governança
    /// ou segue outra versão é defeito de quem chama.
    /// </summary>
    /// <param name="sai">A versão que a concessão deve estar alcançando agora.</param>
    /// <param name="entra">A versão em vigor que a concessão passa a alcançar.</param>
    public void MigrarParaVersao(VersaoDoPerfil sai, VersaoDoPerfil entra)
    {
        ArgumentNullException.ThrowIfNull(sai);
        ArgumentNullException.ThrowIfNull(entra);

        if (RevogadaEm is not null || VersaoId != sai.Id)
        {
            throw new InvalidOperationException(
                $"A concessão {Id} não pode migrar da versão {sai.Id}: está revogada, é de governança ou alcança a versão {VersaoId}.");
        }

        _bases.AddRange(BasesDaTroca(sai, entra));
        VersaoId = entra.Id;
    }

    private List<BaseDaPermissaoNaConcessao> BasesDaTroca(VersaoDoPerfil sai, VersaoDoPerfil entra)
    {
        HashSet<string> mantidos = [.. entra.Codigos()];
        List<BaseDaPermissaoNaConcessao> bases = [];

        foreach (PermissaoDaVersao permissao in sai.Permissoes.OrderBy(static p => p.Codigo, StringComparer.Ordinal))
        {
            if (BaseDaPermissaoNaTroca(permissao, mantidos, entra.Id, bases.Count) is { } nova)
            {
                bases.Add(nova);
            }
        }

        return bases;
    }

    private BaseDaPermissaoNaConcessao? BaseDaPermissaoNaTroca(
        PermissaoDaVersao permissao, HashSet<string> mantidos, Guid versaoAlvoId, int ordinal) =>
        mantidos.Contains(permissao.Codigo)
            ? BaseDoCodigoMantido(permissao, versaoAlvoId, ordinal)
            : BaseDaPermissaoNaConcessao.DaSaida(PerfilId, Id, permissao.Codigo, versaoAlvoId, ordinal);

    private BaseDaPermissaoNaConcessao? BaseDoCodigoMantido(PermissaoDaVersao permissao, Guid versaoAlvoId, int ordinal) =>
        TemBaseAtiva(permissao.Codigo)
            ? null
            : BaseDaPermissaoNaConcessao.Registrar(
                PerfilId, Id, permissao.Codigo, new BaseDaConcessao(permissao.Classificacao, permissao.DeUnidade),
                OrigemDaBase.Migracao, versaoAlvoId, ordinal);

    private bool TemBaseAtiva(string codigo) =>
        _bases.Where(b => b.Codigo == codigo).MaxBy(static b => b.OrdemNaTrilha) is { Origem: not OrigemDaBase.Saida };

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
    /// <param name="ato">Quem revoga e quando.</param>
    /// <param name="motivo">O motivo da revogação, que a trilha mostra.</param>
    public Result Revogar(AtoDoAgente ato, string? motivo)
    {
        ArgumentNullException.ThrowIfNull(ato);

        if (RevogadaEm is not null)
        {
            return Result.Failure(new DomainError(ConcessaoErrorCodes.JaRevogada, "A concessão já foi revogada."));
        }

        return NormalizarMotivo(motivo) is { } motivoNormalizado
            ? RegistrarRevogacao(ato, motivoNormalizado)
            : Result.ValidationFailure([MotivoDaRevogacaoInvalido()]);
    }

    private Result RegistrarRevogacao(AtoDoAgente ato, string motivo)
    {
        RevogadaEm = ato.Instante;
        RevogadaPorEmissor = ato.Agente.Emissor;
        RevogadaPorSub = ato.Agente.Sub;
        RevogadaPorNome = ato.Agente.Nome;
        MotivoRevogacao = motivo;

        return Result.Success();
    }

    private static FieldError MotivoDaRevogacaoInvalido() =>
        new("motivo", new DomainError(
            ConcessaoErrorCodes.MotivoInvalido,
            $"O motivo da revogação é obrigatório e deve ter até {MotivoMaxLength} caracteres."));

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

    private static Concessao Nova(TitularDaConcessao titular, Perfil perfil, EscopoDaConcessao escopo, OrigemDaConcessao origem) =>
        new()
        {
            BeneficiarioEmissor = titular.Pessoa.Emissor,
            BeneficiarioSub = titular.Pessoa.Sub,
            BeneficiarioNome = titular.Pessoa.Nome,
            BeneficiarioEmail = PedidoDeConcessao.EmailNormalizado(titular.Email),
            PerfilId = perfil.Id,
            VersaoId = perfil.VersaoVigente?.Id,
            EscopoTipo = escopo.Tipo,
            UnidadeId = escopo.UnidadeId,
            OrigemTipo = origem.Tipo,
            OrigemId = origem.Id,
        };

    private Concessao ConcedidaPor(AtoDoAgente ato, DateTimeOffset? validaAte)
    {
        ConcedidaEm = ato.Instante;
        ConcedidaPorEmissor = ato.Agente.Emissor;
        ConcedidaPorSub = ato.Agente.Sub;
        ConcedidaPorNome = ato.Agente.Nome;
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
            yield return new("validaAte", new DomainError(ConcessaoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo));
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

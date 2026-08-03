namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Um termo de consentimento/declaração do catálogo administrável
/// (UNI-REQ-0086/RN-COL-05). Identidade própria e estável — dois termos
/// catalogados podem ter o mesmo <see cref="Nome"/>, sem colidir.
/// </summary>
/// <remarks>
/// <para>Ciclo de vida do rascunho: <c>EM_ELABORACAO</c> (mutável) → <see cref="MarcarRevisado"/>
/// (<c>REVISADO</c>, grava o ator explícito) → <see cref="Promover"/> (gera uma
/// <see cref="TermoConsentimentoVersao"/> imutável). Editar texto ou base legal de
/// um rascunho já revisado (<see cref="EditarRascunho"/>) devolve automaticamente
/// o status a <c>EM_ELABORACAO</c> e limpa a marca — a revisão sempre se refere ao
/// conteúdo exato promovido depois dela, nunca a uma edição posterior às suas
/// costas.</para>
/// <para>Escopo desta issue é só o cadastro: exigir o termo numa fase do Processo
/// Seletivo, congelar no snapshot de publicação (segunda metade de UNI-REQ-0086) e
/// o fluxo de aceite do candidato em runtime (UNI-REQ-0091) ficam fora.</para>
/// </remarks>
public sealed class TermoConsentimento : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 1;
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;
    private const char DelimitadorHash = (char)0x1F;

    private readonly List<TermoConsentimentoVersao> _versoes = [];

    public string Nome { get; private set; } = string.Empty;
    public string? TextoRascunho { get; private set; }
    public string? BaseLegalRascunho { get; private set; }
    public FormaAceite FormaAceiteRascunho { get; private set; }
    public bool Revisado { get; private set; }
    public string? RevisadoPor { get; private set; }
    public DateTimeOffset? RevisadoEm { get; private set; }
    public IReadOnlyList<TermoConsentimentoVersao> Versoes => _versoes.AsReadOnly();

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TermoConsentimento()
    {
    }

    /// <summary>
    /// Cria um termo novo, com rascunho vazio ou com campos iniciais. Nasce sempre
    /// <c>EM_ELABORACAO</c> — marcar revisado e promover são operações explícitas
    /// subsequentes, nunca implícitas na criação.
    /// </summary>
    public static Result<TermoConsentimento> Criar(
        string nome, string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        ArgumentNullException.ThrowIfNull(nome);

        Result<FormaAceite> validacao = ValidarCampos(nome, textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        if (validacao.IsFailure)
        {
            return Result<TermoConsentimento>.Failure(validacao.Error!);
        }

        return Result<TermoConsentimento>.Success(new TermoConsentimento
        {
            Nome = nome.Trim(),
            TextoRascunho = NormalizarOpcional(textoRascunho),
            BaseLegalRascunho = NormalizarOpcional(baseLegalRascunho),
            FormaAceiteRascunho = validacao.Value!,
            Revisado = false,
        });
    }

    /// <summary>
    /// Edita o rascunho corrente. Se o rascunho já estava <see cref="Revisado"/>,
    /// a edição é aceita mas devolve o status a <c>EM_ELABORACAO</c> e limpa a
    /// marca de revisão — nunca falha por já estar revisado.
    /// </summary>
    public Result EditarRascunho(string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        Result<FormaAceite> validacao = ValidarCampos(Nome, textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        if (validacao.IsFailure)
        {
            return Result.Failure(validacao.Error!);
        }

        TextoRascunho = NormalizarOpcional(textoRascunho);
        BaseLegalRascunho = NormalizarOpcional(baseLegalRascunho);
        FormaAceiteRascunho = validacao.Value!;

        if (Revisado)
        {
            Revisado = false;
            RevisadoPor = null;
            RevisadoEm = null;
        }

        return Result.Success();
    }

    /// <summary>
    /// Marca o rascunho corrente como revisado — portão distinto da edição comum,
    /// grava o ator explícito da chamada. Recusa rascunho sem texto ou sem base
    /// legal (a forma de aceite pode continuar <c>A_DEFINIR</c>: é a promoção que
    /// bloqueia essa forma na publicação de um processo, não a revisão do cadastro).
    /// </summary>
    public Result MarcarRevisado(string revisadoPor, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revisadoPor);

        if (string.IsNullOrWhiteSpace(TextoRascunho))
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.RevisaoSemTexto,
                "Rascunho sem texto não pode ser marcado como revisado."));
        }

        if (string.IsNullOrWhiteSpace(BaseLegalRascunho))
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.RevisaoSemBaseLegal,
                "Rascunho sem base legal não pode ser marcado como revisado."));
        }

        Revisado = true;
        RevisadoPor = revisadoPor;
        RevisadoEm = agora;

        return Result.Success();
    }

    /// <summary>
    /// Promove o rascunho revisado a uma nova <see cref="TermoConsentimentoVersao"/>
    /// imutável. Recusa rascunho não revisado, sem texto ou sem base legal. O
    /// rascunho permanece intacto após a promoção — pode ser editado de novo,
    /// gerando a próxima versão no futuro.
    /// </summary>
    /// <remarks>
    /// Devolve a versão criada (em vez de só <see cref="Result"/>) para o handler
    /// adicioná-la explicitamente via <c>ITermoConsentimentoRepository.AdicionarVersaoAsync</c>
    /// — o EF Core não detecta como <c>Added</c> uma entidade só inserida na coleção
    /// em memória de um agregado JÁ rastreado (recarregado do banco): o Id gerado
    /// client-side (Guid v7) parece "já existente" para a heurística de
    /// <c>DetectChanges</c>, e a instrução vira um <c>UPDATE</c> que não afeta linha
    /// nenhuma. Mesmo padrão de <c>ProcessoSeletivo.Publicar</c> devolvendo
    /// <c>VersaoConfiguracao</c> para <c>AdicionarVersaoConfiguracaoAsync</c>.
    /// </remarks>
    public Result<TermoConsentimentoVersao> Promover(string promovidoPor, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promovidoPor);

        if (!Revisado)
        {
            return Result<TermoConsentimentoVersao>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.PromocaoSemRevisao,
                "Só um rascunho marcado como revisado pode ser promovido a versão."));
        }

        if (string.IsNullOrWhiteSpace(TextoRascunho))
        {
            return Result<TermoConsentimentoVersao>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.RevisaoSemTexto,
                "Rascunho sem texto não pode ser promovido."));
        }

        if (string.IsNullOrWhiteSpace(BaseLegalRascunho))
        {
            return Result<TermoConsentimentoVersao>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.RevisaoSemBaseLegal,
                "Rascunho sem base legal não pode ser promovido."));
        }

        string formaAceiteToken = FormasAceite.ParaTokenCanonico(FormaAceiteRascunho);
        string hash = CalcularHash(TextoRascunho, BaseLegalRascunho, formaAceiteToken);

        TermoConsentimentoVersao versao = TermoConsentimentoVersao.Promover(
            Id, TextoRascunho, BaseLegalRascunho, FormaAceiteRascunho, hash, agora, promovidoPor);
        _versoes.Add(versao);

        return Result<TermoConsentimentoVersao>.Success(versao);
    }

    /// <summary>
    /// SHA-256 hex do conteúdo semântico da versão (texto + base legal + forma de
    /// aceite), separado por um caractere de controle (Unit Separator, 0x1F) que
    /// não aparece em texto digitado — evita colisão de concatenação ambígua
    /// (ex.: "AB"+"C" vs "A"+"BC") mesmo que um campo contenha espaço ou vírgula.
    /// Determinístico: o mesmo conteúdo produz o mesmo hash em qualquer runtime.
    /// </summary>
    private static string CalcularHash(string texto, string baseLegal, string formaAceiteToken)
    {
        string payload = string.Join(DelimitadorHash, texto, baseLegal, formaAceiteToken);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static Result<FormaAceite> ValidarCampos(
        string nome, string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(TermoConsentimentoErrorCodes.NomeObrigatorio, "Nome do termo é obrigatório.");
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Falha(
                TermoConsentimentoErrorCodes.NomeTamanho,
                $"Nome do termo deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.");
        }

        if (textoRascunho is not null && textoRascunho.Trim().Length > TextoMaxLength)
        {
            return Falha(
                TermoConsentimentoErrorCodes.TextoTamanho,
                $"Texto do rascunho deve ter no máximo {TextoMaxLength} caracteres.");
        }

        if (baseLegalRascunho is not null && baseLegalRascunho.Trim().Length > BaseLegalMaxLength)
        {
            return Falha(
                TermoConsentimentoErrorCodes.BaseLegalTamanho,
                $"Base legal do rascunho deve ter no máximo {BaseLegalMaxLength} caracteres.");
        }

        FormaAceite formaAceite = FormaAceite.ADefinir;
        if (!string.IsNullOrWhiteSpace(formaAceiteRascunhoToken)
            && !FormasAceite.TryAnalisar(formaAceiteRascunhoToken, out formaAceite))
        {
            return Falha(
                TermoConsentimentoErrorCodes.FormaAceiteInvalida,
                "Forma de aceite deve ser uma de: " + string.Join(", ", FormasAceite.TokensCanonicos) + ".");
        }

        return Result<FormaAceite>.Success(formaAceite);
    }

    private static Result<FormaAceite> Falha(string codigo, string mensagem) =>
        Result<FormaAceite>.Failure(new DomainError(codigo, mensagem));

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

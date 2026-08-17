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
/// <see cref="TermoConsentimentoVersao"/> imutável e CONSOME a revisão, devolvendo
/// o status a <c>EM_ELABORACAO</c>). Editar texto ou base legal de um rascunho já
/// revisado (<see cref="EditarRascunho"/>) também devolve automaticamente o status
/// a <c>EM_ELABORACAO</c> — a revisão sempre se refere ao conteúdo exato promovido
/// depois dela, nunca a uma edição posterior às suas costas, e cada versão exige seu
/// próprio sinal de revisão, mesmo promovendo o mesmo conteúdo duas vezes.</para>
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
    /// Cria um termo novo, com rascunho vazio ou com campos iniciais, acumulando
    /// toda violação independente em vez de parar na primeira. Nasce sempre
    /// <c>EM_ELABORACAO</c> — marcar revisado e promover são operações explícitas
    /// subsequentes, nunca implícitas na criação.
    /// </summary>
    public static Result<TermoConsentimento> Criar(
        string? nome, string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        List<FieldError> erros = [];

        string? nomeNorm = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                TermoConsentimentoErrorCodes.NomeObrigatorio, "Nome do termo é obrigatório.")));
        }
        else
        {
            nomeNorm = nome.Trim();
            if (nomeNorm.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    TermoConsentimentoErrorCodes.NomeTamanho,
                    $"Nome do termo deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNorm = null;
            }
        }

        Result<FormaAceite> camposEditaveis = ValidarCamposEditaveis(textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        if (camposEditaveis.IsFailure)
        {
            erros.AddRange(camposEditaveis.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<TermoConsentimento>.ValidationFailure(erros);
        }

        return Result<TermoConsentimento>.Success(new TermoConsentimento
        {
            Nome = nomeNorm!,
            TextoRascunho = NormalizarOpcional(textoRascunho),
            BaseLegalRascunho = NormalizarOpcional(baseLegalRascunho),
            FormaAceiteRascunho = camposEditaveis.Value,
            Revisado = false,
        });
    }

    /// <summary>
    /// Edita o rascunho corrente, acumulando toda violação independente. Se o
    /// rascunho já estava <see cref="Revisado"/>, a edição é aceita mas devolve o
    /// status a <c>EM_ELABORACAO</c> e limpa a marca de revisão — nunca falha por
    /// já estar revisado. O <c>Nome</c> não é campo deste comando (imutável desde
    /// a criação, nunca fica inválido depois de já criado) — só os três campos
    /// editáveis do rascunho são revalidados aqui.
    /// </summary>
    public Result EditarRascunho(string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        Result<FormaAceite> validacao = ValidarCamposEditaveis(textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        TextoRascunho = NormalizarOpcional(textoRascunho);
        BaseLegalRascunho = NormalizarOpcional(baseLegalRascunho);
        FormaAceiteRascunho = validacao.Value;

        if (Revisado)
        {
            Revisado = false;
            RevisadoPor = null;
            RevisadoEm = null;
        }

        return Result.Success();
    }

    /// <summary>
    /// Valida os três campos editáveis do rascunho (texto, base legal, forma de
    /// aceite), sem I/O e sem mutar nada — para o handler de edição falhar rápido
    /// antes de buscar o termo por Id (validação sempre vence 404).
    /// </summary>
    public static Result ValidarCamposDoPayload(
        string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        Result<FormaAceite> resultado = ValidarCamposEditaveis(textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : Result.Success();
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
    /// texto e a base legal do rascunho permanecem intactos após a promoção — só a
    /// marca de revisão é consumida (status volta a <c>EM_ELABORACAO</c>); o
    /// rascunho pode ser editado de novo, gerando a próxima versão no futuro.
    /// </summary>
    /// <remarks>
    /// <para>Devolve a versão criada (em vez de só <see cref="Result"/>) para o handler
    /// adicioná-la explicitamente via <c>ITermoConsentimentoRepository.AdicionarVersaoAsync</c>
    /// — o EF Core não detecta como <c>Added</c> uma entidade só inserida na coleção
    /// em memória de um agregado JÁ rastreado (recarregado do banco): o Id gerado
    /// client-side (Guid v7) parece "já existente" para a heurística de
    /// <c>DetectChanges</c>, e a instrução vira um <c>UPDATE</c> que não afeta linha
    /// nenhuma. Mesmo padrão de <c>ProcessoSeletivo.Publicar</c> devolvendo
    /// <c>VersaoConfiguracao</c> para <c>AdicionarVersaoConfiguracaoAsync</c>.</para>
    /// <para>A revisão é consumida (não só o rascunho preservado) para que um
    /// retry acidental da promoção — outra <c>Idempotency-Key</c>, um segundo
    /// clique — não anexe mais uma versão idêntica ao histórico forense: a segunda
    /// chamada encontra <see cref="Revisado"/> já <see langword="false"/> e falha
    /// com <c>PromocaoSemRevisao</c> em vez de duplicar a versão.</para>
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

        TermoConsentimentoVersao versao = TermoConsentimentoVersao.Promover(
            Id, TextoRascunho, BaseLegalRascunho, FormaAceiteRascunho, agora, promovidoPor);
        _versoes.Add(versao);

        // A revisão é CONSUMIDA pela promoção — cada versão exige seu próprio sinal
        // de revisão, mesmo sem edição do rascunho entre uma promoção e outra. Sem
        // isso, uma segunda chamada de promoção (retry acidental com outra
        // Idempotency-Key, ou um segundo clique) anexaria mais uma versão idêntica
        // ao histórico forense a partir do MESMO conteúdo já revisado — a segunda
        // agora falha com PromocaoSemRevisao até o operador confirmar de novo.
        Revisado = false;
        RevisadoPor = null;
        RevisadoEm = null;

        return Result<TermoConsentimentoVersao>.Success(versao);
    }

    private static Result<FormaAceite> ValidarCamposEditaveis(
        string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        List<FieldError> erros = [];

        if (textoRascunho is not null && textoRascunho.Trim().Length > TextoMaxLength)
        {
            erros.Add(new("textoRascunho", new DomainError(
                TermoConsentimentoErrorCodes.TextoTamanho,
                $"Texto do rascunho deve ter no máximo {TextoMaxLength} caracteres.")));
        }

        if (baseLegalRascunho is not null && baseLegalRascunho.Trim().Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegalRascunho", new DomainError(
                TermoConsentimentoErrorCodes.BaseLegalTamanho,
                $"Base legal do rascunho deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        FormaAceite formaAceite = FormaAceite.ADefinir;
        if (!string.IsNullOrWhiteSpace(formaAceiteRascunhoToken)
            && !FormasAceite.TryAnalisar(formaAceiteRascunhoToken, out formaAceite))
        {
            erros.Add(new("formaAceiteRascunho", new DomainError(
                TermoConsentimentoErrorCodes.FormaAceiteInvalida,
                "Forma de aceite deve ser uma de: " + string.Join(", ", FormasAceite.TokensCanonicos) + ".")));
        }

        return erros.Count == 0
            ? Result<FormaAceite>.Success(formaAceite)
            : Result<FormaAceite>.ValidationFailure(erros);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Um termo de consentimento/declaração do catálogo administrável
/// (termos imunes após a publicação, UNI-REQ-0086). Identidade própria e estável (<c>Id</c>) e código
/// estável (<see cref="Codigo"/>), formado do nome na criação e único entre os termos vivos: nomes que só
/// diferem em acento, caixa ou pontuação geram o mesmo código e não convivem.
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
/// <para>O cadastro do termo é independente da exigência em uma fase do Processo
/// Seletivo, do registro da configuração publicada (UNI-REQ-0086) e do
/// aceite do candidato (UNI-REQ-0091).</para>
/// </remarks>
public sealed class TermoConsentimento : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMinLength = 1;
    private const int NomeMaxLength = 200;
    private const int TextoMaxLength = 20_000;
    private const int BaseLegalMaxLength = 500;

    private readonly List<TermoConsentimentoVersao> _versoes = [];

    public string Nome { get; private set; } = string.Empty;
    public CodigoTermoConsentimento Codigo { get; private set; } = null!;
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
        Result<IdentificacaoDoTermo> identificacao = Identificar(nome);
        Result<FormaAceite> campos = ValidarCamposEditaveis(textoRascunho, baseLegalRascunho, formaAceiteRascunhoToken);
        List<FieldError> erros = [.. ErrosDoNome(identificacao), .. campos.Errors];

        return erros.Count > 0
            ? Result<TermoConsentimento>.ValidationFailure(erros)
            : Result<TermoConsentimento>.Success(Novo(identificacao.Value!, textoRascunho, baseLegalRascunho, campos.Value));
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

        DescartarRevisao();

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
    /// Marca o rascunho corrente como revisado — etapa distinta da edição comum,
    /// grava o ator explícito da chamada. Recusa rascunho sem texto ou sem base
    /// legal (a forma de aceite pode continuar <c>A_DEFINIR</c>: é a promoção que
    /// bloqueia essa forma na publicação de um processo, não a revisão do cadastro).
    /// </summary>
    public Result MarcarRevisado(string revisadoPor, DateTimeOffset agora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revisadoPor);

        if (RecusaDoRascunho("marcado como revisado") is { } recusa)
        {
            return Result.Failure(recusa);
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
    /// <c>DetectChanges</c>, e a instrução se torna um <c>UPDATE</c> que não afeta linha
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

        DomainError? recusa = Revisado ? RecusaDoRascunho("promovido") : SemRevisao();
        if (recusa is not null)
        {
            return Result<TermoConsentimentoVersao>.Failure(recusa);
        }

        TermoConsentimentoVersao versao = TermoConsentimentoVersao.Promover(
            Id, ProximoNumeroDeVersao(), TextoRascunho!, BaseLegalRascunho!, FormaAceiteRascunho, agora, promovidoPor);
        _versoes.Add(versao);
        DescartarRevisao();

        return Result<TermoConsentimentoVersao>.Success(versao);
    }

    private static DomainError SemRevisao() => new(
        TermoConsentimentoErrorCodes.PromocaoSemRevisao,
        "Só um rascunho marcado como revisado pode ser promovido a versão.");

    private DomainError? RecusaDoRascunho(string acao)
    {
        if (string.IsNullOrWhiteSpace(TextoRascunho))
        {
            return new DomainError(TermoConsentimentoErrorCodes.RevisaoSemTexto, $"Rascunho sem texto não pode ser {acao}.");
        }

        return string.IsNullOrWhiteSpace(BaseLegalRascunho)
            ? new DomainError(TermoConsentimentoErrorCodes.RevisaoSemBaseLegal, $"Rascunho sem base legal não pode ser {acao}.")
            : null;
    }

    // A promoção consome a revisão: cada versão exige seu próprio sinal, e uma segunda promoção (retry, segundo clique) falha
    // com PromocaoSemRevisao em vez de anexar outra versão idêntica ao histórico forense.
    private void DescartarRevisao()
    {
        Revisado = false;
        RevisadoPor = null;
        RevisadoEm = null;
    }

    private static IEnumerable<FieldError> ErrosDoNome(Result<IdentificacaoDoTermo> identificacao) =>
        identificacao.IsFailure ? [new FieldError("nome", identificacao.Error!)] : [];

    private static TermoConsentimento Novo(
        IdentificacaoDoTermo identificacao, string? textoRascunho, string? baseLegalRascunho, FormaAceite formaAceite) =>
        new()
        {
            Nome = identificacao.Nome,
            Codigo = identificacao.Codigo,
            TextoRascunho = NormalizarOpcional(textoRascunho),
            BaseLegalRascunho = NormalizarOpcional(baseLegalRascunho),
            FormaAceiteRascunho = formaAceite,
            Revisado = false,
        };

    private static Result<FormaAceite> ValidarCamposEditaveis(
        string? textoRascunho, string? baseLegalRascunho, string? formaAceiteRascunhoToken)
    {
        List<FieldError> erros = [];
        ConferirLimite(erros, "textoRascunho", textoRascunho, TextoMaxLength, TermoConsentimentoErrorCodes.TextoTamanho, "Texto do rascunho");
        ConferirLimite(erros, "baseLegalRascunho", baseLegalRascunho, BaseLegalMaxLength, TermoConsentimentoErrorCodes.BaseLegalTamanho, "Base legal do rascunho");
        FormaAceite formaAceite = ResolverFormaDeAceite(formaAceiteRascunhoToken, erros);

        return erros.Count == 0
            ? Result<FormaAceite>.Success(formaAceite)
            : Result<FormaAceite>.ValidationFailure(erros);
    }

    private static void ConferirLimite(
        List<FieldError> erros, string campo, string? valor, int maximo, string codigoDoErro, string rotulo)
    {
        if (valor is not null && valor.Trim().Length > maximo)
        {
            erros.Add(new(campo, new DomainError(codigoDoErro, $"{rotulo} deve ter no máximo {maximo} caracteres.")));
        }
    }

    private static FormaAceite ResolverFormaDeAceite(string? token, List<FieldError> erros)
    {
        if (token is null)
        {
            return FormaAceite.ADefinir;
        }

        if (!CatalogoDe<FormaAceite>.TentarObter(token, out FormaAceite formaAceite))
        {
            string aceitos = string.Join(", ", CatalogoDe<FormaAceite>.Todos().Select(static declarado => declarado.Codigo));
            erros.Add(new("formaAceiteRascunho", new DomainError(
                TermoConsentimentoErrorCodes.FormaAceiteInvalida, $"Forma de aceite deve ser uma de: {aceitos}.")));
        }

        return formaAceite;
    }

    // As versões do termo são carregadas com ele; duas promoções concorrentes leem o mesmo número e a segunda falha no
    // índice único (termo, número) ou no token de concorrência do termo.
    private int ProximoNumeroDeVersao() => _versoes.Count == 0 ? 1 : _versoes.Max(static v => v.Numero) + 1;

    private static Result<IdentificacaoDoTermo> Identificar(string? nomeInformado)
    {
        Result<string> nome = ValidarNome(nomeInformado);
        if (nome.IsFailure)
        {
            return Result<IdentificacaoDoTermo>.Failure(nome.Error!);
        }

        Result<CodigoTermoConsentimento> codigo = CodigoTermoConsentimento.Gerar(nome.Value!);
        return codigo.IsFailure
            ? Result<IdentificacaoDoTermo>.Failure(codigo.Error!)
            : Result<IdentificacaoDoTermo>.Success(new IdentificacaoDoTermo(nome.Value!, codigo.Value!));
    }

    private static Result<string> ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<string>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.NomeObrigatorio, "Nome do termo é obrigatório."));
        }

        string nomeNorm = nome.Trim();
        if (nomeNorm.Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result<string>.Failure(new DomainError(
                TermoConsentimentoErrorCodes.NomeTamanho, $"Nome do termo deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        return Result<string>.Success(nomeNorm);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed record IdentificacaoDoTermo(string Nome, CodigoTermoConsentimento Codigo);
}

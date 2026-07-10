namespace Unifesspa.UniPlus.Publicacoes.Domain.Entities;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Tipo de ato publicado — cadastro do módulo Publicações (ADR-0105). Diz
/// <i>o que um ato é</i> (edital de abertura, aviso, convocação, resultado
/// final) e que consequências ele carrega, sem que nenhum desses tipos apareça
/// como literal em código: acrescentar um tipo é linha de cadastro (ADR-0103).
/// </summary>
/// <remarks>
/// <para>Os três atributos de consequência são <b>dados lidos</b>, jamais ramos
/// de comportamento. <c>CongelaConfiguracao</c> diz se o ato produz nova versão
/// congelada da configuração (RN08); <c>UnicoPorObjeto</c>, se o objeto do ato
/// admite um único ato vivo daquele tipo; <c>EfeitoIrreversivel</c>, se a
/// publicação não pode ser desfeita. Um <c>if (tipo.Codigo == "CONVOCACAO")</c>
/// seria a violação que a ADR-0103 proíbe.</para>
/// <para>O cadastro é <b>editável</b> — append-only vale para o ato publicado, não
/// para o catálogo. Editar o catálogo não reescreve o passado de ato nenhum: no
/// instante da publicação o ato <b>copia por valor</b> os atributos do tipo. Dois
/// mecanismos distintos, dois alvos distintos — a cópia por valor protege a
/// leitura do ato; a vigência protege a leitura do catálogo.</para>
/// <para>A janela de vigência é <b>semiaberta</b>: <c>[VigenciaInicio, VigenciaFim)</c>,
/// com fim exclusivo, como em <c>ObrigatoriedadeLegal</c>. Uma versão que encerra
/// em 01/06 e a sucessora que começa em 01/06 não se sobrepõem, e no dia da
/// fronteira vigora exatamente uma — a sucessora. A não-sobreposição entre versões
/// vivas do mesmo código é garantida pelo banco (exclusion constraint GIST), não
/// por guarda em memória: duas transações concorrentes furam qualquer guarda.</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete, e libera a janela para uma nova versão do mesmo código.</para>
/// </remarks>
public sealed partial class TipoAtoPublicado : SoftDeletableEntity, IAuditableEntity
{
    private const int CodigoMaxLength = 60;
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int BaseLegalMaxLength = 500;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Se o ato deste tipo produz uma nova versão congelada da configuração (RN08).</summary>
    public bool CongelaConfiguracao { get; private set; }

    /// <summary>Se o objeto do ato admite um único ato vivo deste tipo.</summary>
    public bool UnicoPorObjeto { get; private set; }

    /// <summary>Se a publicação de um ato deste tipo não pode ser desfeita.</summary>
    public bool EfeitoIrreversivel { get; private set; }

    /// <summary>Primeiro dia em que esta versão do tipo vale. Inclusivo.</summary>
    public DateOnly VigenciaInicio { get; private set; }

    /// <summary>Primeiro dia em que esta versão do tipo <b>já não</b> vale. Exclusivo; nulo enquanto a vigência é aberta.</summary>
    public DateOnly? VigenciaFim { get; private set; }

    /// <summary>Lei, artigo ou resolução que fundamenta o tipo de ato, quando houver.</summary>
    public string? BaseLegal { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoAtoPublicado()
    {
    }

    /// <summary>
    /// Cria uma versão do tipo de ato. Valida formato e coerência da janela de
    /// vigência. A não-sobreposição com outras versões vivas do mesmo código é
    /// verificada pelo banco, na gravação.
    /// </summary>
    public static Result<TipoAtoPublicado> Criar(
        string codigo,
        string nome,
        bool congelaConfiguracao,
        bool unicoPorObjeto,
        bool efeitoIrreversivel,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? baseLegal)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);

        Result validacao = ValidarCampos(codigo, nome, vigenciaInicio, vigenciaFim, baseLegal);
        if (validacao.IsFailure)
        {
            return Result<TipoAtoPublicado>.Failure(validacao.Error!);
        }

        var tipo = new TipoAtoPublicado();
        tipo.AplicarCampos(
            codigo, nome, congelaConfiguracao, unicoPorObjeto, efeitoIrreversivel,
            vigenciaInicio, vigenciaFim, baseLegal);

        return Result<TipoAtoPublicado>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos da versão. O <c>Codigo</c> é editável, como no
    /// <c>TipoDocumento</c>: o consumo é por cópia de valor no ato publicado, então
    /// renomear o código vivo não altera nenhum ato já publicado.
    /// </summary>
    public Result Atualizar(
        string codigo,
        string nome,
        bool congelaConfiguracao,
        bool unicoPorObjeto,
        bool efeitoIrreversivel,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? baseLegal)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);

        Result validacao = ValidarCampos(codigo, nome, vigenciaInicio, vigenciaFim, baseLegal);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        AplicarCampos(
            codigo, nome, congelaConfiguracao, unicoPorObjeto, efeitoIrreversivel,
            vigenciaInicio, vigenciaFim, baseLegal);

        return Result.Success();
    }

    /// <summary>
    /// Verdadeiro quando <paramref name="data"/> cai na janela semiaberta
    /// <c>[VigenciaInicio, VigenciaFim)</c>. Espelha o predicado que o repositório
    /// traduz para SQL — existe para os testes de domínio e para leitura em memória.
    /// </summary>
    public bool EstaVigenteEm(DateOnly data) =>
        VigenciaInicio <= data && (VigenciaFim is null || VigenciaFim > data);

    private void AplicarCampos(
        string codigo,
        string nome,
        bool congelaConfiguracao,
        bool unicoPorObjeto,
        bool efeitoIrreversivel,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? baseLegal)
    {
        Codigo = codigo.Trim();
        Nome = nome.Trim();
        CongelaConfiguracao = congelaConfiguracao;
        UnicoPorObjeto = unicoPorObjeto;
        EfeitoIrreversivel = efeitoIrreversivel;
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        BaseLegal = string.IsNullOrWhiteSpace(baseLegal) ? null : baseLegal.Trim();
    }

    private static Result ValidarCampos(
        string codigo,
        string nome,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? baseLegal)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.CodigoObrigatorio,
                "Código do tipo de ato é obrigatório."));
        }

        string codigoNorm = codigo.Trim();

        if (codigoNorm.Length > CodigoMaxLength)
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.CodigoTamanho,
                $"Código do tipo de ato deve ter no máximo {CodigoMaxLength} caracteres."));
        }

        // Caixa alta é exigida, não imposta: `convocacao` é recusado em vez de
        // convertido em silêncio. A coluna é `text` e o Postgres compara
        // case-sensitive — normalizar a caixa esconderia do usuário que dois
        // códigos que ele julgava distintos são o mesmo.
        if (!FormatoCodigo().IsMatch(codigoNorm))
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.CodigoFormato,
                "Código do tipo de ato deve usar apenas letras maiúsculas sem acento, separadas por underscore (ex.: EDITAL_ABERTURA)."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.NomeObrigatorio,
                "Nome do tipo de ato é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.NomeTamanho,
                $"Nome do tipo de ato deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (baseLegal is not null && baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres."));
        }

        // Fim exclusivo: uma janela cujo fim iguala o início não contém dia algum.
        if (vigenciaFim is { } fim && fim <= vigenciaInicio)
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio,
                "Fim da vigência é exclusivo e deve ser posterior ao início."));
        }

        return Result.Success();
    }

    [GeneratedRegex(@"^[A-Z]+(_[A-Z]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoCodigo();
}

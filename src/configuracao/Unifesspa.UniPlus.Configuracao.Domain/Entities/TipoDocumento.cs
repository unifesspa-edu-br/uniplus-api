namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de documento — cadastro institucional <b>classificatório puro</b>
/// (UNI-REQ-0013, módulo Configuração): diz <i>o que um documento é</i> (RG,
/// laudo médico, autodeclaração PPI…), nunca uma regra material sobre ele
/// (validade, assinatura, idade de emissão). Essas regras vivem na exigência
/// documental do edital (banco de Seleção) ou na homologação (ADR-0072).
/// </summary>
/// <remarks>
/// <para>O <c>Codigo</c> é a chave natural, único entre tipos vivos (índice único
/// parcial <c>WHERE is_deleted = false</c>) — e <b>editável</b> (diferente da
/// Modalidade), pois o consumo cross-módulo é por snapshot-copy desacoplado
/// (ADR-0061): editar o código vivo não altera o rótulo já congelado numa
/// exigência de Seleção. A unicidade é checada pelo handler (com proteção de
/// corrida via índice).</para>
/// <para>O <c>TipoEquivalente</c> é <b>rótulo classificatório</b> (RG ≡ CIN), não
/// relacionamento material: guarda o <c>Codigo</c> de outro tipo (sem FK), e o
/// único guarda é não ser equivalente a si mesmo. Por ser rótulo e não FK,
/// remover um tipo apontado como equivalente por outro <b>não</b> é bloqueado
/// (CA-04) — o rótulo do outro fica apontando para um código sem alvo vivo.</para>
/// <para>Dado institucional sem PII (LGPD inaplicável). A remoção é sempre
/// soft-delete e nunca bloqueada por referência.</para>
/// </remarks>
public sealed class TipoDocumento : SoftDeletableEntity, IAuditableEntity
{
    private const int CodigoMinLength = 1;
    private const int CodigoMaxLength = 60;
    private const int NomeMinLength = 2;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;
    private const int FormatosAceitosMaxLength = 200;
    private const int TipoEquivalenteMaxLength = 60;

    public string Codigo { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public CategoriaDocumento Categoria { get; private set; }
    public string? FormatosAceitos { get; private set; }
    public int? TamanhoMaximoMb { get; private set; }
    public string? TipoEquivalente { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private TipoDocumento()
    {
    }

    /// <summary>
    /// Cria um novo TipoDocumento. Valida formato/domínio local (incluindo a
    /// categoria contra o domínio fechado e o guard de auto-equivalência). A
    /// unicidade de <paramref name="codigo"/> entre tipos vivos é
    /// responsabilidade do handler. A <paramref name="categoria"/> chega como
    /// token textual (UPPER_SNAKE) e é validada pela allowlist.
    /// </summary>
    public static Result<TipoDocumento> Criar(
        string codigo,
        string nome,
        string? descricao,
        string categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(categoria);

        Result<CategoriaDocumento> validacao = ValidarCampos(
            codigo, nome, descricao, categoria, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);
        if (validacao.IsFailure)
        {
            return Result<TipoDocumento>.Failure(validacao.Error!);
        }

        var tipo = new TipoDocumento();
        tipo.AplicarCampos(
            codigo, nome, descricao, validacao.Value, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);

        return Result<TipoDocumento>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos do TipoDocumento. O <c>Codigo</c> é editável; sua
    /// unicidade (quando alterado) é responsabilidade do handler. Revalida
    /// formato/domínio e o guard de auto-equivalência.
    /// </summary>
    public Result Atualizar(
        string codigo,
        string nome,
        string? descricao,
        string categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        ArgumentNullException.ThrowIfNull(codigo);
        ArgumentNullException.ThrowIfNull(nome);
        ArgumentNullException.ThrowIfNull(categoria);

        Result<CategoriaDocumento> validacao = ValidarCampos(
            codigo, nome, descricao, categoria, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);
        if (validacao.IsFailure)
        {
            return Result.Failure(validacao.Error!);
        }

        AplicarCampos(
            codigo, nome, descricao, validacao.Value, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);

        return Result.Success();
    }

    private void AplicarCampos(
        string codigo,
        string nome,
        string? descricao,
        CategoriaDocumento categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        Codigo = codigo.Trim();
        Nome = nome.Trim();
        Descricao = NormalizarOpcional(descricao);
        Categoria = categoria;
        FormatosAceitos = NormalizarOpcional(formatosAceitos);
        TamanhoMaximoMb = tamanhoMaximoMb;
        TipoEquivalente = NormalizarOpcional(tipoEquivalente);
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CategoriaDocumento> ValidarCampos(
        string codigo,
        string nome,
        string? descricao,
        string categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.CodigoObrigatorio,
                "Código do tipo de documento é obrigatório."));
        }

        if (codigo.Trim().Length is < CodigoMinLength or > CodigoMaxLength)
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.CodigoTamanho,
                $"Código do tipo de documento deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres."));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.NomeObrigatorio,
                "Nome do tipo de documento é obrigatório."));
        }

        if (nome.Trim().Length is < NomeMinLength or > NomeMaxLength)
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.NomeTamanho,
                $"Nome do tipo de documento deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres."));
        }

        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de documento deve ter no máximo {DescricaoMaxLength} caracteres."));
        }

        if (!CategoriaDocumentos.TryAnalisar(categoria, out CategoriaDocumento categoriaResolvida))
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.CategoriaInvalida,
                $"Categoria do tipo de documento deve ser uma de: {string.Join(", ", CategoriaDocumentos.TokensCanonicos)}."));
        }

        if (formatosAceitos is not null && formatosAceitos.Trim().Length > FormatosAceitosMaxLength)
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.FormatosAceitosTamanho,
                $"Formatos aceitos devem ter no máximo {FormatosAceitosMaxLength} caracteres."));
        }

        if (tamanhoMaximoMb is <= 0)
        {
            return Result<CategoriaDocumento>.Failure(new DomainError(
                TipoDocumentoErrorCodes.TamanhoMaximoInvalido,
                "Tamanho máximo em MB, quando informado, deve ser positivo."));
        }

        if (tipoEquivalente is not null && !string.IsNullOrWhiteSpace(tipoEquivalente))
        {
            string equivalenteNorm = tipoEquivalente.Trim();
            if (equivalenteNorm.Length > TipoEquivalenteMaxLength)
            {
                return Result<CategoriaDocumento>.Failure(new DomainError(
                    TipoDocumentoErrorCodes.TipoEquivalenteTamanho,
                    $"Tipo equivalente deve ter no máximo {TipoEquivalenteMaxLength} caracteres."));
            }

            // Guard de auto-equivalência case-sensitive (Ordinal), alinhado ao CHECK
            // `tipo_equivalente <> codigo` do banco (também case-sensitive).
            if (string.Equals(equivalenteNorm, codigo.Trim(), StringComparison.Ordinal))
            {
                return Result<CategoriaDocumento>.Failure(new DomainError(
                    TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo,
                    "Um tipo de documento não pode declarar-se equivalente a si mesmo."));
            }
        }

        return Result<CategoriaDocumento>.Success(categoriaResolvida);
    }
}

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
    public Enums.CategoriaDocumento Categoria { get; private set; }
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
    /// Valida todos os campos, acumulando cada violação em vez de retornar na
    /// primeira (ADR-0125), sem mutar nada — existe para o handler de atualização
    /// decidir se vale a pena buscar a entidade e consultar unicidade antes mesmo
    /// de chamar <see cref="Atualizar"/>/<see cref="Criar"/>, que revalidam por
    /// conta própria e nunca confiam num resultado calculado por fora. O guard de
    /// auto-equivalência (<paramref name="tipoEquivalente"/> igual a
    /// <paramref name="codigo"/>) só roda depois que os dois já passaram nas
    /// próprias checagens individuais, para não mascarar a causa raiz de um dos
    /// dois atrás de uma relação inválida.
    /// </summary>
    public static Result<(
        string Codigo,
        string Nome,
        string? Descricao,
        Enums.CategoriaDocumento Categoria,
        string? FormatosAceitos,
        int? TamanhoMaximoMb,
        string? TipoEquivalente)> ValidarCampos(
        string? codigo,
        string? nome,
        string? descricao,
        string? categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        List<FieldError> erros = [];

        string? codigoNorm = null;
        if (string.IsNullOrWhiteSpace(codigo))
        {
            erros.Add(new("codigo", new DomainError(
                TipoDocumentoErrorCodes.CodigoObrigatorio, "Código do tipo de documento é obrigatório.")));
        }
        else
        {
            codigoNorm = codigo.Trim();
            if (codigoNorm.Length is < CodigoMinLength or > CodigoMaxLength)
            {
                erros.Add(new("codigo", new DomainError(
                    TipoDocumentoErrorCodes.CodigoTamanho,
                    $"Código do tipo de documento deve ter entre {CodigoMinLength} e {CodigoMaxLength} caracteres.")));
                codigoNorm = null;
            }
        }

        string? nomeNorm = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                TipoDocumentoErrorCodes.NomeObrigatorio, "Nome do tipo de documento é obrigatório.")));
        }
        else
        {
            nomeNorm = nome.Trim();
            if (nomeNorm.Length is < NomeMinLength or > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    TipoDocumentoErrorCodes.NomeTamanho,
                    $"Nome do tipo de documento deve ter entre {NomeMinLength} e {NomeMaxLength} caracteres.")));
                nomeNorm = null;
            }
        }

        string? descricaoNorm = NormalizarOpcional(descricao);
        if (descricaoNorm is not null && descricaoNorm.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                TipoDocumentoErrorCodes.DescricaoTamanho,
                $"Descrição do tipo de documento deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        bool categoriaValida = CategoriaDocumentos.TryAnalisar(categoria, out Enums.CategoriaDocumento categoriaResolvida);
        if (!categoriaValida)
        {
            erros.Add(new("categoria", new DomainError(
                TipoDocumentoErrorCodes.CategoriaInvalida,
                $"Categoria do tipo de documento deve ser uma de: {string.Join(", ", CategoriaDocumentos.TokensCanonicos)}.")));
        }

        string? formatosAceitosNorm = NormalizarOpcional(formatosAceitos);
        if (formatosAceitosNorm is not null && formatosAceitosNorm.Length > FormatosAceitosMaxLength)
        {
            erros.Add(new("formatosAceitos", new DomainError(
                TipoDocumentoErrorCodes.FormatosAceitosTamanho,
                $"Formatos aceitos devem ter no máximo {FormatosAceitosMaxLength} caracteres.")));
        }

        if (tamanhoMaximoMb is <= 0)
        {
            erros.Add(new("tamanhoMaximoMb", new DomainError(
                TipoDocumentoErrorCodes.TamanhoMaximoInvalido,
                "Tamanho máximo em MB, quando informado, deve ser positivo.")));
        }

        string? tipoEquivalenteNorm = NormalizarOpcional(tipoEquivalente);
        if (tipoEquivalenteNorm is not null && tipoEquivalenteNorm.Length > TipoEquivalenteMaxLength)
        {
            erros.Add(new("tipoEquivalente", new DomainError(
                TipoDocumentoErrorCodes.TipoEquivalenteTamanho,
                $"Tipo equivalente deve ter no máximo {TipoEquivalenteMaxLength} caracteres.")));
            tipoEquivalenteNorm = null;
        }

        if (codigoNorm is not null && tipoEquivalenteNorm is not null
            && string.Equals(tipoEquivalenteNorm, codigoNorm, StringComparison.Ordinal))
        {
            // Guard de auto-equivalência case-sensitive (Ordinal), alinhado ao CHECK
            // `tipo_equivalente <> codigo` do banco (também case-sensitive).
            erros.Add(new("tipoEquivalente", new DomainError(
                TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo,
                "Um tipo de documento não pode declarar-se equivalente a si mesmo.")));
        }

        if (erros.Count > 0)
        {
            return Result<(string, string, string?, Enums.CategoriaDocumento, string?, int?, string?)>.ValidationFailure(erros);
        }

        return Result<(string, string, string?, Enums.CategoriaDocumento, string?, int?, string?)>.Success((
            codigoNorm!, nomeNorm!, descricaoNorm, categoriaResolvida, formatosAceitosNorm, tamanhoMaximoMb, tipoEquivalenteNorm));
    }

    /// <summary>
    /// Cria um novo TipoDocumento. Revalida todos os campos via
    /// <see cref="ValidarCampos"/>, acumulando toda violação no mesmo lote. A
    /// unicidade de <c>Codigo</c> entre tipos vivos é responsabilidade do handler.
    /// </summary>
    public static Result<TipoDocumento> Criar(
        string? codigo,
        string? nome,
        string? descricao,
        string? categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        Result<(string Codigo, string Nome, string? Descricao, Enums.CategoriaDocumento Categoria, string? FormatosAceitos, int? TamanhoMaximoMb, string? TipoEquivalente)> validacao =
            ValidarCampos(codigo, nome, descricao, categoria, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);
        if (validacao.IsFailure)
        {
            return Result<TipoDocumento>.ValidationFailure(validacao.Errors);
        }

        var tipo = new TipoDocumento();
        tipo.AplicarCampos(validacao.Value);

        return Result<TipoDocumento>.Success(tipo);
    }

    /// <summary>
    /// Atualiza os atributos do TipoDocumento. O <c>Codigo</c> é editável; sua
    /// unicidade (quando alterado) é responsabilidade do handler. Revalida todos
    /// os campos via <see cref="ValidarCampos"/>, incluindo o guard de
    /// auto-equivalência.
    /// </summary>
    public Result Atualizar(
        string? codigo,
        string? nome,
        string? descricao,
        string? categoria,
        string? formatosAceitos,
        int? tamanhoMaximoMb,
        string? tipoEquivalente)
    {
        Result<(string Codigo, string Nome, string? Descricao, Enums.CategoriaDocumento Categoria, string? FormatosAceitos, int? TamanhoMaximoMb, string? TipoEquivalente)> validacao =
            ValidarCampos(codigo, nome, descricao, categoria, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        AplicarCampos(validacao.Value);

        return Result.Success();
    }

    private void AplicarCampos(
        (string Codigo, string Nome, string? Descricao, Enums.CategoriaDocumento Categoria, string? FormatosAceitos, int? TamanhoMaximoMb, string? TipoEquivalente) campos)
    {
        Codigo = campos.Codigo;
        Nome = campos.Nome;
        Descricao = campos.Descricao;
        Categoria = campos.Categoria;
        FormatosAceitos = campos.FormatosAceitos;
        TamanhoMaximoMb = campos.TamanhoMaximoMb;
        TipoEquivalente = campos.TipoEquivalente;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

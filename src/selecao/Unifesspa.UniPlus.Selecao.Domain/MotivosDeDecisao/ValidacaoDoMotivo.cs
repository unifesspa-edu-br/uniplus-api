namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As regras de campo do cadastro de motivos, num lugar só (ADR-0125): a criação valida o lote
/// inteiro e a edição reaproveita a regra da descrição, para que as duas recusem exatamente o
/// mesmo conjunto de textos.
/// </summary>
public static class ValidacaoDoMotivo
{
    public const int DescricaoMinLength = 3;
    public const int DescricaoMaxLength = 500;

    /// <summary>
    /// Valida todos os campos e acumula as violações independentes: código malformado, descrição
    /// fora do tamanho, tipo ausente ou inválido e sentido ausente coexistem no mesmo <c>errors[]</c>. A
    /// subclassificação e o sentido admitido dependem do tipo e só são conferidos quando ele é válido.
    /// </summary>
    public static Result<CamposValidados> Validar(CamposDoMotivo campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        List<FieldError> erros = [];
        CodigoMotivoDecisao? codigo = Coletar(erros, "codigo", CodigoMotivoDecisao.Criar(campos.Codigo));
        string? descricao = Coletar(erros, "descricao", ValidarDescricao(campos.Descricao));
        TipoMotivoDeDecisao? tipo = ColetarTipo(erros, campos.TipoDeDecisaoCodigo);
        IPoliticaDoTipoDeMotivo? politica = tipo is { } valido ? PoliticasDosTiposDeMotivo.Do(valido) : null;
        string? subclassificacao = ConferirSubclassificacao(erros, politica, campos.Subclassificacao);
        ConferirSentido(erros, politica, campos.Sentido);

        return erros.Count > 0
            ? Result<CamposValidados>.ValidationFailure(erros)
            : Result<CamposValidados>.Success(new CamposValidados(tipo!.Value, codigo!, descricao!, subclassificacao, campos.Sentido!.Value));
    }

    /// <summary>Valida e normaliza a descrição, único campo que a edição alcança.</summary>
    public static Result<string> ValidarDescricao(string? descricao)
    {
        string normalizada = descricao?.Trim() ?? string.Empty;
        DomainError? recusa = RecusaDaDescricao(normalizada);

        return recusa is null
            ? Result<string>.Success(normalizada)
            : Result<string>.ValidationFailure([new FieldError("descricao", recusa)]);
    }

    /// <summary>
    /// Se o texto livre traz o caractere nulo, que a coluna textual do banco não armazena: sem a recusa,
    /// o valor só falharia ao gravar, com 500 em vez de 422. Vale para todo texto livre do cadastro e da
    /// escolha dos motivos.
    /// </summary>
    public static bool TemCaractereNulo(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return texto.Contains('\0', StringComparison.Ordinal);
    }

    private static DomainError? RecusaDaDescricao(string normalizada)
    {
        if (normalizada.Length == 0)
        {
            return new DomainError(MotivoDeDecisaoErrorCodes.DescricaoObrigatoria, "Descrição do motivo de decisão é obrigatória.");
        }

        if (TemCaractereNulo(normalizada))
        {
            return new DomainError(MotivoDeDecisaoErrorCodes.DescricaoCaractereInvalido, "Descrição do motivo não pode conter o caractere nulo.");
        }

        return normalizada.Length is < DescricaoMinLength or > DescricaoMaxLength
            ? new DomainError(MotivoDeDecisaoErrorCodes.DescricaoTamanho, $"Descrição do motivo deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")
            : null;
    }

    private static string? ConferirSubclassificacao(List<FieldError> erros, IPoliticaDoTipoDeMotivo? politica, string? subclassificacao) =>
        politica is null ? null : Coletar(erros, "subclassificacao", politica.ConferirSubclassificacao(subclassificacao));

    private static void ConferirSentido(List<FieldError> erros, IPoliticaDoTipoDeMotivo? politica, SentidoDoMotivo? sentido)
    {
        if (sentido is null)
        {
            erros.Add(new FieldError("sentido", new DomainError(
                MotivoDeDecisaoErrorCodes.SentidoObrigatorio,
                $"Motivo de decisão deve declarar exatamente um sentido: {string.Join(" ou ", CatalogoDe<SentidoDoMotivo>.Todos().Select(static item => item.Codigo))}.")));
        }
        else if (politica is not null && !politica.AdmiteSentido(sentido.Value))
        {
            erros.Add(new FieldError("sentido", new DomainError(
                MotivoDeDecisaoErrorCodes.SentidoNaoAdmitido,
                "O sentido informado não é admitido para este tipo de motivo.")));
        }
    }

    private static TipoMotivoDeDecisao? ColetarTipo(List<FieldError> erros, string? tipoDeDecisaoCodigo)
    {
        Result<TipoMotivoDeDecisao> resultado = TipoDoMotivoInformado.Converter(tipoDeDecisaoCodigo);
        if (resultado.IsSuccess)
        {
            return resultado.Value;
        }

        erros.AddRange(resultado.Errors);
        return null;
    }

    private static T? Coletar<T>(List<FieldError> erros, string campo, Result<T> resultado)
    {
        if (resultado.IsSuccess)
        {
            return resultado.Value;
        }

        erros.AddRange(resultado.Errors.Select(e => e with { Field = e.Field ?? campo }));
        return default;
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
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
    /// fora do tamanho, tipo ausente e sentido ausente coexistem no mesmo <c>errors[]</c>. A
    /// subclassificação e o sentido admitido dependem do tipo e só são conferidos quando ele é válido.
    /// </summary>
    public static Result<CamposValidados> Validar(CamposDoMotivo campos)
    {
        ArgumentNullException.ThrowIfNull(campos);

        List<FieldError> erros = [];
        CodigoMotivoDecisao? codigo = Coletar(erros, "codigo", CodigoMotivoDecisao.Criar(campos.Codigo));
        string? descricao = Coletar(erros, "descricao", ValidarDescricao(campos.Descricao));
        IPoliticaDoTipoDeMotivo? politica = ConferirTipo(erros, campos.Tipo);
        string? subclassificacao = politica is null
            ? null
            : Coletar(erros, "subclassificacao", politica.ConferirSubclassificacao(campos.Subclassificacao));
        ConferirSentido(erros, politica, campos.Sentido);

        return erros.Count > 0
            ? Result<CamposValidados>.ValidationFailure(erros)
            : Result<CamposValidados>.Success(new CamposValidados(campos.Tipo, codigo!, descricao!, subclassificacao, campos.Sentido));
    }

    /// <summary>Valida e normaliza a descrição, único campo que a edição alcança.</summary>
    public static Result<string> ValidarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return FalhaDaDescricao(MotivoDeDecisaoErrorCodes.DescricaoObrigatoria, "Descrição do motivo de decisão é obrigatória.");
        }

        string normalizada = descricao.Trim();

        // O caractere nulo não é espaço em branco e cabe no tamanho, mas a coluna textual do banco
        // não o armazena: sem esta recusa o valor só falharia ao gravar, com 500 em vez de 422.
        if (normalizada.Contains('\0', StringComparison.Ordinal))
        {
            return FalhaDaDescricao(MotivoDeDecisaoErrorCodes.DescricaoCaractereInvalido, "Descrição do motivo não pode conter o caractere nulo.");
        }

        return normalizada.Length is < DescricaoMinLength or > DescricaoMaxLength
            ? FalhaDaDescricao(MotivoDeDecisaoErrorCodes.DescricaoTamanho, $"Descrição do motivo deve ter entre {DescricaoMinLength} e {DescricaoMaxLength} caracteres.")
            : Result<string>.Success(normalizada);
    }

    private static Result<string> FalhaDaDescricao(string codigo, string mensagem) =>
        Result<string>.ValidationFailure([new FieldError("descricao", new DomainError(codigo, mensagem))]);

    private static IPoliticaDoTipoDeMotivo? ConferirTipo(List<FieldError> erros, TipoMotivoDeDecisao tipo)
    {
        IPoliticaDoTipoDeMotivo? politica = PoliticasDosTiposDeMotivo.Do(tipo);
        if (politica is null)
        {
            erros.Add(new FieldError("tipo", new DomainError(
                MotivoDeDecisaoErrorCodes.TipoObrigatorio,
                $"Tipo do motivo é obrigatório e deve ser um dos códigos aceitos: {TipoMotivoDeDecisaoCodigo.CodigosEmTexto}.")));
        }

        return politica;
    }

    private static void ConferirSentido(List<FieldError> erros, IPoliticaDoTipoDeMotivo? politica, SentidoDoMotivo sentido)
    {
        if (sentido == SentidoDoMotivo.Nenhum)
        {
            erros.Add(new FieldError("sentido", new DomainError(
                MotivoDeDecisaoErrorCodes.SentidoObrigatorio,
                $"Motivo de decisão deve declarar exatamente um sentido: {string.Join(" ou ", SentidoDoMotivoCodigo.Todos)}.")));
        }
        else if (politica is not null && !politica.AdmiteSentido(sentido))
        {
            erros.Add(new FieldError("sentido", new DomainError(
                MotivoDeDecisaoErrorCodes.SentidoNaoAdmitido,
                $"O tipo {politica.Tipo.ToCodigo()} não admite motivo {sentido.Rotulo()}.")));
        }
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

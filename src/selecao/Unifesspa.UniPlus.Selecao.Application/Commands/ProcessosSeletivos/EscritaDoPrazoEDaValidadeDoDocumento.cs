namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Lê os códigos do prazo de emissão e da validade do pedido e entrega os valores do domínio. Cada código que
/// o pedido traz é lido, e as recusas dos códigos vão juntas; a coerência entre eles é do domínio.
/// </summary>
internal static class EscritaDoPrazoEDaValidadeDoDocumento
{
    internal const string CaminhoDoPrazo = "requisitos.prazoDeEmissao";
    internal const string CaminhoDaValidade = "requisitos.validade";

    public static Result<PrazoDeEmissao?> Prazo(PrazoDeEmissaoInput? entrada) =>
        entrada is null ? Result<PrazoDeEmissao?>.Success(null) : PrazoDeclarado(entrada);

    public static Result<ValidadeDoDocumento?> Validade(ValidadeDoDocumentoInput? entrada) =>
        entrada is null ? Result<ValidadeDoDocumento?>.Success(null) : ValidadeDeclarada(entrada);

    private static Result<PrazoDeEmissao?> PrazoDeclarado(PrazoDeEmissaoInput entrada)
    {
        Result<EscolhaDoPrazoDeEmissao> escolha = EscolhaDoPrazoDeEmissaoDaEntrada.Ler(entrada.Escolha);
        Result<UnidadeDoPrazoDeEmissao?> unidade = Opcional(entrada.Unidade, UnidadeDoPrazoDeEmissaoDaEntrada.Ler);
        Result<DataDeReferenciaDoDocumento?> referencia = Referencia(entrada.Referencia);
        Result<EfeitoNaAnaliseDoDocumento?> efeito = Opcional(entrada.EfeitoForaDoPrazo, EfeitoNaAnaliseDoDocumentoDaEntrada.Ler);
        List<FieldError> recusas = RecusasDoPrazo(escolha, unidade, referencia, efeito);
        return recusas.Count > 0 ? Falha<PrazoDeEmissao>(recusas, CaminhoDoPrazo) : Criado(
            PrazoDeEmissao.Criar(escolha.Value, entrada.Valor, unidade.Value, referencia.Value, efeito.Value), CaminhoDoPrazo);
    }

    private static List<FieldError> RecusasDoPrazo(
        Result<EscolhaDoPrazoDeEmissao> escolha, Result<UnidadeDoPrazoDeEmissao?> unidade,
        Result<DataDeReferenciaDoDocumento?> referencia, Result<EfeitoNaAnaliseDoDocumento?> efeito) =>
        [.. escolha.Errors.NoCaminho("escolha"), .. unidade.Errors.NoCaminho("unidade"),
            .. referencia.Errors.NoCaminho("referencia"), .. efeito.Errors.NoCaminho("efeitoForaDoPrazo")];

    private static Result<ValidadeDoDocumento?> ValidadeDeclarada(ValidadeDoDocumentoInput entrada)
    {
        Result<EscolhaDaValidade> escolha = EscolhaDaValidadeDaEntrada.Ler(entrada.Escolha);
        Result<DataDeReferenciaDoDocumento?> referencia = Referencia(entrada.Referencia);
        Result<EfeitoNaAnaliseDoDocumento?> efeito = Opcional(entrada.EfeitoDoVencimento, EfeitoNaAnaliseDoDocumentoDaEntrada.Ler);
        List<FieldError> recusas = [.. escolha.Errors.NoCaminho("escolha"), .. referencia.Errors.NoCaminho("referencia"),
            .. efeito.Errors.NoCaminho("efeitoDoVencimento")];
        return recusas.Count > 0 ? Falha<ValidadeDoDocumento>(recusas, CaminhoDaValidade) : Criado(
            ValidadeDoDocumento.Criar(escolha.Value, referencia.Value, efeito.Value), CaminhoDaValidade);
    }

    private static Result<DataDeReferenciaDoDocumento?> Referencia(DataDeReferenciaDoDocumentoInput? entrada) =>
        entrada is null ? Result<DataDeReferenciaDoDocumento?>.Success(null) : ReferenciaDeclarada(entrada);

    private static Result<DataDeReferenciaDoDocumento?> ReferenciaDeclarada(DataDeReferenciaDoDocumentoInput entrada)
    {
        Result<TipoDaDataDeReferencia> tipo = TipoDaDataDeReferenciaDaEntrada.Ler(entrada.Tipo);
        if (tipo.IsFailure)
        {
            return Result<DataDeReferenciaDoDocumento?>.ValidationFailure([.. tipo.Errors.NoCaminho("tipo")]);
        }

        Result<DataDeReferenciaDoDocumento> criada = DataDeReferenciaDoDocumento.Criar(tipo.Value, entrada.Data, entrada.FaseId);
        return criada.IsFailure
            ? Result<DataDeReferenciaDoDocumento?>.ValidationFailure(criada.Errors)
            : Result<DataDeReferenciaDoDocumento?>.Success(criada.Value);
    }

    // O código ausente é a ausência do detalhe, que o domínio confere contra a escolha; o código presente e
    // desconhecido é recusa de leitura.
    private static Result<TEnum?> Opcional<TEnum>(string? codigo, Func<string?, Result<TEnum>> ler)
        where TEnum : struct, Enum
    {
        if (codigo is null)
        {
            return Result<TEnum?>.Success(null);
        }

        Result<TEnum> lido = ler(codigo);
        return lido.IsFailure ? Result<TEnum?>.ValidationFailure(lido.Errors) : Result<TEnum?>.Success(lido.Value);
    }

    private static Result<T?> Criado<T>(Result<T> criado, string caminho)
        where T : class =>
        criado.IsFailure ? Falha<T>(criado.Errors, caminho) : Result<T?>.Success(criado.Value);

    private static Result<T?> Falha<T>(IReadOnlyList<FieldError> recusas, string caminho)
        where T : class =>
        Result<T?>.ValidationFailure([.. recusas.NoCaminho(caminho)]);

    private static IEnumerable<FieldError> NoCaminho(this IEnumerable<FieldError> recusas, string caminho) =>
        recusas.Select(recusa => (recusa with { Field = recusa.Field ?? string.Empty }).NoCaminhoDe(caminho));
}

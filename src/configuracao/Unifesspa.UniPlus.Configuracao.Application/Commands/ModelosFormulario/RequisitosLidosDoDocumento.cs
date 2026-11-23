namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Os requisitos lidos de um documento do modelo, com as recusas da leitura: cada código que o pedido traz
/// é lido pelo catálogo, e o código desconhecido é recusado no caminho dele, a partir do documento. A
/// escolha de um prazo ou de uma validade informados é obrigatória; os demais códigos ausentes são a ausência
/// do detalhe. Que o valor, a unidade, a referência e o efeito combinem com a escolha não se confere no
/// modelo: quem confere é o processo, ao receber a cópia.
/// </summary>
/// <param name="PrazoDeEmissao">O prazo lido, ou nulo quando ausente ou sem escolha reconhecida.</param>
/// <param name="Validade">A validade lida, ou nula quando ausente ou sem escolha reconhecida.</param>
/// <param name="Recusas">As recusas da leitura, no caminho a partir do documento.</param>
internal sealed record RequisitosLidosDoDocumento(
    PrazoDeEmissaoDoModelo? PrazoDeEmissao, ValidadeDoDocumentoDoModelo? Validade, IReadOnlyList<FieldError> Recusas)
{
    private const string CaminhoDoPrazo = "requisitos.prazoDeEmissao";
    private const string CaminhoDaValidade = "requisitos.validade";

    /// <summary>Os requisitos declarados; nulos quando a leitura teve alguma recusa.</summary>
    public RequisitosDoDocumentoDoModelo? Declarados => Recusas.Count == 0 ? new(PrazoDeEmissao, Validade) : null;

    /// <summary>Lê os requisitos da entrada; a entrada ausente é "ainda não escolhidos".</summary>
    public static RequisitosLidosDoDocumento Ler(RequisitosDoDocumentoDoModeloInput? entrada)
    {
        List<FieldError> recusas = [];
        PrazoDeEmissaoDoModelo? prazo = LerPrazo(entrada?.PrazoDeEmissao, recusas);
        ValidadeDoDocumentoDoModelo? validade = LerValidade(entrada?.Validade, recusas);
        return new RequisitosLidosDoDocumento(prazo, validade, recusas);
    }

    private static PrazoDeEmissaoDoModelo? LerPrazo(PrazoDeEmissaoDoModeloInput? entrada, List<FieldError> recusas)
    {
        if (entrada is null)
        {
            return null;
        }

        EscolhaDoPrazoDeEmissao? escolha = Lido(EscolhaDoPrazoDeEmissaoDaEntrada.Ler(entrada.Escolha), $"{CaminhoDoPrazo}.escolha", recusas);
        UnidadeDoPrazoDeEmissao? unidade = Opcional(entrada.Unidade, UnidadeDoPrazoDeEmissaoDaEntrada.Ler, $"{CaminhoDoPrazo}.unidade", recusas);
        DataDeReferenciaDoDocumentoDoModelo? referencia = LerReferencia(entrada.Referencia, $"{CaminhoDoPrazo}.referencia", recusas);
        EfeitoNaAnaliseDoDocumento? efeito = Opcional(
            entrada.EfeitoForaDoPrazo, EfeitoNaAnaliseDoDocumentoDaEntrada.Ler, $"{CaminhoDoPrazo}.efeitoForaDoPrazo", recusas);
        return escolha is { } lida ? new PrazoDeEmissaoDoModelo(lida, entrada.Valor, unidade, referencia, efeito) : null;
    }

    private static ValidadeDoDocumentoDoModelo? LerValidade(ValidadeDoDocumentoDoModeloInput? entrada, List<FieldError> recusas)
    {
        if (entrada is null)
        {
            return null;
        }

        EscolhaDaValidade? escolha = Lido(EscolhaDaValidadeDaEntrada.Ler(entrada.Escolha), $"{CaminhoDaValidade}.escolha", recusas);
        DataDeReferenciaDoDocumentoDoModelo? referencia = LerReferencia(entrada.Referencia, $"{CaminhoDaValidade}.referencia", recusas);
        EfeitoNaAnaliseDoDocumento? efeito = Opcional(
            entrada.EfeitoDoVencimento, EfeitoNaAnaliseDoDocumentoDaEntrada.Ler, $"{CaminhoDaValidade}.efeitoDoVencimento", recusas);
        return escolha is { } lida ? new ValidadeDoDocumentoDoModelo(lida, referencia, efeito) : null;
    }

    private static DataDeReferenciaDoDocumentoDoModelo? LerReferencia(
        DataDeReferenciaDoDocumentoDoModeloInput? entrada, string caminho, List<FieldError> recusas)
    {
        if (entrada is null)
        {
            return null;
        }

        TipoDaDataDeReferencia? tipo = Lido(TipoDaDataDeReferenciaDaEntrada.Ler(entrada.Tipo), $"{caminho}.tipo", recusas);
        return tipo is { } lido ? new DataDeReferenciaDoDocumentoDoModelo(lido, entrada.FaseCodigo, entrada.Data) : null;
    }

    private static TEnum? Opcional<TEnum>(string? codigo, Func<string?, Result<TEnum>> ler, string campo, List<FieldError> recusas)
        where TEnum : struct, Enum =>
        codigo is null ? null : Lido(ler(codigo), campo, recusas);

    private static TEnum? Lido<TEnum>(Result<TEnum> leitura, string campo, List<FieldError> recusas)
        where TEnum : struct, Enum
    {
        if (leitura.IsFailure)
        {
            recusas.Add(new FieldError(campo, leitura.Error!));
            return null;
        }

        return leitura.Value;
    }
}

namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O formato dos códigos que o conteúdo do modelo cita: fase, tipo de documento, fato e opção.</summary>
public static class FormatoDosCodigosDoConteudo
{
    private static readonly CampoDeCodigo Fase = Campo("Código da fase", 60);
    private static readonly CampoDeCodigo TipoDocumento = Campo("Código do tipo de documento", 50);
    private static readonly CampoDeCodigo Fato = Campo("Código do fato", CodigoFatoCandidato.TamanhoMaximo);
    private static readonly CampoDeCodigo Opcao = Campo("Código da opção", 60);

    private static CampoDeCodigo Campo(string nome, int maximo) => new(nome, maximo,
        ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido, ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);

    internal static IEnumerable<FieldError> Conferir(ConteudoDoModelo conteudo) =>
        conteudo.Pressupostos.Select((valor, indice) => string.IsNullOrWhiteSpace(valor)
            ? null : Fato.Recusa(valor, $"pressupostos[{indice}]"))
            .Concat(conteudo.Documentos.ComCaminho().SelectMany(static d => Documento(d.Documento, d.Caminho)))
            .OfType<FieldError>();

    /// <summary>O formato de todos os códigos do documento lido; o código ausente também é recusado.</summary>
    public static IEnumerable<FieldError> ConferirDocumento(DocumentoExigidoDoModelo documento, string caminho)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return Documento(documento, caminho).OfType<FieldError>();
    }

    /// <summary>
    /// O formato dos códigos que o documento recusado na leitura trouxe, no caminho dado: o código da fase e o do tipo
    /// ausentes ou em branco já foram recusados na leitura e são pulados; o código de fase de uma referência, que a leitura não
    /// recusa em branco, é conferido como no documento lido, quando informado; e cada opção excetuada fica na posição que veio. Sem
    /// <paramref name="excetoPorOpcao"/>, o documento não tem exigência por opção.
    /// </summary>
    public static IEnumerable<FieldError> ConferirCodigosInformados(
        string? faseCodigo,
        string? tipoDocumentoCodigo,
        IEnumerable<(string Campo, string? Texto)> referencias,
        string? fatoPorOpcao,
        IReadOnlyList<string?>? excetoPorOpcao,
        string caminho) =>
        new[]
        {
            Informado(Fase, faseCodigo, $"{caminho}.faseCodigo"),
            Informado(TipoDocumento, tipoDocumentoCodigo, $"{caminho}.tipoDocumentoCodigo"),
        }
        .Concat(Referencias(referencias))
        .Concat(excetoPorOpcao is null ? [] : OpcoesInformadas(fatoPorOpcao, excetoPorOpcao, caminho))
        .OfType<FieldError>();

    private static FieldError? Informado(CampoDeCodigo campo, string? valor, string caminho) =>
        string.IsNullOrWhiteSpace(valor) ? null : campo.Recusa(valor, caminho);

    private static IEnumerable<FieldError?> OpcoesInformadas(string? fatoCodigo, IReadOnlyList<string?> exceto, string caminho) =>
        exceto.Select((valor, indice) => Informado(Opcao, valor, $"{caminho}.porOpcao.exceto[{indice}]"))
            .Prepend(Informado(Fato, fatoCodigo, $"{caminho}.porOpcao.fatoCodigo"));

    private static IEnumerable<FieldError?> Documento(DocumentoExigidoDoModelo documento, string caminho) =>
    [
        Fase.Recusa(documento.FaseCodigo, $"{caminho}.faseCodigo"),
        TipoDocumento.Recusa(documento.TipoDocumentoCodigo, $"{caminho}.tipoDocumentoCodigo"),
        .. Referencias(documento.Requisitos.TextosDasReferencias(caminho)),
        .. OpcoesLidas(documento.PorOpcao, caminho),
    ];

    private static IEnumerable<FieldError?> Referencias(IEnumerable<(string Campo, string? Texto)> referencias) =>
        referencias.Select(static referencia => referencia.Texto is null ? null : Fase.Recusa(referencia.Texto, referencia.Campo));

    private static IEnumerable<FieldError?> OpcoesLidas(ExigenciaPorOpcao? opcoes, string caminho) => opcoes is null
        ? []
        : opcoes.Exceto.Select((valor, indice) => Opcao.Recusa(valor, $"{caminho}.porOpcao.exceto[{indice}]"))
            .Prepend(Fato.Recusa(opcoes.FatoCodigo, $"{caminho}.porOpcao.fatoCodigo"));
}

namespace Unifesspa.UniPlus.Configuracao.Domain.Services;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

internal static class FormatoDosCodigosDoConteudo
{
    private static readonly CampoDeCodigo Fase = Campo("Código da fase", 60);
    private static readonly CampoDeCodigo TipoDocumento = Campo("Código do tipo de documento", 50);
    private static readonly CampoDeCodigo Fato = Campo("Código do fato", CodigoFatoCandidato.TamanhoMaximo);
    private static readonly CampoDeCodigo Opcao = Campo("Código da opção", 60);

    private static CampoDeCodigo Campo(string nome, int maximo) => new(nome, maximo,
        ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido, ModeloFormularioErrorCodes.CodigoDoConteudoFormatoInvalido);

    public static IEnumerable<FieldError> Conferir(ConteudoDoModelo conteudo) =>
        conteudo.Pressupostos.Select((valor, indice) => string.IsNullOrWhiteSpace(valor)
            ? null : Fato.Recusa(valor, $"pressupostos[{indice}]"))
            .Concat(conteudo.Documentos.ComCaminho().SelectMany(static d => ConferirDocumento(d.Documento, d.Caminho)))
            .OfType<FieldError>();

    private static IEnumerable<FieldError?> ConferirDocumento(DocumentoExigidoDoModelo documento, string caminho) =>
    [
        Fase.Recusa(documento.FaseCodigo, $"{caminho}.faseCodigo"),
        TipoDocumento.Recusa(documento.TipoDocumentoCodigo, $"{caminho}.tipoDocumentoCodigo"),
        .. ConferirReferencias(documento.Requisitos, caminho),
        .. ConferirOpcoes(documento.PorOpcao, caminho),
    ];

    private static IEnumerable<FieldError?> ConferirReferencias(RequisitosDoDocumentoDoModelo requisitos, string caminho) =>
        requisitos.TextosDasReferencias(caminho).Where(static referencia => referencia.Texto is not null)
            .Select(static referencia => Fase.Recusa(referencia.Texto, referencia.Campo));

    private static IEnumerable<FieldError?> ConferirOpcoes(ExigenciaPorOpcao? opcoes, string caminho) => opcoes is null
        ? []
        : opcoes.Exceto.Select((valor, indice) => Opcao.Recusa(valor, $"{caminho}.porOpcao.exceto[{indice}]"))
            .Prepend(Fato.Recusa(opcoes.FatoCodigo, $"{caminho}.porOpcao.fatoCodigo"));
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os cadastros que a definição dos documentos exigidos consulta: o tipo de documento, o catálogo de fatos
/// do candidato e os modelos de documento do processo. Guarda só as portas, e os handlers o recebem pronto.
/// </summary>
public sealed class CadastrosDosDocumentosExigidos(
    ITipoDocumentoReader tiposDeDocumento,
    IFatoCandidatoReader fatos,
    IModeloDeDocumentoRepository modelos)
{
    internal ITipoDocumentoReader TiposDeDocumento { get; } = tiposDeDocumento;

    internal IFatoCandidatoReader Fatos { get; } = fatos;

    internal IModeloDeDocumentoRepository Modelos { get; } = modelos;
}

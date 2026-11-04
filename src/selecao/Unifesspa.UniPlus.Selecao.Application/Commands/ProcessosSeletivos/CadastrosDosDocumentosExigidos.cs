namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os cadastros que a definição dos documentos exigidos consulta: o tipo de documento, o catálogo de fatos
/// do candidato e os modelos de documento do processo.
/// </summary>
internal sealed record CadastrosDosDocumentosExigidos(
    ITipoDocumentoReader TiposDeDocumento,
    IFatoCandidatoReader Fatos,
    IModeloDeDocumentoRepository Modelos);

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>Os cadastros que a aplicação de um modelo de formulário consulta.</summary>
internal sealed record CadastrosDaAplicacaoDoModelo(
    IFatoCandidatoReader Fatos,
    ITermoConsentimentoReader Termos,
    CadastrosDosDocumentosExigidos Documentos);

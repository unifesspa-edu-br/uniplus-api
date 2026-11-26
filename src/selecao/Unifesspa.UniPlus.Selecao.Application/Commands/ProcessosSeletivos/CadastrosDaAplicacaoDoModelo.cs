namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Os cadastros que a aplicação de um modelo de formulário consulta. Guarda só as portas, e o handler o recebe pronto.
/// </summary>
public sealed class CadastrosDaAplicacaoDoModelo(
    IFatoCandidatoReader fatos,
    ITermoConsentimentoReader termos,
    CadastrosDosDocumentosExigidos documentos)
{
    internal IFatoCandidatoReader Fatos { get; } = fatos;

    internal ITermoConsentimentoReader Termos { get; } = termos;

    internal CadastrosDosDocumentosExigidos Documentos { get; } = documentos;
}

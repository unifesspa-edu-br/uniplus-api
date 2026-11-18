namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Prepara o texto do documento que a pessoa enviou, na ordem das recusas do salvamento: a forma, inclusive os limites do texto
/// que será guardado, recusa o documento inteiro, com todas as causas; depois, o documento precisa nomear a unidade que o
/// possui. Nada é lido de cadastro nem decidido sobre acesso.
/// </summary>
public sealed class PreparacaoDoDocumentoDoRascunho(LeituraDoDocumentoDaConfiguracao leitura)
{
    private const string PonteiroDaUnidade = "/identificacao/unidadeAdministradoraCodigo";
    private const string PonteiroDoNome = "/identificacao/nome";

    public Result<DocumentoPreparado> Preparar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        Result<DocumentoLido> lido = leitura.Ler(texto);
        return lido.IsFailure ? Recusa(lido.Errors) : ComTextoGuardado(lido.Value!);
    }

    private static Result<DocumentoPreparado> ComTextoGuardado(DocumentoLido lido)
    {
        Result<string> guardado = TextoGuardadoDoDocumento.Gerar(lido.Documento);
        if (guardado.IsFailure)
        {
            return Recusa(guardado.Errors);
        }

        Result<DocumentoDoRascunho> documento = DocumentoDoRascunho.Criar(guardado.Value, lido.Nome);
        return documento.IsFailure
            ? Recusa(NoPonteiroDoDocumento(documento.Errors))
            : ComUnidade(documento.Value!, lido.UnidadeAdministradoraCodigo);
    }

    private static Result<DocumentoPreparado> Recusa(IReadOnlyList<FieldError> causas) =>
        Result<DocumentoPreparado>.ValidationFailure(RecusaDaForma.ComGrupo(causas));

    private static Result<DocumentoPreparado> ComUnidade(DocumentoDoRascunho documento, string? codigoDaUnidade) =>
        string.IsNullOrWhiteSpace(codigoDaUnidade)
            ? Result<DocumentoPreparado>.ValidationFailure([new FieldError(PonteiroDaUnidade, RascunhoDeProcessoSeletivo.UnidadeAdministradoraAusente)])
            : Result<DocumentoPreparado>.Success(new DocumentoPreparado(documento, codigoDaUnidade));

    private static IReadOnlyList<FieldError> NoPonteiroDoDocumento(IReadOnlyList<FieldError> erros) =>
        [.. erros.Select(static erro => new FieldError(erro.Field == nameof(DocumentoDoRascunho.Nome) ? PonteiroDoNome : null, erro.Error))];
}

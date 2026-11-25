namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A classificação congelada da exigência a que um arquivo responde, na versão fixada pela inscrição (ADR-0136). A que não
/// se resolve vale como sensível: na dúvida sobre o que o arquivo é, a permissão mais restrita é a que o abre.
/// </summary>
public sealed class ClassificacaoDaExigenciaDoArquivo
{
    private readonly IDocumentoDaInscricaoRepository _documentos;
    private readonly ContextoDaVersaoFixadaResolver _contexto;

    public ClassificacaoDaExigenciaDoArquivo(
        IDocumentoDaInscricaoRepository documentos, ContextoDaVersaoFixadaResolver contexto)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(contexto);

        _documentos = documentos;
        _contexto = contexto;
    }

    public async Task<ClassificacaoDeProtecao> ResolverAsync(
        ArquivoDaApresentacao arquivo, VinculoDaInscricao inscricao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(inscricao);

        ApresentacaoDeDocumento? apresentacao = await _documentos
            .ObterApresentacaoAsync(arquivo.ApresentacaoId, cancellationToken).ConfigureAwait(false);
        ClassificacaoDeProtecao? congelada = apresentacao is null
            ? null
            : await CongeladaAsync(apresentacao.ExigenciaId, inscricao, cancellationToken).ConfigureAwait(false);

        return ClassificacoesDeProtecao.Resolvida(congelada);
    }

    private async Task<ClassificacaoDeProtecao?> CongeladaAsync(Guid exigenciaId, VinculoDaInscricao inscricao, CancellationToken cancellationToken)
    {
        Result<ContextoDaVersaoFixada> contexto = await _contexto.ResolverAsync(inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, inscricao.OfertaCursoId, inscricao.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);

        return contexto.IsSuccess
            && ExigenciaDaInscricao.Localizar(contexto.Value!.Configuracao.RegrasDeComprovacao, exigenciaId) is { } localizada
                ? localizada.Exigencia.ClassificacaoProtecao
                : null;
    }
}

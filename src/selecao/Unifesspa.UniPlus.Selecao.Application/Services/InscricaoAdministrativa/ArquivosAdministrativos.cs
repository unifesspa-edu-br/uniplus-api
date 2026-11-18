namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os metadados dos arquivos confirmados, cada um com a classificação e a hipótese legal que a exigência congelou na
/// versão da inscrição. A exigência que a versão não tem deixa a classificação sem resolver, e o item vale como sensível
/// sem base legal.
/// </summary>
public static class ArquivosAdministrativos
{
    public static IReadOnlyList<ArquivoAdministrativoDto> Montar(
        IReadOnlyList<ApresentacaoDeDocumento> apresentacoes, IReadOnlyList<ArquivoDaApresentacao> arquivos, ConfiguracaoPublicada configuracaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(apresentacoes);
        ArgumentNullException.ThrowIfNull(arquivos);
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);

        Dictionary<Guid, ApresentacaoDeDocumento> porId = apresentacoes.ToDictionary(static a => a.Id);

        return
        [
            .. arquivos
                .Where(a => a.ConfirmadoEm is not null && porId.ContainsKey(a.ApresentacaoId))
                .OrderBy(static a => a.ConfirmadoEm)
                .ThenBy(static a => a.Id)
                .Select(a => Arquivo(a, porId[a.ApresentacaoId], configuracaoPublicada)),
        ];
    }

    private static ArquivoAdministrativoDto Arquivo(ArquivoDaApresentacao arquivo, ApresentacaoDeDocumento apresentacao, ConfiguracaoPublicada configuracaoPublicada)
    {
        (ClassificacaoDeProtecao classificacao, string? hipoteseLegal) = ProtecaoCongeladaDaExigencia(configuracaoPublicada, apresentacao.ExigenciaId);

        return new ArquivoAdministrativoDto(classificacao, hipoteseLegal)
        {
            ArquivoId = arquivo.Id,
            ExigenciaId = apresentacao.ExigenciaId,
            OcorrenciaId = apresentacao.OcorrenciaId,
            NomeDoDocumento = apresentacao.NomeDoDocumento,
            TipoDeMidia = arquivo.TipoDeMidia,
            Situacao = arquivo.Situacao,
            TamanhoBytes = arquivo.TamanhoBytes,
            HashSha256 = arquivo.HashSha256,
            ConfirmadoEm = arquivo.ConfirmadoEm!.Value,
        };
    }

    private static (ClassificacaoDeProtecao Classificacao, string? HipoteseLegal) ProtecaoCongeladaDaExigencia(
        ConfiguracaoPublicada configuracaoPublicada, Guid exigenciaId)
    {
        DocumentoExigido? exigencia = configuracaoPublicada.Exigencias.DocumentosExigidos.FirstOrDefault(d => d.Id == exigenciaId);

        return (
            exigencia?.ClassificacaoProtecao ?? ClassificacaoDeProtecao.Nenhuma,
            string.IsNullOrWhiteSpace(exigencia?.HipoteseLegal) ? null : exigencia.HipoteseLegal);
    }
}

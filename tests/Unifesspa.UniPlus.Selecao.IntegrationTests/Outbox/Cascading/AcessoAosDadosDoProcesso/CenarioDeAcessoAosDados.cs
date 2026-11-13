namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>
/// O processo publicado com as inscrições sobre as quais o acesso é decidido: a enviada de Fernanda, com um arquivo
/// verificado em cada classificação; o rascunho; a de Erick, com passaporte; a da servidora, que impede a revelação; e a
/// que tem um grupo repetível. Traz também as rotas administrativas de cada uma.
/// </summary>
internal sealed record CenarioDeAcessoAosDados(
    ProcessoPublicado Processo,
    Guid InscricaoId,
    ArquivosDaInscricao Arquivos,
    RascunhoDeInscricao Rascunho,
    Guid InscricaoDeErick,
    Guid InscricaoDaServidora,
    Guid InscricaoComGrupo)
{
    public string RotaDaLista() =>
        $"/api/selecao/processos-seletivos/{Processo.Id}/inscricoes";

    public string RotaDoDetalhe(Guid inscricaoId) =>
        $"{RotaDaLista()}/{inscricaoId}";

    public string RotaDaRevelacao(Guid inscricaoId, string fatoCodigo, string? ocorrencia = null) =>
        $"{RotaDoDetalhe(inscricaoId)}/campos/{fatoCodigo}/revelacao"
        + (ocorrencia is null ? string.Empty : $"?ocorrencia={ocorrencia}");

    public string RotaDoAcessoDoArquivo(Guid inscricaoId, Guid arquivoId) =>
        $"{RotaDoDetalhe(inscricaoId)}/documentos/arquivos/{arquivoId}/acesso";

    public string RotaDoAcesso(Guid arquivoId) =>
        RotaDoAcessoDoArquivo(InscricaoId, arquivoId);

    public string RotaDoAcessoDoRascunho() =>
        RotaDoAcessoDoArquivo(Rascunho.Id, Rascunho.ArquivoId);
}

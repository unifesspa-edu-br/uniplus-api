namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As causas do documento que não impedem o salvamento: as recusas do cadastro de reserva e da manifestação de interesse, as da
/// prova presencial e as da importação do resultado, pelas mesmas regras de domínio da gravação por dimensão e da publicação, e as citações de modelos que não se
/// resolvem. Cada causa aponta o ponto do documento, e as causas saem na ordem única do diagnóstico. O que depende do Geo é
/// conferido antes de gravar, para a espera por ele não ficar presa à trava da linha do rascunho.
/// </summary>
public sealed class DiagnosticoDoRascunho(ResolucaoDosModelosCitados modelos, ConferenciaDaProvaPresencialDoDocumento provaPresencial)
{
    /// <summary>Lê o documento uma vez e confere nele o que depende do Geo, sem tocar no rascunho.</summary>
    public async Task<DocumentoConferidoAntesDeGravar> ConferirAntesDeGravarAsync(DocumentoDoRascunho documento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documento);

        DeclaracoesDoDocumento declaracoes = DeclaracoesDoDocumentoLidas.De(JsonNode.Parse(documento.Conteudo)!.AsObject());
        ProvaPresencialConferida conferida = await provaPresencial.ConferirAsync(declaracoes.ProvaPresencial, cancellationToken).ConfigureAwait(false);

        return new DocumentoConferidoAntesDeGravar(declaracoes, conferida);
    }

    /// <summary>As causas do rascunho gravado: as já conferidas antes de gravar, as do cadastro de reserva e da manifestação, as da importação do resultado e as dos modelos.</summary>
    public async Task<IReadOnlyList<FieldError>> DiagnosticarAsync(
        RascunhoDeProcessoSeletivo rascunho, DocumentoConferidoAntesDeGravar conferido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rascunho);
        ArgumentNullException.ThrowIfNull(conferido);

        JsonObject documento = JsonNode.Parse(rascunho.Documento)!.AsObject();
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(conferido.Declaracoes);
        ResultadoDosModelosCitados citados = await modelos.ResolverAsync(rascunho.Id, documento, cancellationToken).ConfigureAwait(false);

        return OrdemDasCausas.Ordenar(
            [.. lidos.Origem.PartesRecusadas.Select(static parte => parte.Causa), .. conferido.ProvaPresencial.Causas(), .. CausasDaImportacao(conferido.Declaracoes), .. citados.Causas], documento);
    }

    private static IEnumerable<FieldError> CausasDaImportacao(DeclaracoesDoDocumento declaracoes) =>
        ImportacaoDoResultadoLida.Ler(declaracoes.Importacao, declaracoes.Fases, declaracoes.Identificacao.OrigemCandidatos == OrigemCandidatos.InscricaoPropria).Origem.PartesRecusadas.Select(static parte => parte.Causa);
}

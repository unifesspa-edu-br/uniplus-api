namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// As causas que o rascunho guardado já traz e que não impedem o salvamento: as recusas do cadastro de reserva e da manifestação
/// de interesse, pelas mesmas regras de domínio da gravação por dimensão, e as citações de modelos que não se resolvem. Cada causa
/// aponta o ponto do documento, e as causas saem na ordem única do diagnóstico.
/// </summary>
public sealed class DiagnosticoDoRascunho(ResolucaoDosModelosCitados modelos)
{
    public async Task<IReadOnlyList<FieldError>> DiagnosticarAsync(RascunhoDeProcessoSeletivo rascunho, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rascunho);

        JsonObject documento = JsonNode.Parse(rascunho.Documento)!.AsObject();
        CadastroDeReservaEManifestacaoLidos lidos = CadastroDeReservaEManifestacaoLidos.Ler(DeclaracoesDoDocumentoLidas.De(documento));
        ResultadoDosModelosCitados citados = await modelos.ResolverAsync(rascunho.Id, documento, cancellationToken).ConfigureAwait(false);

        return OrdemDasCausas.Ordenar(
            [.. lidos.Origem.PartesRecusadas.Select(static parte => parte.Causa), .. citados.Causas], documento);
    }
}

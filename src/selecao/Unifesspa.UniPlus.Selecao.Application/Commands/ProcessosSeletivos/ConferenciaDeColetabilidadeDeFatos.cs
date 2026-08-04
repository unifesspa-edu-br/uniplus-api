namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Reconfere, no momento do congelamento, que cada <see cref="FatoColetado"/> do processo ainda
/// satisfaz <see cref="ColetabilidadeDeFato.EhColetavel"/> contra o catálogo VIVO — o mesmo
/// predicado que <see cref="DefinirFatosColetadosCommandHandler"/> já aplica ao vincular o fato
/// (PUT /fatos-coletados), reaplicado aqui porque o catálogo pode reclassificar a
/// <c>Origem</c> de um fato depois que ele já virou <see cref="FatoColetado"/> — a migration que
/// reclassificou MODALIDADE de DECLARADO para DERIVADO é o caminho real que o projeto usa para
/// evoluir o catálogo. Sem esta reconferência, um vínculo morto seria congelado numa versão
/// append-only e, pela doutrina do agregado, irreparável depois.
/// </summary>
/// <remarks>
/// Espelha <see cref="ResolvedorMetadadosFatosCongelados"/> em estilo: helper estático
/// compartilhado pelos três handlers que congelam. Usa
/// <see cref="IFatoCandidatoReader.ListarAsync"/> (o catálogo inteiro, baixo volume) em vez de
/// uma consulta por código — todo <see cref="FatoColetado"/> do processo precisa ser revisitado,
/// ao contrário do congelamento de metadado, que só revisita os poucos fatos citados em gatilho
/// de documento.
/// </remarks>
internal static class ConferenciaDeColetabilidadeDeFatos
{
    public const string FatoColetadoNaoMaisDeclarado = "ProcessoSeletivo.FatoColetadoNaoMaisDeclarado";

    public static async Task<Result> ConferirAsync(
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

        if (processo.FatosColetados.Count == 0)
        {
            return Result.Success();
        }

        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, FatoCandidatoView> porCodigo = catalogo
            .ToDictionary(static f => f.Codigo, StringComparer.Ordinal);

        foreach (FatoColetado fatoColetado in processo.FatosColetados.OrderBy(static f => f.Ordem))
        {
            if (!porCodigo.TryGetValue(fatoColetado.FatoCodigo, out FatoCandidatoView? fato)
                || !ColetabilidadeDeFato.EhColetavel(fato))
            {
                return Result.Failure(new DomainError(
                    FatoColetadoNaoMaisDeclarado,
                    $"O fato coletado '{fatoColetado.FatoCodigo}' não é mais declarado/vinculado a campo de " +
                    "inscrição no catálogo de fatos do candidato — o catálogo mudou depois que este fato foi " +
                    "vinculado ao formulário, e o congelamento não persiste um vínculo que já não é coletável."));
            }
        }

        return Result.Success();
    }
}

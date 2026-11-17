namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// A fase que conclui o ciclo de recurso, citada pela chave. A declaração guarda o código canônico da fase citada, também quando a
/// leitura a recusou (o código é o que o documento escreveu): a fase recusada não é chave inexistente, e a conclusão do ciclo fica
/// suspensa pela parte recusada da lista de fases. A chave que nenhuma fase do documento tem vira parte recusada.
/// </summary>
internal static class LeituraDaFaseConcluinte
{
    internal static ValorLido<string?> Ler(FaseNoDocumento fase, IReadOnlyDictionary<string, string> codigosDasFasesPorChave)
    {
        string? chave = fase.Referencias.FaseConcluinteChave;
        if (chave is null)
        {
            return new(null, []);
        }

        return codigosDasFasesPorChave.TryGetValue(chave, out string? codigo) ? new(codigo, []) : new(null, [ChaveInexistente(fase)]);
    }

    private static RecusaDaParte ChaveInexistente(FaseNoDocumento fase) => new(
        FaseDeclarada.CampoDaFaseConcluinte,
        "faseConcluinteCodigo",
        fase.PosicaoNoDocumento,
        null,
        new DomainError(
            DocumentoDaConfiguracaoErrorCodes.ChaveCitadaInexistente,
            "A fase cita, como fase que conclui o ciclo de recurso, uma chave que nenhuma fase do documento tem."));
}

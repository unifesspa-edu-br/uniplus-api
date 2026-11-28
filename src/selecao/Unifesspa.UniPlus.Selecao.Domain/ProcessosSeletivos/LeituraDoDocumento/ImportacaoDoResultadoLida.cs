namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// A importação do resultado que a leitura do documento entregou como declaração, e a origem que guarda as recusas de forma: o
/// código ou a versão do leiaute que faltam, o leiaute repetido e a primeira fase que não é fase do documento. O bloco recusado fica
/// sem declaração, o processo com inscrição pelo sistema que declara importação só recebe a recusa de origem, e o bloco ausente ou sem relatório e sem fase é o processo por declarar. O que depende da origem dos candidatos e
/// do cronograma completo é conferido na publicação, não aqui. O identificador da fase é o da memória da leitura, que só liga a chave
/// à fase do documento.
/// </summary>
public sealed record ImportacaoDoResultadoLida(DeclaracaoDaImportacaoDoResultado? Declaracao, OrigemNoDocumento Origem)
{
    /// <summary>Confere o bloco do documento pelas regras de forma da declaração, contra as fases que o mesmo documento traz.</summary>
    public static ImportacaoDoResultadoLida Ler(ImportacaoNoDocumento? bloco, IReadOnlyList<FaseNoDocumento> fases, bool inscricaoPeloSistema)
    {
        ArgumentNullException.ThrowIfNull(fases);

        if (bloco is null || PorDeclarar(bloco))
        {
            return new(null, OrigemNoDocumento.Identidade);
        }

        return inscricaoPeloSistema
            ? new(null, OrigemNoDocumento.SoComPartesRecusadas([.. ConferenciaDaImportacaoDoResultado.RecusaDaOrigem(true).Select(DaImportacao)]))
            : Declarar(bloco, IdentificadorPelaChave(fases));
    }

    private static ParteRecusada DaImportacao(FieldError recusa) => ParteRecusada.DoBloco(ConferenciaDaImportacaoDoResultado.Campo, recusa);

    private static bool PorDeclarar(ImportacaoNoDocumento bloco) =>
        bloco.Relatorios.Count == 0 && bloco.PrimeiraFaseDepoisDaImportacaoChave is null;

    private static Dictionary<string, Guid> IdentificadorPelaChave(IReadOnlyList<FaseNoDocumento> fases) =>
        ChavesDasPartesLidas.IdPelaChave(fases.Select(static fase => (fase.Chave, Guid.CreateVersion7())));

    private static ImportacaoDoResultadoLida Declarar(ImportacaoNoDocumento bloco, Dictionary<string, Guid> identificadorPelaChave)
    {
        Guid? faseId = bloco.PrimeiraFaseDepoisDaImportacaoChave is { } chave && identificadorPelaChave.TryGetValue(chave, out Guid id) ? id : null;
        Result<DeclaracaoDaImportacaoDoResultado> declaracao = DeclaracaoDaImportacaoDoResultado.Declarar(bloco.Relatorios, faseId);
        IEnumerable<FieldError> daForma = declaracao.IsFailure ? declaracao.Errors : [];
        FieldError[] recusas = [.. daForma, .. RecusaDaFase(bloco, faseId)];

        return recusas.Length == 0
            ? new(declaracao.Value, OrigemNoDocumento.Identidade)
            : new(null, OrigemNoDocumento.SoComPartesRecusadas([.. recusas.Select(DaImportacao)]));
    }

    private static IEnumerable<FieldError> RecusaDaFase(ImportacaoNoDocumento bloco, Guid? faseId)
    {
        if (bloco.PrimeiraFaseDepoisDaImportacaoChave is not null && faseId is null)
        {
            yield return ConferenciaDaImportacaoDoResultado.PrimeiraFaseForaDoCronograma();
        }
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// Copia do modelo os termos de consentimento. A versão de termo removida do catálogo depois da composição
/// do modelo sai da cópia e volta no relatório; os termos que ficam passam pelo resolvedor da escrita do
/// formulário, contra o universo de fatos que o processo coleta e deriva depois da cópia.
/// </summary>
internal sealed class CopiaDosTermosDoModelo(ITermoConsentimentoReader termoConsentimentoReader, RelatorioDaAplicacaoDoModelo relatorio)
{
    public async Task<TermosSelecionados> SelecionarAsync(IReadOnlyList<TermoExigidoInput> termosDoModelo, CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoTermoConsentimentoView> versoes = await termoConsentimentoReader
            .ListarVersoesAsync([.. termosDoModelo.Select(static t => t.VersaoId).Distinct()], cancellationToken)
            .ConfigureAwait(false);
        Dictionary<Guid, VersaoTermoConsentimentoView> versaoPorId = versoes.ToDictionary(static v => v.VersaoId);
        List<int> indices = [];
        List<TermoExigidoInput> aCopiar = [];
        for (int i = 0; i < termosDoModelo.Count; i++)
        {
            if (VersaoVigente(termosDoModelo[i], versaoPorId))
            {
                indices.Add(i);
                aCopiar.Add(termosDoModelo[i]);
            }
        }

        return new TermosSelecionados(EscritaDosTermos.Ler(aCopiar), versaoPorId, indices, aCopiar);
    }

    public static List<TermoExigidoFormulario> Resolver(
        TermosSelecionados selecionados,
        ContextoDoCatalogo contexto,
        IReadOnlySet<string> universoDeFatos,
        List<FieldError> erros)
    {
        (List<TermoExigidoFormulario> termos, List<FieldError> errosDosTermos) =
            EscritaDosTermos.Resolver(selecionados.Lidos, contexto, selecionados.VersaoPorId, universoDeFatos);
        erros.AddRange(errosDosTermos.Select(e => PosicaoNoModelo.NoModelo(e, "termos", selecionados.IndicesNoModelo)));
        return termos;
    }

    private bool VersaoVigente(TermoExigidoInput termo, Dictionary<Guid, VersaoTermoConsentimentoView> versaoPorId)
    {
        if (versaoPorId.TryGetValue(termo.VersaoId, out VersaoTermoConsentimentoView? versao) && versao.TermoId == termo.TermoId)
        {
            return true;
        }

        relatorio.Descartados.Add(new ParteDescartadaDto(ParteDoModelo.Termo, termo.Codigo, MotivoDoDescarte.VersaoDeTermoRemovida));
        return false;
    }
}

/// <summary>Os termos que entram na cópia, lidos, com as versões do catálogo e o índice de cada termo no modelo.</summary>
internal sealed record TermosSelecionados(
    TermosLidos Lidos,
    IReadOnlyDictionary<Guid, VersaoTermoConsentimentoView> VersaoPorId,
    List<int> IndicesNoModelo,
    List<TermoExigidoInput> Entradas);

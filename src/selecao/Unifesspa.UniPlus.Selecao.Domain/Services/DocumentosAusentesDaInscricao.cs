namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Regras.Formularios;

using ValueObjects;

/// <summary>
/// Os documentos obrigatórios da inscrição que ainda faltam (UNI-REQ-0028). A decisão parte do estado
/// do NÓ da árvore, nunca da folha: num grupo "um ou outro" basta uma alternativa atendida, e a folha
/// que ficou pendente ao lado dela não falta. Dentro de um grupo repetível o estado é o de cada
/// ocorrência, e a falta sai por ocorrência. O que não é da inscrição — outra finalidade ou fase
/// posterior —, o documento não obrigatório e a exigência cujos espaços (competência, exercício ou ocorrências
/// esperadas) o envio ainda não registra não recusam: o arquivo não diz a qual espaço pertence, e recusar
/// tornaria a inscrição impossível de enviar.
/// </summary>
public static class DocumentosAusentesDaInscricao
{
    public static IReadOnlyList<DocumentoAusente> Apurar(ArvoreExigenciasCongelada arvore, ResultadoResolucaoArvore resultado)
    {
        ArgumentNullException.ThrowIfNull(arvore);
        ArgumentNullException.ThrowIfNull(resultado);

        Dictionary<(Guid No, string Entidade), EstadoSatisfacao> porEntidade = resultado.EstadosPorEntidade
            .ToDictionary(static e => (e.NoExigenciaId, e.EntidadeId), static e => e.Estado);
        List<DocumentoAusente> ausentes = [];
        foreach (NoExigencia raiz in arvore.Raizes.OrderBy(static r => r.Ordem))
        {
            Percorrer(raiz, null);
        }

        return ausentes;

        void Percorrer(NoExigencia no, string? entidadeId)
        {
            if (no.RepetePorEntidade is not null && entidadeId is null)
            {
                foreach (string ocorrencia in porEntidade.Keys.Where(k => k.No == no.Id).Select(static k => k.Entidade).Order(StringComparer.Ordinal))
                {
                    Percorrer(no, ocorrencia);
                }

                return;
            }

            EstadoSatisfacao estado = EstadoDe(no, entidadeId);
            if (no.Tipo == TipoNo.GrupoE)
            {
                // O E impossível por um OU irmão não esconde o que falta nos outros filhos.
                if (estado is not (EstadoSatisfacao.Pendente or EstadoSatisfacao.Impossivel))
                {
                    return;
                }

                foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
                {
                    Percorrer(filho, entidadeId);
                }

                return;
            }

            if (!NaoAtendido(estado))
            {
                return;
            }

            // A folha, ou o grupo "um ou outro" ainda não atendido: falta o que o grupo tem de pendente e
            // obrigatório; sem nenhuma alternativa obrigatória, o grupo não recusa.
            DocumentoExigido[] faltantes = [.. no.AchatarComDescendentes()
                .Where(n => n.Tipo == TipoNo.Folha && ConferivelPelasApresentacoes(n))
                .Where(n => NaoAtendido(EstadoDe(n, entidadeId)))
                .Select(static n => n.DocumentoExigido!)
                .Where(static d => d.Obrigatorio && d.Finalidade == FinalidadeFormulario.Inscricao)];
            if (faltantes.Length > 0)
            {
                ausentes.Add(new DocumentoAusente(faltantes, entidadeId));
            }
        }

        EstadoSatisfacao EstadoDe(NoExigencia no, string? entidadeId) =>
            entidadeId is null
                ? resultado.EstadosPorNo.GetValueOrDefault(no.Id, EstadoSatisfacao.Indeterminado)
                : porEntidade.GetValueOrDefault((no.Id, entidadeId), EstadoSatisfacao.Indeterminado);
    }

    /// <summary>A folha que conta apresentações, ou apresentações distintas: o envio registra só isso, e não o espaço de cada arquivo.</summary>
    private static bool ConferivelPelasApresentacoes(NoExigencia folha) =>
        folha.ChaveDistincao is null || (folha.ChaveDistincao == ChaveDistincao.Ocorrencia && folha.OcorrenciasEsperadas is null);

    /// <summary>Só o pendente falta: o indeterminado (ainda não se sabe se se aplica), o não aplicável e o impossível — que enviar mais nada resolve — não recusam.</summary>
    private static bool NaoAtendido(EstadoSatisfacao estado) => estado == EstadoSatisfacao.Pendente;
}

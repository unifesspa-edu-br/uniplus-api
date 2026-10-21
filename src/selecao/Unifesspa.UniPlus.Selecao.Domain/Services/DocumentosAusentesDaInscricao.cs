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
/// posterior — e o documento não obrigatório não recusam.
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

            if (!NaoAtendido(EstadoDe(no, entidadeId)))
            {
                return;
            }

            if (no.Tipo == TipoNo.GrupoE)
            {
                foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
                {
                    Percorrer(filho, entidadeId);
                }

                return;
            }

            // A folha, ou o grupo "um ou outro" ainda não atendido: falta o que o grupo tem de pendente e
            // obrigatório; sem nenhuma alternativa obrigatória, o grupo não recusa.
            DocumentoExigido[] faltantes = [.. no.AchatarComDescendentes()
                .Where(n => n.Tipo == TipoNo.Folha && NaoAtendido(EstadoDe(n, entidadeId)))
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

    /// <summary>Pendente ou impossível de atender: o indeterminado (ainda não se sabe se se aplica) e o não aplicável não faltam.</summary>
    private static bool NaoAtendido(EstadoSatisfacao estado) => estado is EstadoSatisfacao.Pendente or EstadoSatisfacao.Impossivel;
}

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
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade = resultado.EstadosPorEntidade
            .ToDictionary(static e => (e.NoExigenciaId, e.EntidadeId), static e => e.Estado);
        List<DocumentoAusente> ausentes = [];
        foreach (NoExigencia raiz in arvore.Raizes.OrderBy(static r => r.Ordem))
        {
            Percorrer(raiz, null, resultado, porEntidade, ausentes);
        }
        return ausentes;
    }

    private static void Percorrer(NoExigencia no, string? entidadeId, ResultadoResolucaoArvore resultado,
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade, List<DocumentoAusente> ausentes)
    {
        if (no.RepetePorEntidade is not null && entidadeId is null)
        {
            foreach (string ocorrencia in porEntidade.Keys.Where(k => k.No == no.Id).Select(static k => k.Entidade).Order(StringComparer.Ordinal))
            {
                Percorrer(no, ocorrencia, resultado, porEntidade, ausentes);
            }
            return;
        }
        PercorrerNoEscopo(no, entidadeId, resultado, porEntidade, ausentes);
    }

    private static void PercorrerNoEscopo(NoExigencia no, string? entidadeId, ResultadoResolucaoArvore resultado,
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade, List<DocumentoAusente> ausentes)
    {
        if (no.Tipo == TipoNo.GrupoE)
        {
            PercorrerGrupoE(no, entidadeId, resultado, porEntidade, ausentes);
            return;
        }
        if (EstadoDe(no, entidadeId, resultado, porEntidade) == SituacaoDaComprovacao.Pendente)
        {
            RegistrarAusentes(no, entidadeId, resultado, porEntidade, ausentes);
        }
    }

    private static void PercorrerGrupoE(NoExigencia no, string? entidadeId, ResultadoResolucaoArvore resultado,
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade, List<DocumentoAusente> ausentes)
    {
        if (EstadoDe(no, entidadeId, resultado, porEntidade) is not (SituacaoDaComprovacao.Pendente or SituacaoDaComprovacao.Impossivel))
        {
            return;
        }
        foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
        {
            Percorrer(filho, entidadeId, resultado, porEntidade, ausentes);
        }
    }

    private static void RegistrarAusentes(NoExigencia no, string? entidadeId, ResultadoResolucaoArvore resultado,
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade, List<DocumentoAusente> ausentes)
    {
        DocumentoExigido[] faltantes = [.. no.AchatarComDescendentes()
            .Where(static n => n.Tipo == TipoNo.Folha && (n.Distincao is null || n.Distincao.ConferivelPelaContagem))
            .Where(n => EstadoDe(n, entidadeId, resultado, porEntidade) == SituacaoDaComprovacao.Pendente)
            .Select(static n => n.DocumentoExigido!)
            .Where(static d => d.Obrigatorio && d.Finalidade == FinalidadeFormulario.Inscricao)];
        if (faltantes.Length > 0)
        {
            ausentes.Add(new DocumentoAusente(faltantes, entidadeId));
        }
    }

    private static SituacaoDaComprovacao EstadoDe(NoExigencia no, string? entidadeId, ResultadoResolucaoArvore resultado,
        Dictionary<(Guid No, string Entidade), SituacaoDaComprovacao> porEntidade) => entidadeId is null
        ? resultado.EstadosPorNo.GetValueOrDefault(no.Id, SituacaoDaComprovacao.Indeterminado)
        : porEntidade.GetValueOrDefault((no.Id, entidadeId), SituacaoDaComprovacao.Indeterminado);
}

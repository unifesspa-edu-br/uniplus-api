namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

internal sealed class ColetorDeDocumentosAvaliados(
    ResultadoDaComprovacao resultado, IReadOnlyDictionary<string, AvaliacaoGrupo> grupos, IReadOnlySet<Guid> deFasePosterior)
{
    private readonly List<DocumentoAvaliado> _documentos = [];

    internal List<DocumentoAvaliado> Coletar(RegrasDeComprovacaoDaVersao arvore)
    {
        foreach (NoExigencia raiz in arvore.Raizes.OrderBy(static r => r.Ordem))
        {
            ColetarNo(raiz, [], null);
        }

        return _documentos;
    }

    private bool GrupoAtendido(Guid grupoId, string? ocorrenciaId) =>
        (ocorrenciaId is not null
            && resultado.EstadosPorEntidade.FirstOrDefault(e => e.NoExigenciaId == grupoId && e.EntidadeId == ocorrenciaId) is { } daOcorrencia
                ? daOcorrencia.Estado
                : resultado.SituacoesPorRegra.GetValueOrDefault(grupoId, SituacaoDaComprovacao.Indeterminado)) == SituacaoDaComprovacao.Satisfeito;

    private void ColetarNo(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais, string? grupoRepetido)
    {
        string? repetido = no.RepetePorEntidade ?? grupoRepetido;
        if (no.Tipo == TipoNo.Folha)
        {
            ColetarDocumento(no, ancestrais, repetido);
            return;
        }

        IReadOnlyList<GrupoDaArvore> dosFilhos = AncestraisDosFilhos(no, ancestrais);
        foreach (NoExigencia filho in no.Filhos.OrderBy(static f => f.Ordem))
        {
            ColetarNo(filho, dosFilhos, repetido);
        }
    }

    private static IReadOnlyList<GrupoDaArvore> AncestraisDosFilhos(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais) =>
    [
        .. ancestrais,
        new GrupoDaArvore(no.Id, no.Tipo, no.Tipo == TipoNo.GrupoOu ? no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao : null,
            no.RepetePorEntidade, no.Tipo == TipoNo.GrupoOu ? no.Consequencia : null),
    ];

    private DocumentoAvaliado Avaliado(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais, string? repetido,
        StatusResolucaoExigencia status, string? ocorrenciaId)
    {
        DocumentoExigido documento = no.DocumentoExigido!;
        SituacaoDoDocumento situacao = DocumentosAvaliadosDaInscricao.Situacao(status, deFasePosterior.Contains(documento.Id));
        return new(documento, situacao, repetido, ocorrenciaId, ancestrais, no.RepetePorEntidade,
            no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao,
            DispensadoPeloGrupo: situacao != SituacaoDoDocumento.Satisfeita
                && ancestrais.Any(g => g.Tipo == TipoNo.GrupoOu && GrupoAtendido(g.GrupoId, ocorrenciaId)));
    }

    private void ColetarDocumento(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais, string? repetido)
    {
        if (repetido is null)
        {
            _documentos.Add(Avaliado(no, ancestrais, null, resultado.StatusPorExigencia.GetValueOrDefault(no.DocumentoExigido!.Id), null));
            return;
        }

        AvaliacaoGrupo? grupo = grupos.GetValueOrDefault(repetido);
        if (grupo?.Visivel == Ternario.Indeterminado)
        {
            _documentos.Add(Avaliado(no, ancestrais, repetido, StatusResolucaoExigencia.AplicabilidadeIndeterminada, null));
            return;
        }

        ColetarOcorrencias(no, ancestrais, repetido, grupo);
    }

    private void ColetarOcorrencias(NoExigencia no, IReadOnlyList<GrupoDaArvore> ancestrais, string repetido, AvaliacaoGrupo? grupo)
    {
        List<DocumentoAvaliado> porOcorrencia = [.. resultado.StatusPorEntidade
            .Where(s => s.DocumentoExigidoId == no.DocumentoExigido!.Id)
            .Select(s => Avaliado(no, ancestrais, repetido, s.Status, s.EntidadeId))];
        if (porOcorrencia.Count > 0)
        {
            _documentos.AddRange(porOcorrencia);
            return;
        }

        // Grupo pendente mantém a exigência indeterminada; só grupo oculto ou lista vazia válida a descartam.
        _documentos.Add(Avaliado(no, ancestrais, repetido,
            grupo?.Estado == EstadoFato.Indeterminado ? StatusResolucaoExigencia.AplicabilidadeIndeterminada : StatusResolucaoExigencia.NaoAplicavel,
            null));
    }
}

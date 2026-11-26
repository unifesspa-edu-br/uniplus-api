namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

internal sealed class ColetorDeDocumentosAvaliados(
    ResultadoDaComprovacao resultado, IReadOnlyDictionary<string, AvaliacaoGrupo> grupos, IReadOnlySet<Guid> deFasePosterior)
{
    private readonly List<DocumentoAvaliado> _documentos = [];

    internal List<DocumentoAvaliado> Coletar(RegrasDeComprovacaoDaVersao regras)
    {
        foreach (RegraDeComprovacao regraSemGrupo in regras.RegrasSemGrupo.OrderBy(static r => r.Ordem))
        {
            ColetarRegra(regraSemGrupo, [], null);
        }

        return _documentos;
    }

    private bool GrupoAtendido(Guid grupoId, string? ocorrenciaId) =>
        (ocorrenciaId is not null
            && resultado.SituacoesDeRegraPorOcorrencia.FirstOrDefault(e => e.RegraDeComprovacaoId == grupoId && e.OcorrenciaId == ocorrenciaId) is { } daOcorrencia
                ? daOcorrencia.Situacao
                : resultado.SituacoesPorRegra.GetValueOrDefault(grupoId, SituacaoDaComprovacao.AguardandoDados)) == SituacaoDaComprovacao.Cumprida;

    private void ColetarRegra(RegraDeComprovacao regra, IReadOnlyList<GrupoDeComprovacao> ancestrais, string? grupoRepetido)
    {
        string? repetido = regra.GrupoDoFormularioParaRepeticao ?? grupoRepetido;
        if (regra.Forma == FormaDeComprovacao.Documento)
        {
            ColetarDocumento(regra, ancestrais, repetido);
            return;
        }

        IReadOnlyList<GrupoDeComprovacao> dosItens = GruposDosItens(regra, ancestrais);
        foreach (RegraDeComprovacao item in regra.Itens.OrderBy(static f => f.Ordem))
        {
            ColetarRegra(item, dosItens, repetido);
        }
    }

    private static IReadOnlyList<GrupoDeComprovacao> GruposDosItens(RegraDeComprovacao regra, IReadOnlyList<GrupoDeComprovacao> ancestrais) =>
    [
        .. ancestrais,
        new GrupoDeComprovacao(regra.Id, regra.Forma, regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens ? regra.QuantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao : null,
            regra.GrupoDoFormularioParaRepeticao, regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens ? regra.Consequencia?.Codigo() : null),
    ];

    private DocumentoAvaliado Avaliado(RegraDeComprovacao regra, IReadOnlyList<GrupoDeComprovacao> ancestrais, string? repetido,
        StatusResolucaoExigencia status, string? ocorrenciaId)
    {
        DocumentoExigido documento = regra.DocumentoExigido!;
        SituacaoDoDocumento situacao = DocumentosAvaliadosDaInscricao.Situacao(status, deFasePosterior.Contains(documento.Id));
        return new(documento, situacao, repetido, ocorrenciaId, ancestrais, regra.GrupoDoFormularioParaRepeticao,
            regra.QuantidadeMinima ?? RegraDeComprovacao.QuantidadeMinimaPadrao,
            DispensadoPeloGrupo: situacao != SituacaoDoDocumento.Satisfeita
                && ancestrais.Any(g => g.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && GrupoAtendido(g.GrupoId, ocorrenciaId)));
    }

    private void ColetarDocumento(RegraDeComprovacao regra, IReadOnlyList<GrupoDeComprovacao> ancestrais, string? repetido)
    {
        if (repetido is null)
        {
            _documentos.Add(Avaliado(regra, ancestrais, null, resultado.SituacoesPorExigencia.GetValueOrDefault(regra.DocumentoExigido!.Id), null));
            return;
        }

        AvaliacaoGrupo? grupo = grupos.GetValueOrDefault(repetido);
        if (grupo?.Visivel == Ternario.Indeterminado)
        {
            _documentos.Add(Avaliado(regra, ancestrais, repetido, StatusResolucaoExigencia.AguardandoDados, null));
            return;
        }

        ColetarOcorrencias(regra, ancestrais, repetido, grupo);
    }

    private void ColetarOcorrencias(RegraDeComprovacao regra, IReadOnlyList<GrupoDeComprovacao> ancestrais, string repetido, AvaliacaoGrupo? grupo)
    {
        List<DocumentoAvaliado> porOcorrencia = [.. resultado.SituacoesDeExigenciaPorOcorrencia
            .Where(s => s.DocumentoExigidoId == regra.DocumentoExigido!.Id)
            .Select(s => Avaliado(regra, ancestrais, repetido, s.Situacao, s.OcorrenciaId))];
        if (porOcorrencia.Count > 0)
        {
            _documentos.AddRange(porOcorrencia);
            return;
        }

        // Grupo pendente mantém a exigência indeterminada; só grupo oculto ou lista vazia válida a descartam.
        _documentos.Add(Avaliado(regra, ancestrais, repetido,
            grupo?.Estado == EstadoFato.Indeterminado ? StatusResolucaoExigencia.AguardandoDados : StatusResolucaoExigencia.NaoExigida,
            null));
    }
}

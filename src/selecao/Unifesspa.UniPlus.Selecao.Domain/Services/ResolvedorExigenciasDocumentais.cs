namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Text.Json;

using Entities;
using Enums;
using Kernel.Results;
using ValueObjects;

/// <summary>
/// Resolve, para um candidato, o veredicto de cada <see cref="DocumentoExigido"/> de um
/// <see cref="BlocoExigenciasCongelado"/> — aplicável ou não (gatilho DNF, PR-b) e, quando
/// aplicável, satisfeita ou pendente (apresentação, direta ou via grupo de satisfação)
/// (Story #554, PR-e, issue #548, ADR-0076).
/// </summary>
/// <remarks>
/// <para>
/// <b>Puro</b>: sem I/O, sem <see cref="TimeProvider"/>, <c>static</c>. Opera inteiramente
/// sobre os três argumentos — o snapshot congelado, os fatos já resolvidos do candidato
/// (fora de escopo desta Story: quem os resolve é o motor de coleta) e as apresentações já
/// recebidas. <b>Nunca</b> é chamado dentro da transação de publicação (issue #548 §2) — o
/// bloco que ele recebe já está congelado havia tempo quando ele roda.
/// </para>
/// <para>
/// <b>Erros nomeados, nunca resultado vazio</b> (ADR-0076, mesmo princípio dos blocos
/// <c>classificacao</c>/<c>atendimento</c> do envelope, que também nunca congelam um
/// <c>nao_construido</c> quando a dimensão é obrigatória): snapshot ausente, bloco estruturalmente inválido (uma garantia
/// interna que o decoder já deveria manter, mas que este método — recebendo um
/// <see cref="BlocoExigenciasCongelado"/> de qualquer chamador — não pressupõe) e bloco
/// semanticamente inválido (a invariante de <c>GrupoSatisfacaoId</c> — "escopo
/// processo+fase", documentada em <see cref="DocumentoExigido.GrupoSatisfacaoId"/> —
/// violada) produzem <see cref="DomainError"/>, nunca um <see cref="ResultadoResolucaoExigencias"/>
/// vazio ou parcial.
/// </para>
/// </remarks>
public static class ResolvedorExigenciasDocumentais
{
    public static Result<ResultadoResolucaoExigencias> Resolver(
        BlocoExigenciasCongelado? bloco,
        IReadOnlyDictionary<string, JsonElement> fatosResolvidos,
        IReadOnlyDictionary<Guid, ApresentacaoDocumento> apresentacoesPorExigenciaId)
    {
        ArgumentNullException.ThrowIfNull(fatosResolvidos);
        ArgumentNullException.ThrowIfNull(apresentacoesPorExigenciaId);

        if (bloco is null)
        {
            return Result<ResultadoResolucaoExigencias>.Failure(new DomainError(
                "ResolvedorExigencias.SnapshotAusente",
                "Não há versão vigente congelada para resolver — nenhuma exigência documental para avaliar."));
        }

        if (bloco.Exigencias.Select(static e => e.Id).Distinct().Count() != bloco.Exigencias.Count)
        {
            return Result<ResultadoResolucaoExigencias>.Failure(new DomainError(
                "ResolvedorExigencias.BlocoEstruturalmenteInvalido",
                "O bloco congelado tem exigenciaId repetido — cada exigência precisa de identidade única (CA-09)."));
        }

        if (GrupoSatisfacaoForaDeEscopo(bloco.Exigencias) is { } grupoInvalido)
        {
            return Result<ResultadoResolucaoExigencias>.Failure(new DomainError(
                "ResolvedorExigencias.BlocoSemanticamenteInvalido",
                $"O grupo de satisfação '{grupoInvalido}' agrupa exigências de fases diferentes — o escopo do grupo é processo+fase."));
        }

        Dictionary<Guid, bool> aplicavelPorExigencia = bloco.Exigencias.ToDictionary(
            static e => e.Id, e => AvaliarAplicabilidade(e, fatosResolvidos));

        Dictionary<Guid, bool> satisfeitaDiretamentePorExigencia = bloco.Exigencias.ToDictionary(
            e => e.Id, e => apresentacoesPorExigenciaId.ContainsKey(e.Id));

        // O grupo de satisfação propaga a satisfação DIRETA para as demais exigências
        // aplicáveis do mesmo grupo — uma única apresentação, dentro do grupo, satisfaz
        // as outras (DocumentoExigido.GrupoSatisfacaoId, "semântica plena na PR-e").
        Dictionary<Guid, Guid> apresentacaoQueSatisfazOGrupo = [];
        foreach (IGrouping<Guid, DocumentoExigido> grupo in bloco.Exigencias
            .Where(e => e.GrupoSatisfacaoId is not null && aplicavelPorExigencia[e.Id])
            .GroupBy(static e => e.GrupoSatisfacaoId!.Value))
        {
            DocumentoExigido? satisfator = grupo.FirstOrDefault(e => satisfeitaDiretamentePorExigencia[e.Id]);
            if (satisfator is not null)
            {
                apresentacaoQueSatisfazOGrupo[grupo.Key] = apresentacoesPorExigenciaId[satisfator.Id].Id;
            }
        }

        List<ExigenciaResolvida> resolucoes = [];
        foreach (DocumentoExigido exigencia in bloco.Exigencias)
        {
            if (!aplicavelPorExigencia[exigencia.Id])
            {
                resolucoes.Add(new ExigenciaResolvida(exigencia.Id, StatusResolucaoExigencia.NaoAplicavel, null));
                continue;
            }

            if (satisfeitaDiretamentePorExigencia[exigencia.Id])
            {
                resolucoes.Add(new ExigenciaResolvida(
                    exigencia.Id, StatusResolucaoExigencia.Satisfeita, apresentacoesPorExigenciaId[exigencia.Id].Id));
                continue;
            }

            if (exigencia.GrupoSatisfacaoId is { } grupoId
                && apresentacaoQueSatisfazOGrupo.TryGetValue(grupoId, out Guid apresentacaoDoGrupo))
            {
                resolucoes.Add(new ExigenciaResolvida(exigencia.Id, StatusResolucaoExigencia.Satisfeita, apresentacaoDoGrupo));
                continue;
            }

            resolucoes.Add(new ExigenciaResolvida(exigencia.Id, StatusResolucaoExigencia.Pendente, null));
        }

        return Result<ResultadoResolucaoExigencias>.Success(new ResultadoResolucaoExigencias(resolucoes));
    }

    /// <summary>
    /// GERAL é sempre aplicável — o gatilho nunca é avaliado. CONDICIONAL depende do
    /// predicado DNF (PR-b): zero cláusulas vivas nunca casa com ninguém, mesma semântica
    /// de <see cref="PredicadoDnf.Avaliar"/> e de CA-01 ("exigida de ninguém").
    /// </summary>
    private static bool AvaliarAplicabilidade(DocumentoExigido exigencia, IReadOnlyDictionary<string, JsonElement> fatosResolvidos)
    {
        if (exigencia.Aplicabilidade == Aplicabilidade.Geral)
        {
            return true;
        }

        if (exigencia.Condicoes.Count == 0)
        {
            return false;
        }

        // O dado já foi validado quando a exigência foi escrita (CondicaoGatilho.Criar) —
        // mesmo raciocínio de CondicaoGatilho.ParaCondicaoDnf(), que esta chamada usa por
        // baixo: reidratar não revalida.
        PredicadoDnf predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. exigencia.Condicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;

        return predicado.Avaliar(fatosResolvidos);
    }

    /// <summary>
    /// O escopo de <see cref="DocumentoExigido.GrupoSatisfacaoId"/> é processo+fase — devolve
    /// o primeiro grupo que aparece em mais de uma <see cref="DocumentoExigido.ExigidoNaFaseId"/>,
    /// ou <see langword="null"/> se todos os grupos respeitarem o escopo.
    /// </summary>
    private static Guid? GrupoSatisfacaoForaDeEscopo(IReadOnlyList<DocumentoExigido> exigencias)
    {
        foreach (IGrouping<Guid, DocumentoExigido> grupo in exigencias
            .Where(static e => e.GrupoSatisfacaoId is not null)
            .GroupBy(static e => e.GrupoSatisfacaoId!.Value))
        {
            if (grupo.Select(static e => e.ExigidoNaFaseId).Distinct().Count() > 1)
            {
                return grupo.Key;
            }
        }

        return null;
    }
}

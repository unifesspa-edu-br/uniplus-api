namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Os fatos do processo com o instante em que aconteceram, que podem iniciar uma fase: o ato publicado, a importação de
/// notas, a inscrição recebida, o envio ao INEP e o início da avaliação no lugar. Só vale o fato que já tinha
/// acontecido no instante consultado.
/// </summary>
public sealed record FatosDoInicioDasFases(
    IReadOnlyList<AtoPublicadoNoLugar> Atos,
    IReadOnlyList<ImportacaoDeNotasNaEtapa> Importacoes,
    IReadOnlyList<DateTimeOffset> InscricoesRecebidas,
    IReadOnlyList<EnvioAoInepDaEtapa> EnviosAoInep,
    IReadOnlyList<AvaliacaoIniciadaNoLugar> AvaliacoesIniciadas)
{
    /// <summary>Os fatos que já tinham acontecido no instante, inclusive nele.</summary>
    public FatosDoInicioDasFases AteOInstante(DateTimeOffset instante) => new(
        [.. Atos.Where(a => a.Instante <= instante)],
        [.. Importacoes.Where(i => i.Instante <= instante)],
        [.. InscricoesRecebidas.Where(i => i <= instante)],
        [.. EnviosAoInep.Where(e => e.Instante <= instante)],
        [.. AvaliacoesIniciadas.Where(a => a.Instante <= instante)]);
}

/// <summary>
/// A publicação do ato de um item do edital, no lugar que o item declara: a fase de origem ou a etapa. Exatamente uma das
/// duas identidades está preenchida; a identidade vazia é a de uma fase ou etapa cujo envelope veio sem identificador.
/// </summary>
public sealed record AtoPublicadoNoLugar(Guid? FaseCanonicaOrigemId, Guid? EtapaProcessoId, DateTimeOffset Instante);

/// <summary>A importação de notas numa etapa: a importação guarda a versão divulgada contra a qual executou.</summary>
public sealed record ImportacaoDeNotasNaEtapa(Guid EtapaId, Guid VersaoId, DateTimeOffset Instante);

/// <summary>A geração do envio ao INEP numa etapa: o registro não guarda a versão, vale a vigente no instante.</summary>
public sealed record EnvioAoInepDaEtapa(Guid EtapaId, DateTimeOffset Instante);

/// <summary>O instante em que a avaliação começou num lugar, por decisão ou por registro de avaliação.</summary>
public sealed record AvaliacaoIniciadaNoLugar(LugarDaDecisao Lugar, DateTimeOffset Instante);

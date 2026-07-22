namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Entrada de recurso de uma fase, usada por <see cref="FaseCronogramaInput"/>.
/// Presença = a fase admite recurso (Story #851 §3.6) — sem enum, sem flag.
/// </summary>
public sealed record RegraRecursoFaseInput(
    string RegraCodigo,
    string RegraVersao,
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    string AtoAncoraCodigo,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);

/// <summary>
/// Entrada de uma fase do cronograma, usada por
/// <see cref="DefinirCronogramaFasesCommand"/>. O handler resolve
/// <see cref="FaseCanonicaId"/> e <see cref="TiposBancaIds"/> contra o módulo
/// Configuração e congela os atributos vigentes por valor (snapshot-copy,
/// ADR-0061) — o cliente não os declara diretamente.
/// </summary>
public sealed record FaseCronogramaInput(
    int Ordem,
    Guid FaseCanonicaId,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    string? AtoProduzidoCodigo,
    IReadOnlyList<Guid> TiposBancaIds,
    RegraRecursoFaseInput? RegraRecurso);

/// <summary>
/// Substitui integralmente o cronograma de fases do processo (Story #851, CA-06):
/// o handler resolve, via <c>IFaseCanonicaReader</c>/<c>ITipoBancaReader</c>/
/// <c>IPrecedenciaFaseReader</c> (módulo Configuração, ADR-0056) e
/// <c>ITipoAtoPublicadoReader</c> (módulo Publicações), os snapshots-copy e o grafo
/// de precedências, e delega a montagem/validação ao domínio.
/// </summary>
public sealed record DefinirCronogramaFasesCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<FaseCronogramaInput> Fases,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;

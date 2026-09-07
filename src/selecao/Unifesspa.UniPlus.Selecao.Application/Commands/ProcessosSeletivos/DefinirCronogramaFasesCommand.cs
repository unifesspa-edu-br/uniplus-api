namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Entrada de recurso de uma fase, usada por <see cref="FaseCronogramaInput"/>.
/// Presença = a fase admite recurso (Story #851 §3.6) — sem enum, sem flag.
/// </summary>
/// <param name="AtoAncoraCodigo">
/// Tipo de ato do qual o prazo conta o instante de publicação. É resolvido <b>dentro dos
/// produtos desta mesma fase</b> (<see cref="FaseCronogramaInput.Produtos"/>) e congelado
/// como a identidade daquele produto — ancorar na publicação de outra fase deixa de ser
/// exprimível, ainda que as duas declarem o mesmo tipo de ato.
/// </param>
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
/// Entrada de um produto publicado pela fase, usada por <see cref="FaseCronogramaInput"/>.
/// </summary>
/// <param name="AtoCodigo">Código do tipo de ato no catálogo de Publicações.</param>
/// <param name="Papel">
/// <c>PRELIMINAR</c>, <c>DEFINITIVO</c> ou ausente. Ausente é o caso do ato que não é
/// resultado: a fase publica um aviso sem que isso a torne produtora de resultado. O papel
/// só é aceito em tipo de ato que o catálogo declara resultado — o handler resolve.
/// </param>
public sealed record ProdutoDaFaseInput(string AtoCodigo, string? Papel);

/// <summary>
/// Entrada de uma banca requerida pela fase, usada por <see cref="FaseCronogramaInput"/>.
/// </summary>
/// <param name="TipoBancaId">
/// Id do <c>TipoBanca</c> vivo no cadastro de Configuração. O handler o resolve e congela
/// o par identidade-e-código por valor (snapshot-copy, ADR-0061).
/// </param>
/// <param name="CategoriasDocumentoIds">
/// O recorte de competência: as categorias de documento que esta banca julga, referenciadas
/// pelo id do cadastro e congeladas por valor como o tipo de banca. Vazio é declarável
/// quando o tipo já identifica a banca sozinho dentro da fase.
/// </param>
public sealed record BancaRequeridaInput(Guid TipoBancaId, IReadOnlyList<Guid> CategoriasDocumentoIds);

/// <summary>
/// Entrada de uma fase do cronograma, usada por
/// <see cref="DefinirCronogramaFasesCommand"/>. O handler resolve
/// <see cref="FaseCanonicaId"/> e <see cref="BancasRequeridas"/> contra o módulo
/// Configuração e congela os atributos vigentes por valor (snapshot-copy,
/// ADR-0061) — o cliente não os declara diretamente.
/// </summary>
/// <param name="Produtos">Tudo o que a fase publica, com o papel de cada publicação.</param>
/// <param name="FaseConcluinteCodigo">
/// Código canônico da fase que conclui o ciclo recursal desta, quando a própria fase não
/// publica a definitiva da matéria que abriu.
/// </param>
/// <param name="EmiteParecerIndividual">
/// Se a fase promete parecer individual por candidato — a promessa de que existirá; o
/// parecer é produzido na execução.
/// </param>
public sealed record FaseCronogramaInput(
    int Ordem,
    Guid FaseCanonicaId,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    IReadOnlyList<ProdutoDaFaseInput> Produtos,
    string? FaseConcluinteCodigo,
    bool EmiteParecerIndividual,
    IReadOnlyList<BancaRequeridaInput> BancasRequeridas,
    RegraRecursoFaseInput? RegraRecurso);

/// <summary>
/// Substitui integralmente o cronograma de fases do processo (Story #851, CA-06):
/// o handler resolve, via <c>IFaseCanonicaReader</c>/<c>ITipoBancaReader</c>/
/// <c>ICategoriaDocumentoReader</c>/<c>IPrecedenciaFaseReader</c> (módulo Configuração,
/// ADR-0056) e
/// <c>ITipoAtoPublicadoReader</c> (módulo Publicações), os snapshots-copy e o grafo
/// de precedências, e delega a montagem/validação ao domínio.
/// </summary>
public sealed record DefinirCronogramaFasesCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<FaseCronogramaInput> Fases,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;

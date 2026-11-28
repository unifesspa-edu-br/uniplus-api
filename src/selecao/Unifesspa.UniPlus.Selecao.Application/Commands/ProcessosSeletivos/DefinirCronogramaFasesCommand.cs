namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.ComponentModel;

using Domain.Enums;
using Domain.ValueObjects;

using Kernel.Domain.CodigosDeContrato;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Entrada de recurso de uma fase, usada por <see cref="FaseCronogramaInput"/>.
/// Presença = a fase admite recurso — sem enum, sem flag.
/// </summary>
/// <param name="AtoAncoraCodigo">
/// Tipo de ato do qual o prazo conta o instante de publicação. É resolvido <b>dentro dos
/// produtos desta mesma fase</b> (<see cref="FaseCronogramaInput.Produtos"/>) e congelado
/// como a identidade daquele produto — vincular à publicação de outra fase deixa de ser
/// exprimível, ainda que as duas declarem o mesmo tipo de ato.
/// </param>
/// <param name="PrazoValor">Valor do prazo de interposição; ausente é recusado pelo domínio.</param>
/// <param name="PrazoUnidade">Código da unidade do prazo de interposição; o domínio recusa o ausente e o desconhecido.</param>
/// <param name="AdmiteInstanciaSuperior">
/// Se o recurso desta fase admite instância superior. Obrigatório: o ausente virava <c>false</c> em silêncio, e o edital seria
/// congelado afirmando que o recurso termina na 1ª instância sem que ninguém tivesse decidido isso. Ausente ou nulo chega ao
/// domínio e é recusado (422).
/// </param>
/// <param name="PrazoSegundaInstanciaValor">Valor do prazo da 2ª instância; obrigatório com instância superior e recusado sem ela.</param>
/// <param name="PrazoSegundaInstanciaUnidade">Código da unidade do prazo da 2ª instância.</param>
/// <param name="EfeitoSuspensivo">Código do efeito suspensivo da 1ª instância; obrigatório.</param>
/// <param name="GuardaDaVaga">Código da guarda da vaga; ausente vale guardar até o resultado definitivo.</param>
/// <param name="Procedimentos">Como o recurso de cada matéria que a fase produz é revisado.</param>
public sealed record RegraRecursoFaseInput(
    string RegraCodigo,
    string RegraVersao,
    decimal? PrazoValor,
    [property: AceitaCodigosDe<UnidadePrazo>] string? PrazoUnidade,
    string AtoAncoraCodigo,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    bool? AdmiteInstanciaSuperior,
    decimal? PrazoSegundaInstanciaValor,
    [property: AceitaCodigosDe<UnidadePrazo>] string? PrazoSegundaInstanciaUnidade,
    [property: AceitaCodigosDe<EfeitoSuspensivo>] string? EfeitoSuspensivo,
    [property: AceitaCodigosDe<GuardaDaVaga>] string? GuardaDaVaga,
    IReadOnlyList<ProcedimentoDeRecursoInput>? Procedimentos = null);

/// <summary>
/// Entrada de um produto publicado pela fase, usada por <see cref="FaseCronogramaInput"/>.
/// </summary>
/// <param name="AtoCodigo">Código do tipo de ato no catálogo de Publicações.</param>
/// <param name="Papel">
/// <c>PRELIMINAR</c>, <c>DEFINITIVO</c> ou ausente. Ausente é o caso do ato que não é
/// resultado: a fase publica um aviso sem que isso a torne produtora de resultado. O papel
/// só é aceito em tipo de ato que o catálogo declara resultado — o handler resolve.
/// </param>
public sealed record ProdutoDaFaseInput(string AtoCodigo, [property: AceitaCodigosDe<PapelDaPublicacaoPrevista>] string? Papel);

/// <summary>
/// Entrada de uma banca requerida pela fase, usada por <see cref="FaseCronogramaInput"/>.
/// </summary>
/// <param name="TipoBancaId">
/// Id do <c>TipoBanca</c> vivo no cadastro de Configuração. O handler o resolve e congela
/// o par identidade-e-código por valor (cópia congelada, ADR-0061).
/// </param>
/// <param name="CategoriasDocumentoIds">
/// Os candidatos atendidos: as categorias de documento que esta banca julga, referenciadas
/// pelo id do cadastro e congeladas por valor como o tipo de banca. Vazio é declarável
/// quando o tipo já identifica a banca sozinho dentro da fase.
/// </param>
/// <param name="RegraDeDeliberacao">
/// Como a banca é composta e delibera. Opcional no rascunho; a publicação e a retificação a exigem.
/// </param>
/// <param name="Id">
/// O id da banca que a leitura devolveu. Com ele, a banca é a mesma e mantém a identidade ao mudar de tipo, de regra ou de
/// posição; sem ele, ou com id que não é de banca da fase, a banca é criada de novo.
/// </param>
public sealed record BancaRequeridaInput(
    Guid TipoBancaId,
    IReadOnlyList<Guid> CategoriasDocumentoIds,
    RegraDeDeliberacaoInput? RegraDeDeliberacao = null,
    Guid? Id = null, [property: AceitaCodigosDe<ConsequenciaDoDescumprimento>] string? ConsequenciaDaAusencia = null);

/// <summary>
/// Entrada de uma fase do cronograma, usada por
/// <see cref="DefinirCronogramaFasesCommand"/>. O handler resolve
/// <see cref="FaseCanonicaId"/> e <see cref="BancasRequeridas"/> contra o módulo
/// Configuração e congela os atributos vigentes por valor (cópia congelada,
/// ADR-0061) — o cliente não os declara diretamente.
/// </summary>
/// <param name="Produtos">Tudo o que a fase publica, com o papel de cada publicação.</param>
/// <param name="EmiteParecerIndividual">
/// Se a fase promete parecer individual por candidato — a promessa de que existirá; o
/// parecer é produzido na execução.
/// </param>
/// <param name="UnidadeResponsavelId">
/// Id da unidade da Unifesspa que responde pela fase. Opcional no rascunho; a publicação e
/// a retificação a exigem. O handler a resolve pela porta <c>IResolvedorDaUnidadeResponsavel</c> e congela id, sigla
/// e nome por valor (cópia congelada, ADR-0061).
/// </param>
/// <param name="TiposDeDecisao">
/// Os tipos de decisão que a fase declara produzir, como o cliente os escreveu. Ausente ou vazio: nenhum. A
/// ordem não importa, e a leitura os devolve em ordem canônica. Tirar um tipo mantém os motivos escolhidos daquele tipo: o
/// congelamento da versão recusa até que sejam retirados ou o tipo volte a ser declarado.
/// </param>
/// <param name="RegraDeHomologacao">
/// A regra com que a fase homologa as inscrições. O efeito da gravação sobre ela está na descrição do campo no contrato.
/// </param>
public sealed record FaseCronogramaInput(
    [property: Description("Posição da fase no cronograma, na ordem em que as fases acontecem.")]
    int Ordem,
    [property: Description("Identificador da fase no cadastro de fases da Configuração. O sistema copia os dados vigentes da fase, e o cliente não os declara.")]
    Guid FaseCanonicaId,
    [property: Description("Início do período da fase.")]
    DateTimeOffset? Inicio,
    [property: Description("Fim do período da fase.")]
    DateTimeOffset? Fim,
    [property: Description("Tudo o que a fase publica, com o papel de cada publicação.")]
    IReadOnlyList<ProdutoDaFaseInput> Produtos,
    [property: ObrigatorioNoContrato]
    [property: Description("Se a fase promete parecer individual por candidato. É só a promessa: o parecer é produzido na execução da fase. Obrigatório; ausente ou nulo é recusado (422).")]
    bool? EmiteParecerIndividual,
    [property: Description("As bancas que a fase exige.")]
    IReadOnlyList<BancaRequeridaInput> BancasRequeridas,
    [property: Description("Como o recurso da fase funciona: instâncias, prazos, efeito suspensivo, guarda da vaga e procedimentos.")]
    RegraRecursoFaseInput? RegraRecurso,
    [property: Description("Identificador da unidade da Unifesspa que responde pela fase. Opcional no rascunho; a publicação e a retificação o exigem.")]
    Guid? UnidadeResponsavelId = null,
    [property: AceitaCodigosDe<TipoMotivoDeDecisao>]
    [property: Description("Os tipos de decisão que a fase declara produzir. Ausente ou vazio: nenhum. A ordem não importa, e a leitura os devolve em ordem canônica. Tirar um tipo mantém os motivos escolhidos daquele tipo, e o congelamento da versão recusa até que sejam retirados ou o tipo volte a ser declarado.")]
    IReadOnlyList<string?>? TiposDeDecisao = null,
    [property: Description("Como a fase homologa as inscrições. A gravação substitui a fase inteira: se este campo vier ausente ou nulo, a fase deixa de homologar e a regra que ela tinha é apagada. Quem grava devolve a regra lida na consulta, mesmo quando não a altera.")]
    RegraDeHomologacaoInput? RegraDeHomologacao = null)
{
    /// <summary>
    /// A fase acontece no mesmo momento da fase de ordem imediatamente anterior, e não depois dela. Vale a partir da segunda fase. É
    /// substituição integral: omitir a marca equivale a <c>false</c>, e quem quer conservá-la a devolve.
    /// </summary>
    [Description("A fase acontece no mesmo momento da fase de ordem imediatamente anterior, e não depois dela. Vale a partir da segunda fase. A gravação substitui a fase inteira: omitida, a marca vale falso, e quem quer conservá-la a devolve.")]
    public bool NoMesmoMomentoDaAnterior { get; init; }
}

/// <summary>
/// Substitui integralmente o cronograma de fases do processo (CA-06):
/// o handler resolve, via <c>IFaseCanonicaReader</c>/<c>ITipoBancaReader</c>/
/// <c>ICategoriaDocumentoReader</c>/<c>IPrecedenciaFaseReader</c> (módulo Configuração,
/// ADR-0056) e
/// <c>ITipoAtoPublicadoReader</c> (módulo Publicações), os snapshots-copy e o grafo
/// de precedências, e delega a montagem/validação ao domínio.
/// </summary>
public sealed record DefinirCronogramaFasesCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<FaseCronogramaInput> Fases) : ICommand<Result>;

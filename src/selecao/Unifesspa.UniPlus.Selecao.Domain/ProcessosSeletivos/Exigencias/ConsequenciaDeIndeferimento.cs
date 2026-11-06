namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ConsequenciaDeIndeferimento
{
    /// <summary>
    /// Coerência entre a consequência de indeferimento declarada em cada
    /// <see cref="DocumentoExigido"/> e a ação da vaga, lida de
    /// <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/>, sem campo duplicado em
    /// <see cref="DocumentoExigido"/>. Avaliada depois de
    /// <see cref="PendenciasDasExigencias.PendenciaDasExigenciasDocumentais"/> e sem cache: uma mudança de
    /// gatilho ou de distribuição de vagas entre versões é sempre reavaliada.
    /// </summary>
    /// <remarks>
    /// Duas checagens:
    /// <list type="bullet">
    /// <item><c>REMOVE_VANTAGEM</c> exige vantagem viva no processo — a única
    /// vantagem modelada é <see cref="ProcessoSeletivo.BonusRegional"/> (Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015; só existe quando
    /// <see cref="ProcessoSeletivo.AplicaBonusRegional"/> é verdadeiro).</item>
    /// <item>Para cada modalidade que a exigência alcança pelas condições sobre os fatos cujos
    /// valores são modalidades (os mesmos do gate real, <see cref="Services.AvaliadorConformidadeLegal"/>),
    /// quando essa modalidade declara <c>AcaoQuandoIndeferido</c>, a consequência precisa
    /// ser idêntica — mesmo vocabulário fechado dos dois lados.</item>
    /// </list>
    /// </remarks>
    internal static DomainError? PendenciaDeCoerenciaDaConsequenciaDeIndeferimento(EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos)
        {
            if (exigencia.ConsequenciaIndeferimento is not { } consequencia)
            {
                continue;
            }

            if (ConsequenciaRemoveVantagemSemVantagemViva(estado, consequencia))
            {
                return new DomainError(
                    "DocumentoExigido.RemoveVantagemSemVantagemViva",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva (ex.: bônus regional) para remover.");
            }

            if (ModalidadeIncoerenteComConsequencia(consequencia, ModalidadesAlcancadasPor(estado, exigencia, fatosDeModalidade)) is { } modalidadeIncoerente)
            {
                return new DomainError(
                    "DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' declara consequência '{consequencia}', incoerente com a ação de indeferimento '{modalidadeIncoerente.AcaoQuandoIndeferido}' configurada para a modalidade '{modalidadeIncoerente.Codigo}'.");
            }
        }

        // os mesmos dois gates, estendidos ao grupo OU/N-de com consequência
        // própria (exigência de 1ª classe) — o alcance de modalidade é a união (OR) das
        // folhas descendentes (NoExigencia.PodeAlcancarModalidade).
        foreach (NoExigencia grupo in estado.NosExigencia.Where(static no => no.Tipo == TipoNo.GrupoOu && no.Consequencia is not null))
        {
            string consequenciaDoGrupo = grupo.Consequencia!;

            if (ConsequenciaRemoveVantagemSemVantagemViva(estado, consequenciaDoGrupo))
            {
                return new DomainError(
                    "NoExigencia.RemoveVantagemSemVantagemViva",
                    $"O grupo '{grupo.Id}' declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva (ex.: bônus regional) para remover.");
            }

            if (ModalidadeIncoerenteComConsequencia(consequenciaDoGrupo, ModalidadesAlcancadasPor(estado, grupo, fatosDeModalidade)) is { } modalidadeIncoerenteDoGrupo)
            {
                return new DomainError(
                    "NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga",
                    $"O grupo '{grupo.Id}' declara consequência '{consequenciaDoGrupo}', incoerente com a ação de indeferimento '{modalidadeIncoerenteDoGrupo.AcaoQuandoIndeferido}' configurada para a modalidade '{modalidadeIncoerenteDoGrupo.Codigo}'.");
            }
        }

        return null;
    }

    /// <summary>A consequência é REMOVE_VANTAGEM sem nenhuma vantagem viva no processo (bônus regional, Fator de Atenuação das Desigualdades de Acesso, UNI-REQ-0015).</summary>
    internal static bool ConsequenciaRemoveVantagemSemVantagemViva(EstadoDaConfiguracao estado, string? consequencia) =>
        consequencia == "REMOVE_VANTAGEM" && estado.BonusRegional is null;

    /// <summary>
    /// A primeira modalidade alcançada cuja <c>AcaoQuandoIndeferido</c> declarada é incoerente com a
    /// consequência — ou <see langword="null"/> quando todas as que declaram ação concordam. Devolve a
    /// ENTIDADE (não um bool) porque tanto o gate (mensagem nomeando oferta/modalidade) quanto o
    /// checklist (presença) derivam do mesmo cálculo, sem recomputar a busca duas vezes.
    /// </summary>
    internal static ModalidadeSelecionada? ModalidadeIncoerenteComConsequencia(
        string consequencia, IEnumerable<ModalidadeSelecionada> modalidadesAlcancadas)
    {
        string consequenciaComoAcaoDaVaga = NormalizarConsequenciaParaAcaoDaVaga(consequencia);
        return modalidadesAlcancadas.FirstOrDefault(modalidade => modalidade.AcaoQuandoIndeferido is { } acao
            && !string.Equals(acao, consequenciaComoAcaoDaVaga, StringComparison.Ordinal));
    }

    /// <summary>Existe <see cref="DocumentoExigido"/> (folha) com REMOVE_VANTAGEM sem vantagem viva.</summary>
    internal static bool ExisteExigenciaRemoveVantagemSemVantagemViva(EstadoDaConfiguracao estado) =>
        estado.DocumentosExigidos.Any(e => ConsequenciaRemoveVantagemSemVantagemViva(estado, e.ConsequenciaIndeferimento));

    /// <summary>Existe <see cref="DocumentoExigido"/> (folha) com consequência incoerente com a ação de alguma modalidade alcançada.</summary>
    internal static bool ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga(EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade) =>
        estado.DocumentosExigidos.Any(e => e.ConsequenciaIndeferimento is { } c
            && ModalidadeIncoerenteComConsequencia(c, ModalidadesAlcancadasPor(estado, e, fatosDeModalidade)) is not null);

    /// <summary>Existe grupo OU/N-de com consequência própria REMOVE_VANTAGEM sem vantagem viva.</summary>
    internal static bool ExisteGrupoRemoveVantagemSemVantagemViva(EstadoDaConfiguracao estado) =>
        estado.NosExigencia.Any(no => no.Tipo == TipoNo.GrupoOu && ConsequenciaRemoveVantagemSemVantagemViva(estado, no.Consequencia));

    /// <summary>Existe grupo OU/N-de com consequência própria incoerente com a ação de alguma modalidade alcançada.</summary>
    internal static bool ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga(EstadoDaConfiguracao estado, FatosDeModalidade fatosDeModalidade) =>
        estado.NosExigencia.Any(no => no.Tipo == TipoNo.GrupoOu && no.Consequencia is { } c
            && ModalidadeIncoerenteComConsequencia(c, ModalidadesAlcancadasPor(estado, no, fatosDeModalidade)) is not null);

    /// <summary>
    /// Ponte entre os dois vocabulários fechados de "ação de indeferimento":
    /// <see cref="DocumentoExigido.ConsequenciaIndeferimento"/> usa <c>RECLASSIFICA_AC</c>, mas
    /// <see cref="ModalidadeSelecionada.AcaoQuandoIndeferido"/> — snapshot-copy do cadastro
    /// de <c>Modalidade</c> no módulo Configuração
    /// (<c>ck_modalidade_acao_quando_indeferido</c>) — usa <c>RECLASSIFICAR_AC</c>. É o
    /// MESMO conceito com grafias diferentes, nunca unificadas entre os dois módulos;
    /// comparar os tokens crus reprovaria sempre o único caso de reclassificação
    /// realmente coerente que existe.
    /// </summary>
    internal static string NormalizarConsequenciaParaAcaoDaVaga(string consequencia) =>
        consequencia == "RECLASSIFICA_AC" ? "RECLASSIFICAR_AC" : consequencia;

    /// <summary>
    /// Modalidades ofertadas que o gatilho da exigência PODE alcançar — verificação
    /// estrutural (<see cref="DocumentoExigido.PodeAlcancarModalidade"/>), não a avaliação
    /// factual de <see cref="DocumentoExigido.AplicavelPara"/> que o gate de conformidade
    /// legal usa (<see cref="Services.AvaliadorConformidadeLegal"/>) — aqui não há
    /// candidato real, só a pergunta "existe combinação de fatos em que este documento se
    /// torna exigido para alguém nesta modalidade?".
    /// </summary>
    internal static IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(EstadoDaConfiguracao estado, DocumentoExigido exigencia, FatosDeModalidade fatosDeModalidade) =>
        estado.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => exigencia.PodeAlcancarModalidade(modalidade.Codigo, fatosDeModalidade));

    /// <summary>Mesma checagem estrutural acima, para um nó de grupo — <see cref="NoExigencia.PodeAlcancarModalidade"/> é a união (OR) das folhas descendentes.</summary>
    internal static IEnumerable<ModalidadeSelecionada> ModalidadesAlcancadasPor(EstadoDaConfiguracao estado, NoExigencia no, FatosDeModalidade fatosDeModalidade) =>
        estado.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(modalidade => no.PodeAlcancarModalidade(modalidade.Codigo, fatosDeModalidade));
}

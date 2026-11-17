namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que cada origem de candidatos exige da homologação das inscrições (UNI-REQ-0264, 0340 e 0342). A fase que homologa é a
/// que declara a regra de homologação: o tipo de decisão que ela declara é conferido na cobertura dos motivos, e o resultado
/// que a regra aponta, na declaração da fase. Nada aqui lê o código canônico da fase, o tipo de processo, o nome do exame, a
/// taxa ou a existência de etapa. Cada exigência julga só o que se pode saber: a parte que a leitura do documento recusou
/// deixa a regra sem julgar, e o item do painel fica pendente.
/// </summary>
internal static class ExigenciaDaHomologacaoPorOrigem
{
    private const string CampoDasFases = "fases";
    private const string CampoDaRegra = "regraDeHomologacao";
    private const string CampoDasAlternativas = "regraDeHomologacao.alternativas";

    /// <summary>
    /// As causas da exigência, na ordem em que o autor as corrige: a fase ausente, o ciclo da importação externa e a confirmação
    /// do INEP. A falta da fase de coleta é conferida antes, pelo cronograma.
    /// </summary>
    internal static IReadOnlyList<FieldError> Causas(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. FaseQueHomologa(origem, cronograma, etapas).Causas,
            .. CicloDaImportacaoExterna(origem, cronograma).Causas,
            .. ConfirmacaoDoInep(cronograma, etapas).Causas,
        ];
    }

    /// <summary>Os itens do painel, cada um derivado do veredicto da exigência de que a conferência tira as causas.</summary>
    internal static IReadOnlyList<ItemConformidade> Itens(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            Item("cronograma_homologacao_das_inscricoes_exigida", "Cronograma: o processo tem a fase que homologa as inscrições", FaseQueHomologa(origem, cronograma, etapas)),
            Item("cronograma_homologacao_das_inscricoes_com_ciclo_recursal", "Cronograma: a homologação da importação externa tem o resultado preliminar, o período de recurso e o definitivo", CicloDaImportacaoExterna(origem, cronograma)),
            Item("cronograma_homologacao_das_inscricoes_com_confirmacao_do_inep", "Cronograma: a homologação das inscrições de quem usa a nota do ENEM tem a confirmação do INEP entre as condições", ConfirmacaoDoInep(cronograma, etapas)),
        ];
    }

    private static ItemConformidade Item(string codigo, string mensagem, Veredicto veredicto) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, veredicto.Conforme);

    // Sem a fase que tem a regra não há resultado nem condição a conferir, e a recusa é só desta. Com fase recusada pela leitura
    // não se sabe se a regra está nela.
    private static Veredicto FaseQueHomologa(OrigemCandidatos origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        if (cronograma.Fases.Any(TemARegra))
        {
            return Veredicto.Atendido;
        }

        return origem == OrigemCandidatos.Nenhuma || cronograma.TemParteRecusadaNaListaDeFases || RegraRecusadaEmAlgumaFase(cronograma) || (origem != OrigemCandidatos.ImportacaoExterna && etapas.UsoDaNotaDoEnemIndeterminado)
            ? Veredicto.SemJulgar
            : Veredicto.Recusado([new FieldError(CampoDasFases, FaseAusente(origem, etapas))]);
    }

    private static bool RegraRecusadaEmAlgumaFase(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.Any(static fase => fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao));

    private static bool TemARegra(FaseDeclarada fase) =>
        !fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao) && fase.Atos.RegraDeHomologacao is not null;

    private static DomainError FaseAusente(OrigemCandidatos origem, EtapasDoProcesso etapas) => origem switch
    {
        OrigemCandidatos.ImportacaoExterna => new DomainError(
            ProcessoSeletivoErrorCodes.ImportacaoExternaSemHomologacaoDasInscricoes,
            "O processo importa candidatos de fora, e nenhuma fase do cronograma tem a regra de homologação das inscrições."),
        _ when etapas.UsaNotaDoEnem => new DomainError(
            ProcessoSeletivoErrorCodes.ImportacaoDeExameSemHomologacaoDasInscricoes,
            "O processo usa a nota do ENEM, e nenhuma fase do cronograma tem a regra de homologação das inscrições."),
        _ => new DomainError(
            ProcessoSeletivoErrorCodes.InscricaoPropriaSemHomologacaoDasInscricoes,
            "A origem dos candidatos é inscrição própria, e nenhuma fase do cronograma tem a regra de homologação das inscrições."),
    };

    // Basta uma fase completa. A fase que pode estar completa sem que se saiba, por parte recusada na leitura, impede concluir
    // que nenhuma está.
    private static Veredicto CicloDaImportacaoExterna(OrigemCandidatos origem, CronogramaDoProcesso cronograma)
    {
        if (origem != OrigemCandidatos.ImportacaoExterna)
        {
            return origem == OrigemCandidatos.Nenhuma ? Veredicto.SemJulgar : Veredicto.Atendido;
        }

        CicloDaFase[] fases = [.. cronograma.Fases.Select(CicloDaFase.De).Where(static ciclo => TemARegra(ciclo.Fase))];

        return fases.Any(static ciclo => ciclo.Estado == EstadoDoCiclo.Completo) ? Veredicto.Atendido
            : cronograma.TemParteRecusadaNaListaDeFases || RegraRecusadaEmAlgumaFase(cronograma) || fases.Any(static ciclo => ciclo.Estado == EstadoDoCiclo.Indeterminado) ? Veredicto.SemJulgar
            : PrimeiraFaseIncompleta(fases);
    }

    // Sem nenhuma completa, a recusa é a da primeira na ordem do cronograma, para o veredicto não depender da ordem da coleção.
    private static Veredicto PrimeiraFaseIncompleta(CicloDaFase[] fases)
    {
        FieldError[] recusa =
        [
            .. fases
                .Where(static ciclo => ciclo.Estado == EstadoDoCiclo.Incompleto)
                .OrderBy(static ciclo => ciclo.Fase.Ordem)
                .Take(1)
                .Select(static ciclo => CronogramaDoProcesso.NaFase(ciclo.Posicao, CampoDaRegra, CicloIncompleto(ciclo.Fase))),
        ];

        return recusa.Length == 0 ? Veredicto.SemJulgar : Veredicto.Recusado(recusa);
    }

    private static DomainError CicloIncompleto(FaseDeclarada fase) => new(
        ProcessoSeletivoErrorCodes.ImportacaoExternaSemJanelaDeRecurso,
        $"A fase '{fase.Codigo}' homologa as inscrições e precisa publicar o resultado preliminar e o definitivo do resultado que a regra aponta, com o período de recurso iniciado pelo preliminar.");

    // Com nota do ENEM, cada regra aceita precisa conter a confirmação, inclusive quando está vazia.
    // A regra descartada pela leitura deixa a fase sem julgamento, e o item do painel permanece pendente.
    private static Veredicto ConfirmacaoDoInep(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.UsoDaNotaDoEnemIndeterminado ? Veredicto.SemJulgar
        : etapas.UsaNotaDoEnem ? InepDasFases(cronograma)
        : Veredicto.Atendido;

    private static Veredicto InepDasFases(CronogramaDoProcesso cronograma)
    {
        (FaseDeclarada Fase, int Posicao)[] semOInep =
            [.. cronograma.Fases.Select(static (fase, posicao) => (Fase: fase, Posicao: posicao)).Where(static par => SemOInep(par.Fase))];

        return new(
            [.. semOInep.Where(static par => !par.Fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeHomologacao)).Select(static par => CausaDoInep(par.Fase, par.Posicao))],
            cronograma.TemParteRecusadaNaListaDeFases || RegraRecusadaEmAlgumaFase(cronograma));
    }

    private static bool SemOInep(FaseDeclarada fase) => fase.Atos.RegraDeHomologacao is { TemConfirmacaoDoInep: false };

    private static FieldError CausaDoInep(FaseDeclarada fase, int posicao) => CronogramaDoProcesso.NaFase(
        posicao,
        CampoDasAlternativas,
        new DomainError(
            HomologacaoDasInscricoesErrorCodes.ConfirmacaoDoInepAusente,
            $"O processo usa a nota do ENEM, e a regra de homologação da fase '{fase.Codigo}' não tem a confirmação do INEP entre as condições."));

    private enum EstadoDoCiclo
    {
        Completo,
        Incompleto,
        JaRecusada,
        Indeterminado,
    }

    // O resultado ausente ou que não é produto da fase já é recusa da declaração dela: aqui a fase não se julga, e não conta como completa.
    private readonly record struct CicloDaFase(FaseDeclarada Fase, int Posicao, EstadoDoCiclo Estado)
    {
        internal static CicloDaFase De(FaseDeclarada fase, int posicao) => new(fase, posicao, EstadoDe(fase));

        private static EstadoDoCiclo EstadoDe(FaseDeclarada fase)
        {
            if (fase.Atos.RegraDeHomologacao?.ResultadoAtoCodigo is not { } resultado)
            {
                return EstadoDoCiclo.JaRecusada;
            }

            return fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeRecurso) ? EstadoDoCiclo.Indeterminado
                : Completo(fase.Atos, resultado) ? EstadoDoCiclo.Completo
                : fase.ParteRecusadaEm(FaseDeclarada.ListaDosProdutos) ? EstadoDoCiclo.Indeterminado
                : fase.Atos.PublicaComPapel(resultado) ? EstadoDoCiclo.Incompleto
                : EstadoDoCiclo.JaRecusada;
        }

        private static bool Completo(AtosDaFase atos, string resultado) =>
            atos.TiposFechadosNaPropriaFase().Contains(resultado, StringComparer.Ordinal)
            && string.Equals(atos.AtoDaAncoraDoRecurso, resultado, StringComparison.Ordinal);
    }

    private readonly record struct Veredicto(IReadOnlyList<FieldError> Causas, bool Indeterminado)
    {
        internal static Veredicto Atendido => new([], Indeterminado: false);

        internal static Veredicto SemJulgar => new([], Indeterminado: true);

        internal static Veredicto Recusado(IReadOnlyList<FieldError> causas) => new(causas, Indeterminado: false);

        internal bool Conforme => Causas.Count == 0 && !Indeterminado;
    }
}

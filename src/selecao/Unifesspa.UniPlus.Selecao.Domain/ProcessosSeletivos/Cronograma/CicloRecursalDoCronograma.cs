namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

internal static class CicloRecursalDoCronograma
{
    /// <summary>
    /// As violações da conclusão do ciclo recursal, uma por fase mal declarada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Três estados legítimos, e só eles: a fase que não publica preliminar não declara
    /// conclusão; a que publica o definitivo de cada preliminar conclui a si mesma; a que
    /// deixa algum preliminar sem o definitivo do mesmo tipo declara qual fase a conclui.
    /// </para>
    /// <para>
    /// <b>A declaração vence a inferência.</b> Havendo <see cref="FaseCronograma.FaseConcluinteCodigo"/>,
    /// é ela que é conferida — está no cronograma, publica definitiva, não antecede —, e a
    /// única declaração recusada por si só é a fase apontando para si mesma, que não descreve
    /// travessia nenhuma. A auto-conclusão só decide onde o operador não declarou nada.
    /// </para>
    /// <para>
    /// <b>Matéria.</b> A matéria é o tipo de ato. A fase concluinte declarada fecha o ciclo de
    /// cada matéria que a fase concluída deixa aberta, isto é, de cada preliminar sem o
    /// definitivo do mesmo tipo na própria fase: ela precisa publicar o definitivo com o mesmo
    /// tipo. O definitivo de outro tipo não fecha o ciclo e o deixaria aberto para sempre.
    /// A auto-conclusão, quando nada é declarado, também é por matéria: a fase só conclui a si
    /// mesma quando publica o definitivo de cada tipo cujo preliminar publica. A fase que
    /// publica o resultado preliminar e só o gabarito definitivo deixa o ciclo do resultado
    /// aberto e precisa declarar a concluinte.
    /// </para>
    /// <para>
    /// Dentro de uma fase as checagens são encadeadas — não faz sentido conferir a ordem de
    /// uma concluinte que não está no cronograma —, então a primeira recusa é a que orienta
    /// e as seguintes não são avaliadas. Entre fases não há encadeamento: todas são
    /// percorridas.
    /// </para>
    /// <para>
    /// Sem I/O e sem navegação — só o que já está em memória (CA-11).
    /// </para>
    /// </remarks>
    internal static List<FieldError> ConferirConclusaoDoCicloRecursal(IReadOnlyList<FaseCronograma> fases)
    {
        List<FieldError> violacoes = [];

        for (int indice = 0; indice < fases.Count; indice++)
        {
            FaseCronograma fase = fases[indice];
            string campo = $"fases[{indice}].faseConcluinteCodigo";

            if (!fase.PublicaResultadoPreliminar)
            {
                if (fase.FaseConcluinteCodigo is not null)
                {
                    violacoes.Add(new(campo, new DomainError(
                        "ProcessoSeletivo.ConclusaoDeclaradaSemPreliminar",
                        $"A fase '{fase.Codigo}' declara a fase '{fase.FaseConcluinteCodigo}' como concluinte, mas não publica nenhum resultado preliminar — não há ciclo recursal a concluir.")));
                }

                continue;
            }

            if (fase.FaseConcluinteCodigo is not { } concluinteCodigo)
            {
                // Sem declaração, a fase conclui a si mesma só quando publica o definitivo de
                // cada matéria que abre: o definitivo de outro tipo não fecha o ciclo.
                if (fase.TiposComCicloAberto().FirstOrDefault() is { } tipoSemDefinitivoProprio)
                {
                    violacoes.Add(new(campo, new DomainError(
                        "ProcessoSeletivo.ConclusaoNaoDeclarada",
                        $"A fase '{fase.Codigo}' publica o resultado preliminar do ato '{tipoSemDefinitivoProprio}' e não publica o definitivo do mesmo tipo — publique-o na própria fase ou declare qual fase do cronograma a conclui.")));
                }

                continue;
            }

            // Apontar para si mesma não descreve travessia nenhuma: ou a fase conclui a
            // própria matéria publicando a definitiva, e então não há o que declarar, ou não
            // conclui, e a concluinte é outra fase.
            if (string.Equals(concluinteCodigo, fase.Codigo, StringComparison.Ordinal))
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.ConclusaoDeclaradaEmFaseQueSeConclui",
                    $"A fase '{fase.Codigo}' declara a si mesma como concluinte — a fase que encerra o ciclo recursal de outra é sempre distinta dela; para concluir o próprio ciclo, a fase publica o resultado definitivo e não declara concluinte.")));
                continue;
            }

            FaseCronograma? concluinte = fases
                .FirstOrDefault(f => string.Equals(f.Codigo, concluinteCodigo, StringComparison.Ordinal));
            if (concluinte is null)
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.FaseConcluinteForaDoCronograma",
                    $"A fase '{fase.Codigo}' declara '{concluinteCodigo}' como concluinte, e nenhuma fase do cronograma tem esse código.")));
                continue;
            }

            if (!concluinte.PublicaResultadoDefinitivo)
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo",
                    $"A fase '{concluinteCodigo}', declarada como concluinte de '{fase.Codigo}', não publica nenhum resultado definitivo.")));
                continue;
            }

            string? tipoSemDefinitivo = fase.TiposComCicloAberto()
                .FirstOrDefault(tipo => !concluinte.PublicaResultadoDefinitivoDe(tipo));
            if (tipoSemDefinitivo is not null)
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.FaseConcluinteSemResultadoDefinitivo",
                    $"A fase '{concluinteCodigo}', declarada como concluinte de '{fase.Codigo}', não publica o resultado definitivo do ato '{tipoSemDefinitivo}', o mesmo tipo do preliminar que '{fase.Codigo}' abre.")));
                continue;
            }

            // O tipo que a fase já fecha sozinha não tem o que a concluinte fechar: com o
            // definitivo nos dois lugares, o ciclo teria dois definitivos sem regra que diga qual
            // o fecha.
            string? tipoComDoisDefinitivos = fase.TiposFechadosNaPropriaFase()
                .FirstOrDefault(concluinte.PublicaResultadoDefinitivoDe);
            if (tipoComDoisDefinitivos is not null)
            {
                violacoes.Add(new(campo, new DomainError(
                    ProcessoSeletivoErrorCodes.ConclusaoDuplicada,
                    $"A fase '{fase.Codigo}' já publica o resultado definitivo do ato '{tipoComDoisDefinitivos}', e a fase '{concluinte.Codigo}', declarada como concluinte, o publica também — o ciclo ficaria com dois definitivos. Remova a declaração da concluinte de '{fase.Codigo}' ou o definitivo repetido de uma das duas fases.")));
                continue;
            }

            if (concluinte.Ordem <= fase.Ordem)
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.FaseConcluinteAntecedeAConcluida",
                    $"A fase '{concluinteCodigo}' (ordem {concluinte.Ordem}) conclui '{fase.Codigo}' (ordem {fase.Ordem}) — a concluinte não pode anteceder a fase que ela encerra.")));
                continue;
            }

            // A ordem já garante a precedência declarada; a janela é a segunda evidência, e
            // só existe quando as duas fases a declaram (uma fase NA_EXECUCAO sem data é
            // estado válido, §3.2).
            if (fase.Inicio is { } inicioConcluida
                && concluinte.Inicio is { } inicioConcluinte
                && inicioConcluinte < inicioConcluida)
            {
                violacoes.Add(new(campo, new DomainError(
                    "ProcessoSeletivo.FaseConcluinteComecaAntesDaConcluida",
                    $"A janela da fase '{concluinteCodigo}' começa em {inicioConcluinte:O}, antes de '{fase.Codigo}' ({inicioConcluida:O}) — a concluinte não pode começar antes da fase que ela encerra.")));
            }
        }

        return violacoes;
    }

    /// <summary>
    /// A primeira violação do recorte de competência no cronograma corrente, ou
    /// <see langword="null"/> quando toda fase identifica cada banca que requer.
    /// </summary>
    /// <remarks>
    /// O gate devolve uma; <see cref="ChecklistDeConformidade.AvaliarConformidade"/> projeta o mesmo predicado num
    /// item só. As duas metades vêm da MESMA travessia
    /// (<see cref="ConferirRecorteDeCompetencia"/>), que por sua vez chama o predicado de
    /// <c>FaseCronograma</c> que a fábrica, a reidratação e o decodificador do envelope
    /// também chamam.
    /// </remarks>
    internal static DomainError? PendenciaDoRecorteDeCompetencia(EstadoDaConfiguracao estado)
    {
        List<FieldError> violacoes = ConferirRecorteDeCompetencia([.. estado.CronogramaFases]);

        return violacoes.Count > 0 ? violacoes[0].Error : null;
    }

    /// <summary>
    /// As violações do recorte de competência, uma por banca mal declarada, com o campo que
    /// localiza cada uma.
    /// </summary>
    /// <remarks>Sem I/O e sem navegação — só o que já está em memória.</remarks>
    internal static List<FieldError> ConferirRecorteDeCompetencia(IReadOnlyList<FaseCronograma> fases)
    {
        List<FieldError> violacoes = [];

        for (int indice = 0; indice < fases.Count; indice++)
        {
            violacoes.AddRange(fases[indice].ViolacoesDoRecorteDeCompetencia()
                .Select(violacao => violacao with { Field = $"fases[{indice}].{violacao.Field}" }));
        }

        return violacoes;
    }

    /// <summary>
    /// A primeira violação da âncora do prazo de recurso no cronograma corrente, ou
    /// <see langword="null"/> quando toda fase que admite recurso ancora num produto
    /// preliminar seu.
    /// </summary>
    /// <remarks>
    /// O gate devolve uma; <see cref="ChecklistDeConformidade.AvaliarConformidade"/> projeta o mesmo predicado num
    /// item só. As duas metades vêm da MESMA travessia
    /// (<see cref="ConferirAncoraDoRecurso"/>), que por sua vez chama o predicado de
    /// <c>FaseCronograma</c> que a fábrica, a reidratação e o decodificador do envelope
    /// também chamam — não há um segundo <c>if</c> para lembrar de sincronizar.
    /// </remarks>
    internal static DomainError? PendenciaDaAncoraDoRecurso(EstadoDaConfiguracao estado)
    {
        List<FieldError> violacoes = ConferirAncoraDoRecurso([.. estado.CronogramaFases]);

        return violacoes.Count > 0 ? violacoes[0].Error : null;
    }

    /// <summary>
    /// A primeira violação da âncora das janelas de recurso das etapas, ou
    /// <see langword="null"/> quando toda janela se ancora onde pode. Chama o mesmo predicado de
    /// <see cref="EtapaProcesso"/> que a gravação das janelas chama.
    /// </summary>
    internal static DomainError? PendenciaDaAncoraDoRecursoDasEtapas(EstadoDaConfiguracao estado)
    {
        foreach (EtapaProcesso etapa in estado.Etapas)
        {
            if (etapa.ViolacaoDaAncoraDoRecurso() is { } violacao)
            {
                return violacao;
            }
        }

        return null;
    }

    /// <summary>
    /// As violações da âncora do prazo de recurso, uma por fase mal declarada, com o campo
    /// que localiza cada uma.
    /// </summary>
    /// <remarks>Sem I/O e sem navegação — só o que já está em memória.</remarks>
    internal static List<FieldError> ConferirAncoraDoRecurso(IReadOnlyList<FaseCronograma> fases)
    {
        List<FieldError> violacoes = [];

        for (int indice = 0; indice < fases.Count; indice++)
        {
            if (fases[indice].ViolacaoDaAncoraDoRecurso() is { } violacao)
            {
                violacoes.Add(violacao with { Field = $"fases[{indice}].{violacao.Field}" });
            }
        }

        return violacoes;
    }

    /// <summary>
    /// A primeira violação da conclusão do ciclo recursal no cronograma corrente, ou
    /// <see langword="null"/> quando todas as fases que abrem ciclo o fecham.
    /// </summary>
    /// <remarks>
    /// O gate devolve uma; <see cref="ChecklistDeConformidade.AvaliarConformidade"/> projeta o mesmo predicado num
    /// item só. As duas metades vêm da MESMA travessia
    /// (<see cref="ConferirConclusaoDoCicloRecursal"/>), que é o que mantém a
    /// bicondicional de pé sem um segundo <c>if</c> para lembrar de sincronizar.
    /// </remarks>
    internal static DomainError? PendenciaDaConclusaoDoCicloRecursal(EstadoDaConfiguracao estado)
    {
        List<FieldError> violacoes =
        [
            .. CicloRecursalDoCronograma.ConferirConclusaoDoCicloRecursal([.. estado.CronogramaFases]),
            .. RegrasDasEtapas.ConferirFechamentoDoCicloDasEtapas([.. estado.Etapas]),
        ];

        return violacoes.Count > 0 ? violacoes[0].Error : null;
    }
}

namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Parâmetros aplicados de <see cref="Entities.RegraRecursoFase"/> — os parâmetros que o
/// admin preenche ao ancorar o prazo de recurso de uma fase na regra
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Os DOIS pares de suspensividade, cada um independente e anulável.</b> O recurso
/// contra um ato da fase fica sob a unidade responsável pela fase de origem do ato, inclusive
/// quando chega à instância superior, administrativa ou judicial, e qualquer das duas pode
/// surgir em qualquer fase. A suspensividade pode ser distinta em cada instância: a
/// configuração diz se a 1ª e a instância superior bloqueiam, e a ausência do par <b>é</b> a
/// desativação — não há <c>if</c> por módulo nem por tipo de instância.
/// </para>
/// <para>
/// Não é uma discriminated union: <see cref="Entities.RegraRecursoFase"/> só admite a
/// regra <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> (CA-02 recusa qualquer outra), então este
/// VO é a única variante — sem discriminador, sem <c>switch</c>.
/// </para>
/// </remarks>
/// <param name="PrazoValor">Magnitude do prazo de interposição (1ª instância — o Uni+ gere só esta). Em dias úteis, exige valor inteiro.</param>
/// <param name="PrazoUnidade">Unidade do prazo de interposição — só <see cref="UnidadePrazo.DiasUteis"/> e <see cref="UnidadePrazo.Horas"/> são declaráveis; <see cref="Entities.RegraRecursoFase.Criar"/> recusa dia corrido em runtime.</param>
/// <param name="SuspensividadePrimeiraInstanciaValor">Magnitude da janela de suspensividade da 1ª instância, ou <see langword="null"/> — não bloqueia.</param>
/// <param name="SuspensividadePrimeiraInstanciaUnidade">Unidade da suspensividade da 1ª instância.</param>
/// <param name="AdmiteInstanciaSuperior">Se a janela admite recurso em instância superior. Obrigatória e sem valor padrão: quem configura decide, para que nenhum edital seja congelado sem dizer se o recurso termina na 1ª instância.</param>
/// <param name="SuspensividadeSegundaInstanciaValor">Magnitude da janela de suspensividade da instância superior, ou <see langword="null"/> — não bloqueia. Só pode ser declarada quando a janela admite instância superior.</param>
/// <param name="SuspensividadeSegundaInstanciaUnidade">Unidade da suspensividade da instância superior.</param>
public sealed record ParametrosRegraPrazoRecurso(
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    bool AdmiteInstanciaSuperior,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);

/// <summary>
/// Por que os parâmetros de um recurso foram recusados. O código de erro NÃO é montado aqui: ele é
/// literal em cada entidade, senão a cobertura do registro de erros não consegue enxergá-lo.
/// </summary>
public enum MotivoDaRecusaDosParametros
{
    Nenhum = 0,
    PrazoNaoPositivo,
    PrazoEmDiasCorridos,
    PrazoSemUnidadeDeclaravel,
    PrazoEmFracaoDeDiaUtil,
    SuspensividadeNaoPositiva,
    SuspensividadeUnidadeNaoDeclaravel,
    SuspensividadeIncompleta,
    SuspensividadeSuperiorSemInstanciaSuperior,
}

/// <summary>A recusa: o motivo, que vira código, e a mensagem já redigida.</summary>
public readonly record struct RecusaDosParametrosDoRecurso(MotivoDaRecusaDosParametros Motivo, string Mensagem);

/// <summary>
/// As invariantes que <see cref="ParametrosRegraPrazoRecurso"/> prova sozinho, sem saber quem o
/// carrega — prazo de interposição e os dois pares de suspensividade.
/// </summary>
/// <remarks>
/// <para>
/// Mora aqui, e não na entidade, porque o VO é o mesmo nos dois donos: a regra da fase
/// (<see cref="Entities.RegraRecursoFase"/>, 0..1) e a janela da etapa
/// (<see cref="Entities.RecursoDaEtapa"/>, 0..*). Enquanto a validação vivia privada na
/// primeira, a segunda aceitava prazo zero, dia corrido e par de suspensividade pela metade —
/// e o candidato recorria numa janela que fecha no instante em que abre.
/// </para>
/// <para>
/// A assimetria real entre os dois donos é de <b>âncora</b> — a fase conta sempre da publicação
/// do ato; a etapa também admite a ciência individual —, e ela não alcança estas regras. A
/// recusa de dia corrido, em particular, fala do prazo em que o CANDIDATO tem de agir: feriado
/// encolhe a janela dele qualquer que seja o instante que iniciou a contagem.
/// </para>
/// <para>
/// Devolve o MOTIVO, não o erro pronto: o código de erro nomeia quem recusou, e precisa ser
/// literal em cada entidade para que a cobertura do registro o alcance.
/// </para>
/// </remarks>
public static class ValidacaoDosParametrosDoRecurso
{
    /// <summary>Valida o prazo de interposição e os dois pares de suspensividade.</summary>
    public static IReadOnlyList<RecusaDosParametrosDoRecurso> Validar(ParametrosRegraPrazoRecurso parametros)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        RecusaDosParametrosDoRecurso?[] recusas = [
            ValidarPrazo(parametros),
            ValidarParDeSuspensividade(parametros.SuspensividadePrimeiraInstanciaValor, parametros.SuspensividadePrimeiraInstanciaUnidade, "1ª instância"),
            ValidarSuspensividadeSuperior(parametros)];
        return [.. recusas.OfType<RecusaDosParametrosDoRecurso>()];
    }

    // O valor é conferido antes da unidade: mudar a unidade não resolve um prazo negativo.
    private static RecusaDosParametrosDoRecurso? ValidarPrazo(ParametrosRegraPrazoRecurso parametros) => parametros switch
    {
        { PrazoValor: <= 0 } => new(MotivoDaRecusaDosParametros.PrazoNaoPositivo,
            "O prazo de interposição deve ser estritamente positivo."),
        { PrazoUnidade: UnidadePrazo.Dias } => new(MotivoDaRecusaDosParametros.PrazoEmDiasCorridos,
            "O prazo de interposição deve ser informado em dias úteis ou horas; dias corridos não são aceitos."),
        { PrazoUnidade: not (UnidadePrazo.DiasUteis or UnidadePrazo.Horas) } => new(MotivoDaRecusaDosParametros.PrazoSemUnidadeDeclaravel,
            "O prazo de interposição exige unidade declarável — dias úteis ou horas."),
        { PrazoUnidade: UnidadePrazo.DiasUteis } when decimal.Truncate(parametros.PrazoValor) != parametros.PrazoValor => new(MotivoDaRecusaDosParametros.PrazoEmFracaoDeDiaUtil,
            "O prazo de interposição em dias úteis exige valor inteiro. Para prazo menor que um dia, declare em horas."),
        _ => null,
    };

    // A existência da instância precede a validade do par: remover o par resolve também seus defeitos.
    private static RecusaDosParametrosDoRecurso? ValidarSuspensividadeSuperior(ParametrosRegraPrazoRecurso parametros) =>
        !parametros.AdmiteInstanciaSuperior
            && (parametros.SuspensividadeSegundaInstanciaValor is not null || parametros.SuspensividadeSegundaInstanciaUnidade is not null)
            ? new(MotivoDaRecusaDosParametros.SuspensividadeSuperiorSemInstanciaSuperior,
                "Não há recurso em instância superior, então não é possível informar tempo de bloqueio para essa instância. Remova o bloqueio ou permita recurso em instância superior.")
            : ValidarParDeSuspensividade(parametros.SuspensividadeSegundaInstanciaValor, parametros.SuspensividadeSegundaInstanciaUnidade, "2ª instância");

    // UNI-REQ-0080: ausência dos dois valores desativa a suspensão; uma metade válida exige completar o par.
    private static RecusaDosParametrosDoRecurso? ValidarParDeSuspensividade(decimal? valor, UnidadePrazo? unidade, string instancia)
    {
        if (valor is null && unidade is null)
        {
            return null;
        }

        return ValidarPartesDaSuspensividade(valor, unidade, instancia)
            ?? (valor is null || unidade is null
                ? new(MotivoDaRecusaDosParametros.SuspensividadeIncompleta,
                    $"A suspensividade da {instancia} exige valor e unidade juntos, ou nenhum dos dois.")
                : null);
    }

    // Cada metade presente é conferida antes da incompletude: completar o par não resolve um valor inválido.
    private static RecusaDosParametrosDoRecurso? ValidarPartesDaSuspensividade(decimal? valor, UnidadePrazo? unidade, string instancia) =>
        (valor, unidade) switch
        {
            ( <= 0, _) => new(MotivoDaRecusaDosParametros.SuspensividadeNaoPositiva,
                $"O valor da suspensividade da {instancia} deve ser estritamente positivo."),
            (_, not null and not (UnidadePrazo.Horas or UnidadePrazo.Dias or UnidadePrazo.DiasUteis)) => new(MotivoDaRecusaDosParametros.SuspensividadeUnidadeNaoDeclaravel,
                $"A unidade da suspensividade da {instancia} não é declarável."),
            _ => null,
        };
}

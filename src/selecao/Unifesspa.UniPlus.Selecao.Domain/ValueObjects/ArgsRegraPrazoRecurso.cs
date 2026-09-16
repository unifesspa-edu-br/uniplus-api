namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Args aplicados de <see cref="Entities.RegraRecursoFase"/> — os parâmetros que o
/// admin preenche ao ancorar o prazo de recurso de uma fase na regra
/// <c>RECURSO-PRAZO-ANCORADO-EM-ATO</c> (#854, Story #851 §3.0/§3.6).
/// </summary>
/// <remarks>
/// <para>
/// <b>Os DOIS pares de suspensividade, cada um independente e anulável.</b> A
/// suspensividade não é a mesma nos dois módulos (PO, 14/07): na Seleção as duas
/// instâncias bloqueiam; no Ingresso/Habilitação a 1ª bloqueia e a 2ª (judicial, prazo
/// indeterminado) não. Isso <b>não vira</b> <c>if (modulo == Ingresso)</c> — é
/// configuração: para o Ingresso, <c>SuspensividadeSegundaInstancia*</c> é simplesmente
/// <see langword="null"/>. A ausência <b>é</b> a desativação.
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
/// <param name="SuspensividadeSegundaInstanciaValor">Magnitude da janela de suspensividade da instância superior, ou <see langword="null"/> — não bloqueia (caso normal do Ingresso via judicial).</param>
/// <param name="SuspensividadeSegundaInstanciaUnidade">Unidade da suspensividade da instância superior.</param>
public sealed record ArgsRegraPrazoRecurso(
    decimal PrazoValor,
    UnidadePrazo PrazoUnidade,
    decimal? SuspensividadePrimeiraInstanciaValor,
    UnidadePrazo? SuspensividadePrimeiraInstanciaUnidade,
    decimal? SuspensividadeSegundaInstanciaValor,
    UnidadePrazo? SuspensividadeSegundaInstanciaUnidade);

/// <summary>
/// Por que os args de um recurso foram recusados. O código de erro NÃO é montado aqui: ele é
/// literal em cada entidade, senão a cobertura do registro de erros não consegue enxergá-lo.
/// </summary>
public enum MotivoDeRecusaDeArgs
{
    Nenhum = 0,
    PrazoNaoPositivo,
    PrazoEmDiasCorridos,
    PrazoSemUnidadeDeclaravel,
    PrazoEmFracaoDeDiaUtil,
    SuspensividadeNaoPositiva,
    SuspensividadeUnidadeNaoDeclaravel,
    SuspensividadeIncompleta,
}

/// <summary>A recusa: o motivo, que vira código, e a mensagem já redigida.</summary>
public readonly record struct RecusaDeArgsDeRecurso(MotivoDeRecusaDeArgs Motivo, string Mensagem);

/// <summary>
/// As invariantes que <see cref="ArgsRegraPrazoRecurso"/> prova sozinho, sem saber quem o
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
public static class ValidacaoDeArgsDeRecurso
{
    /// <summary>Valida o prazo de interposição e os dois pares de suspensividade.</summary>
    public static RecusaDeArgsDeRecurso? Validar(ArgsRegraPrazoRecurso args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // UNI-REQ-0081: o valor congelado é sempre estritamente positivo. Zero fecharia a
        // janela no mesmo instante em que a abre, e negativo a fecharia antes do ato que a
        // ancora — nos dois casos o prazo nasce inutilizável.
        //
        // Vem antes de QUALQUER checagem de unidade de propósito. Um valor negativo em dia
        // corrido viola duas regras ao mesmo tempo, e só a magnitude tem remediação que
        // resolve: mandar quem declarou -5 dias corridos reescrever em dias úteis o deixa com
        // um prazo que continua fechando antes de abrir. A recusa que orienta é a primeira.
        if (args.PrazoValor <= 0)
        {
            return new(
                MotivoDeRecusaDeArgs.PrazoNaoPositivo,
                $"O prazo de interposição deve ser estritamente positivo — recebido '{args.PrazoValor}'.");
        }

        // UNI-REQ-0113: o prazo de interposição corre exclusivamente em dia útil. É o prazo
        // que fecha a porta do candidato, e tempo que passa quando ele não tem como agir não
        // pode consumir a janela — dia corrido a encolheria sempre que calhasse de cair em
        // feriado.
        if (args.PrazoUnidade == UnidadePrazo.Dias)
        {
            return new(
                MotivoDeRecusaDeArgs.PrazoEmDiasCorridos,
                "O prazo de interposição deve ser informado em dias úteis ou horas; dias corridos não são aceitos.");
        }

        // O que sobra do enum não é prazo declarável. Nenhuma é o valor zero e, portanto, o
        // default: um ArgsRegraPrazoRecurso construído sem preencher a unidade chega aqui
        // nesse estado. A recusa é por lista do que vale — uma unidade nova no enum nasce
        // recusada até ser declarada aqui de propósito.
        if (args.PrazoUnidade is not (UnidadePrazo.DiasUteis or UnidadePrazo.Horas))
        {
            return new(
                MotivoDeRecusaDeArgs.PrazoSemUnidadeDeclaravel,
                $"O prazo de interposição exige unidade declarável — dias úteis ou horas; recebido '{args.PrazoUnidade}'.");
        }

        // UNI-REQ-0113: fração de dia útil não tem leitura unívoca — meio expediente, doze
        // horas dentro do dia, ou metade de um dia civil que numa transição de fuso nem sempre
        // tem vinte e quatro horas. As três fecham a janela em instantes diferentes.
        if (args.PrazoUnidade == UnidadePrazo.DiasUteis && decimal.Truncate(args.PrazoValor) != args.PrazoValor)
        {
            return new(
                MotivoDeRecusaDeArgs.PrazoEmFracaoDeDiaUtil,
                $"O prazo de interposição em dias úteis exige valor inteiro — recebido '{args.PrazoValor}'. Para prazo menor que um dia, declare em horas.");
        }

        RecusaDeArgsDeRecurso? primeira = ValidarParDeSuspensividade(
            args.SuspensividadePrimeiraInstanciaValor,
            args.SuspensividadePrimeiraInstanciaUnidade,
            "1ª instância");
        if (primeira is not null)
        {
            return primeira;
        }

        // A suspensividade admite as três unidades — dias corridos, que a interposição acabou
        // de recusar, e também dias úteis: é outro relógio, com outra regra. O que a contagem
        // em dia útil exige — a convenção de contagem declarada (UNI-REQ-0116) — é invariante
        // do PROCESSO, não destes args, porque a declaração é uma por certame.
        return ValidarParDeSuspensividade(
            args.SuspensividadeSegundaInstanciaValor,
            args.SuspensividadeSegundaInstanciaUnidade,
            "2ª instância");
    }

    /// <summary>
    /// Valida um dos dois pares de suspensividade. UNI-REQ-0080 congela a suspensividade como
    /// par valor-unidade, nunca um valor numérico sozinho: um lado sem o outro não descreve
    /// janela alguma. A ausência dos dois é legítima e significa que aquela instância não
    /// bloqueia — é assim que o Ingresso desativa a 2ª instância, sem <c>if</c> por módulo.
    /// </summary>
    private static RecusaDeArgsDeRecurso? ValidarParDeSuspensividade(
        decimal? valor,
        UnidadePrazo? unidade,
        string instancia)
    {
        // A ausência dos dois é a desativação prevista da instância, e sai antes de tudo.
        if (valor is null && unidade is null)
        {
            return null;
        }

        // Cada metade presente é conferida ANTES de a incompletude ser reportada. Um par como
        // (valor: -3, unidade: ausente) erra duas coisas, e mandar completá-lo devolveria a
        // pessoa com uma janela negativa e uma segunda recusa — a orientação que resolve é a
        // que trata o valor que já está errado.
        if (valor is { } magnitude && magnitude <= 0)
        {
            return new(
                MotivoDeRecusaDeArgs.SuspensividadeNaoPositiva,
                $"O valor da suspensividade da {instancia} deve ser estritamente positivo — recebido '{magnitude}'.");
        }

        // Unidade presente e igual a Nenhuma é ausência disfarçada: passa por qualquer checagem
        // de presença e continua não dizendo em que unidade a janela corre.
        if (unidade is { } declarada
            && declarada is not (UnidadePrazo.Horas or UnidadePrazo.Dias or UnidadePrazo.DiasUteis))
        {
            return new(
                MotivoDeRecusaDeArgs.SuspensividadeUnidadeNaoDeclaravel,
                $"A unidade da suspensividade da {instancia} não é declarável — recebido '{declarada}'.");
        }

        // Sobrou o par com uma metade só, e ela é válida: aqui completar o par é, de fato, a
        // correção que resolve.
        if (valor is null || unidade is null)
        {
            return new(
                MotivoDeRecusaDeArgs.SuspensividadeIncompleta,
                $"A suspensividade da {instancia} exige valor e unidade juntos, ou nenhum dos dois.");
        }

        return null;
    }
}

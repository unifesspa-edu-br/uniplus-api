namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A divulgação de um certame esbarrou no identificador legível que outro certame público já traz.
/// </summary>
/// <remarks>
/// <para>
/// Não deveria acontecer: o cadastro garante a unicidade do identificador e a imutabilidade depois
/// de publicado, e a divulgação copia o valor congelado. Se acontecer, é dado inconsistente — e
/// insistir não resolve, porque o outro certame continua com o mesmo endereço.
/// </para>
/// <para>
/// O tipo próprio é o que permite à política de reentrega mandar a mensagem direto para a fila
/// morta, nomeando o processo e o identificador, em vez de gastar as tentativas da falha
/// transiente e terminar lá com um erro de banco genérico.
/// </para>
/// </remarks>
public sealed class IdentificadorLegivelJaDivulgadoException(string message, Exception innerException)
    : Exception(message, innerException)
{
    /// <summary>
    /// Nome do índice único da coluna na divulgação — o que identifica este conflito entre as
    /// violações possíveis na gravação.
    /// </summary>
    internal const string IndiceDaDivulgacao = "ux_certames_divulgados_identificador_legivel";

    /// <summary>
    /// Se <paramref name="falha"/> é a violação do índice do identificador na divulgação.
    /// </summary>
    /// <remarks>
    /// Violar o índice não basta para concluir que outro certame tem o endereço: duas entregas de
    /// versões do MESMO processo correndo juntas também podem esbarrar nele, se o banco conferir
    /// este índice antes da chave primária. Quem chama decide pelo dono da linha que já existe
    /// (<see cref="Classificar"/>).
    /// </remarks>
    internal static bool EhViolacaoDoIndice(Exception falha) => string.Equals(
        UniqueConstraintViolation.GetViolatedConstraint(falha), IndiceDaDivulgacao, StringComparison.Ordinal);

    /// <summary>
    /// O conflito terminal, quando a linha que já traz o identificador é de OUTRO processo;
    /// <see langword="null"/> quando é do próprio processo ou já não existe — corrida entre entregas,
    /// que a reentrega resolve.
    /// </summary>
    internal static IdentificadorLegivelJaDivulgadoException? Classificar(
        Exception falha,
        Guid processoSeletivoId,
        Guid? processoDonoDoIdentificador,
        string identificadorLegivel) =>
        processoDonoDoIdentificador is { } dono && dono != processoSeletivoId
            ? new IdentificadorLegivelJaDivulgadoException(
                $"O identificador legível '{identificadorLegivel}' do processo {processoSeletivoId} já é o endereço do certame divulgado do processo {dono}.",
                falha)
            : null;
}

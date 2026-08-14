namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A zona horária em que o dia civil do certame começa e termina (UNI-REQ-0111).
/// </summary>
/// <remarks>
/// <para>Não é declarada por quem configura o processo: ela não varia entre certames — todas as
/// unidades da instituição estão na mesma zona —, e perguntar um valor sempre igual só transferiria
/// ao operador a chance de errá-lo. O sistema aplica e congela na versão publicada.</para>
/// <para><strong>Não é "horário de Brasília".</strong> As duas coincidem apenas enquanto não houver
/// horário de verão: o Pará sempre foi isento, e nos períodos em que só a zona de Brasília o
/// observava, o mesmo instante caía em dias civis diferentes nas duas — o que muda a contagem do
/// prazo. Quem escrever <c>America/Sao_Paulo</c> por hábito vê tudo funcionar hoje e erra o dia
/// civil quando o decreto voltar.</para>
/// <para>É constante, e não configuração de implantação, porque é invariante institucional: deixar
/// cada instalação escolher outra zona acrescentaria um modo de falha (erro de operador) sem
/// atender a nenhuma necessidade real. A resolução ainda pode falhar — o runtime pode não trazer a
/// base de fusos —, e é por isso que passa por <c>IResolvedorFusoInstitucional</c> em vez de virar
/// um <c>static readonly</c> que estouraria no carregamento do tipo.</para>
/// </remarks>
public static class FusoInstitucional
{
    /// <summary>Identificador IANA da zona do estado em que a instituição opera.</summary>
    public const string ZoneId = "America/Belem";
}

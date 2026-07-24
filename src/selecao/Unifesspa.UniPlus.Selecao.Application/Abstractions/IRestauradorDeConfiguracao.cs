namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Prova que a configuração de uma <see cref="VersaoConfiguracao"/> congelada pode ser reposta com
/// fidelidade e devolve o grafo <b>já provado</b> para o chamador aplicar (ADR-0110 D1/D2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Prova primeiro, aplica depois — e a prova é aqui.</b> Decodifica o envelope, repõe numa
/// <b>sombra destacada</b> (fora do change tracker), recanonicaliza com o encoder <b>daquela</b>
/// versão e compara byte a byte com os bytes congelados. Só devolve <see cref="Result{T}.Success"/>
/// — com o grafo congelado a aplicar — quando os bytes batem; se um único campo se perdeu na
/// reconstrução, os bytes divergem e a operação <b>falha</b>, em vez de deixar o chamador repor uma
/// configuração empobrecida que ninguém mais teria como detectar. A raiz sabe repor um grafo e
/// recusar um incoerente, mas <b>não</b> sabe recanonicalizar (ADR-0042: o Domain não chama o
/// codec); por isso a prova vive aqui.
/// </para>
/// <para>
/// <b>Não toca a raiz viva.</b> A reposição na raiz é do chamador, DEPOIS da prova — o descarte
/// precisa intercalar um <c>SaveChanges</c> intermediário entre limpar as coleções mutáveis
/// (fatos/regras) e reinserir as congeladas, para não colidir no índice único de ordem. Devolver o
/// grafo provado (em vez de aplicá-lo aqui) é o que permite esse flush sem tocar a raiz antes de a
/// prova passar. O grafo devolvido <b>já foi provado</b>: o chamador não repõe nada não verificado.
/// </para>
/// </remarks>
public interface IRestauradorDeConfiguracao
{
    /// <param name="processo">O agregado cuja identidade a sombra empresta — carregado com o grafo completo, e o mesmo a que a versão pertence.</param>
    /// <param name="versao">A versão congelada cuja configuração volta a valer.</param>
    /// <returns>O grafo congelado <b>provado</b>, pronto para o chamador aplicar na raiz viva; ou a falha da prova.</returns>
    Result<GrafoConfiguracao> Restaurar(ProcessoSeletivo processo, VersaoConfiguracao versao);
}

namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// A ordem dos pedidos: do mais recente para o mais antigo, pelo instante de criação, com o <c>Id</c> como desempate final,
/// que a paginação acrescenta. A fila e a situação entram no resumo do ponto de continuação; na fila dos pedidos da própria
/// pessoa, o e-mail verificado também, porque ele escolhe os pedidos sem beneficiário identificado.
/// </summary>
internal static class OrdenacaoDosPedidos
{
    private const string CampoCriadoEm = "criado_em";

    public static KeysetSort<PedidoDeConcessao> Montar(FiltroDePedidos filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        return new(
            [KeysetSortColumn<PedidoDeConcessao>.For(CampoCriadoEm, p => p.CriadoEm, p => InstanteDoCursor.Escrever(p.CriadoEm), SortDirection.Descending)],
            PontoDeContinuacao,
            CondicoesDoFiltro.De(
                ParteDoFiltro.De<FilaDePedidos>(filtro.Fila),
                ParteDoFiltro.De(filtro.Situacao),
                ParteDoFiltro.De(filtro.Fila == FilaDePedidos.Meus ? filtro.EmailVerificado : null)));
    }

    private static object PontoDeContinuacao(IReadOnlyList<string> partes, Guid id) =>
        new { CriadoEm = InstanteDoCursor.Ler(partes[0]), Id = id };
}

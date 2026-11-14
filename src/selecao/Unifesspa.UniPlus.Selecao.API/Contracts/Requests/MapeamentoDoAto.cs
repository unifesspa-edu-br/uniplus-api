namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Converte o bloco <c>ato</c> e os anexos que a publicação e a retificação recebem
/// no corpo da requisição para os dados que o comando carrega.
/// </summary>
internal static class MapeamentoDoAto
{
    internal static DadosDoAto ParaDados(DadosDoAtoRequest ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        return new DadosDoAto(ato.Orgao, ato.Serie, ato.Ano, ato.DataDoAto, ato.Assinante, ato.TipoAtoCodigo);
    }

    // O item nulo segue para o validator, que o recusa com 4xx — desreferenciar aqui responderia
    // 500 antes de o comando existir.
    internal static List<AnexoSolicitadoDoEdital>? ParaAnexos(IReadOnlyList<AnexoDoEditalRequest>? anexos) =>
        anexos?.Select(static a => a is null ? null! : new AnexoSolicitadoDoEdital(a.DocumentoId, a.Nome)).ToList();
}

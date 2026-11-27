namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As distribuições de vagas do documento, uma por oferta de curso, lidas na ordem enviada. A que a leitura recusa fica fora, com as
/// causas na posição dela, e as demais seguem adotadas: a recusa de uma oferta não impede a leitura das outras.
/// </summary>
internal static class LeituraDasDistribuicoesDeVagas
{
    /// <summary>O nome da lista na origem do documento, que é o caminho da parte recusada.</summary>
    internal const string Lista = "distribuicaoVagas";

    internal static DistribuicoesLidas Ler(IReadOnlyList<DistribuicaoNoDocumento> distribuicoes, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(distribuicoes);
        ArgumentNullException.ThrowIfNull(cadastros);

        DistribuicaoLida[] lidas = [.. distribuicoes.Select(distribuicao => LeituraDaDistribuicaoDeVagas.Ler(distribuicao, cadastros))];

        return new(
            [.. lidas.Where(static lida => lida.Distribuicao is not null)],
            [.. lidas.SelectMany(static lida => lida.Recusadas)]);
    }
}

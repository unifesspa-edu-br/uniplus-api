namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Services;
using Domain.ValueObjects;

/// <summary>
/// Os documentos que um ato publicou no acervo público, vistos pelo endereço de cada um. A chave é
/// a mesma que a divulgação usou ao copiar — as duas pontas a montam pelo domínio.
/// </summary>
/// <param name="ProcessoSeletivoId">O processo da versão lida.</param>
/// <param name="AtoId">O ato que criou a versão lida, e com ela publicou os documentos.</param>
/// <param name="EnderecoDaChave">O endereço público de uma chave do acervo.</param>
internal sealed record DocumentosDoAtoNoAcervo(Guid ProcessoSeletivoId, Guid AtoId, Func<string, Uri> EnderecoDaChave)
{
    /// <summary>O endereço do modelo de documento que o ato publicou.</summary>
    public Uri DoModelo(ModeloDaExigencia modelo) =>
        EnderecoDaChave(ChaveNoAcervoPublico.DoModelo(ProcessoSeletivoId, AtoId, modelo));
}

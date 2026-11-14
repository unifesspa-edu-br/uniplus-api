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
/// <param name="Copiados">
/// Se a cópia dos documentos do ato ao acervo já registrou o endereço. Antes disso o arquivo pode não estar no acervo, e
/// nenhum endereço é exposto.
/// </param>
public sealed record DocumentosDoAtoNoAcervo(Guid ProcessoSeletivoId, Guid AtoId, Func<string, Uri> EnderecoDaChave, bool Copiados)
{
    /// <summary>O endereço do modelo de documento que o ato publicou; nulo enquanto a cópia ao acervo não terminou.</summary>
    internal Uri? DoModelo(ModeloDaExigencia modelo) =>
        Copiados ? EnderecoDaChave(ChaveNoAcervoPublico.DoModelo(ProcessoSeletivoId, AtoId, modelo)) : null;
}

namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que uma versão muda em relação à vigente, lido pelo catálogo atual: as permissões que acrescenta, as que remove e
/// se o acréscimo exige a proteção de dados. A classe é sempre a do acréscimo, nunca a do perfil.
/// </summary>
/// <param name="Acrescimo">Os códigos da versão que a vigente não reúne; sem vigente, todos.</param>
/// <param name="Remocao">Os códigos da vigente que a versão não reúne.</param>
/// <param name="ExigeProtecaoDeDados">Algum código do acréscimo é pessoal, identificador ou sensível.</param>
public sealed record DiferencaDaVersao(IReadOnlyList<string> Acrescimo, IReadOnlyList<string> Remocao, bool ExigeProtecaoDeDados)
{
    public bool TemAcrescimo => Acrescimo.Count > 0;

    public bool TemRemocao => Remocao.Count > 0;

    /// <summary>Compara os códigos da versão com os da vigente, ambos ordenados; código fora do catálogo conta como sensível.</summary>
    public static DiferencaDaVersao Calcular(
        IReadOnlyList<string> versao, IReadOnlyList<string>? vigente, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(catalogo);

        IReadOnlyList<string> vigenteOuVazia = vigente ?? [];
        string[] acrescimo = [.. versao.Except(vigenteOuVazia, StringComparer.Ordinal)];

        return new DiferencaDaVersao(
            acrescimo,
            [.. vigenteOuVazia.Except(versao, StringComparer.Ordinal)],
            acrescimo.Any(c => catalogo.Sensibilidade(c) >= ClassificacaoDeProtecao.Pessoal));
    }
}

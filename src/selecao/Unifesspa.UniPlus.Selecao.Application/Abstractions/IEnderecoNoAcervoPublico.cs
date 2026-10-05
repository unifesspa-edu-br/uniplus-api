namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// O endereço público de um objeto do acervo (ADR-0132): o nome que a borda publica seguido da
/// chave. É o link divulgado no contrato público — nunca pré-assinado, porque o objeto é imutável e
/// de leitura anônima, e cacheá-lo é o que se quer.
/// </summary>
public interface IEnderecoNoAcervoPublico
{
    /// <summary>O endereço absoluto do objeto de chave <paramref name="chave"/>.</summary>
    Uri De(string chave);
}

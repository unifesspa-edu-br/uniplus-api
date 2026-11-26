namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A parte do documento recebido que a leitura recusou e que, por isso, não entrou na declaração: o caminho dela na declaração
/// (a lista de onde saiu ou o campo recusado, <c>fases[1].unidadeResponsavel</c>, com a posição canônica de cada item do caminho,
/// como <see cref="OrigemNoDocumento"/> nomeia as listas), a chave dela quando tem, a posição dela no documento e a causa. A causa
/// já aponta o documento pelo ponteiro JSON, porque quem a produz é a leitura, que fala o documento, e a conversão do campo a
/// deixa como está.
/// </summary>
public sealed record ParteRecusada
{
    internal ParteRecusada(string caminho, string? chave, int posicaoDeOrigem, FieldError causa)
    {
        ArgumentException.ThrowIfNullOrEmpty(caminho);
        ArgumentOutOfRangeException.ThrowIfNegative(posicaoDeOrigem);
        ArgumentNullException.ThrowIfNull(causa);

        Caminho = caminho;
        Chave = chave;
        PosicaoDeOrigem = posicaoDeOrigem;
        Causa = causa;
    }

    /// <summary>A parte de um bloco sem lista que a leitura recusou: o campo do bloco como caminho e a causa já apontada no documento.</summary>
    internal static ParteRecusada DoBloco(string campoDoBloco, FieldError causa)
    {
        ArgumentNullException.ThrowIfNull(causa);

        return new(campoDoBloco, null, 0, causa with { Field = PonteiroNoDocumento.Converter(causa.Field, OrigemNoDocumento.Identidade) });
    }

    public string Caminho { get; }

    public string? Chave { get; }

    public int PosicaoDeOrigem { get; }

    public FieldError Causa { get; }
}

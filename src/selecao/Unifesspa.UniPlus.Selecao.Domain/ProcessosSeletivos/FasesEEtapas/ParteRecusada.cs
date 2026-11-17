namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A parte do documento recebido que a leitura recusou e que, por isso, não entrou na declaração: a lista de onde saiu
/// (pelo mesmo nome que <see cref="OrigemNoDocumento"/> dá às listas), a chave dela quando tem, a posição dela no documento
/// e a causa. A causa já aponta o documento pelo ponteiro JSON, porque quem a produz é a leitura, que fala o documento, e a
/// conversão do campo a deixa como está.
/// </summary>
public sealed record ParteRecusada
{
    internal ParteRecusada(string colecao, string? chave, int posicaoDeOrigem, FieldError causa)
    {
        ArgumentException.ThrowIfNullOrEmpty(colecao);
        ArgumentOutOfRangeException.ThrowIfNegative(posicaoDeOrigem);
        ArgumentNullException.ThrowIfNull(causa);

        Colecao = colecao;
        Chave = chave;
        PosicaoDeOrigem = posicaoDeOrigem;
        Causa = causa;
    }

    public string Colecao { get; }

    public string? Chave { get; }

    public int PosicaoDeOrigem { get; }

    public FieldError Causa { get; }
}

namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// O leiaute de um relatório como o catálogo o entrega e a versão o congela: o código, a versão, o hash do
/// conteúdo e os dados que o arquivo traz, copiados por valor. Os leiautes são iguais quando têm o mesmo
/// código, versão e hash e os mesmos dados, na mesma ordem.
/// </summary>
public sealed record LeiauteDoRelatorio(string Codigo, string Versao, string Hash, IReadOnlyList<DadoDoArquivo> Dados)
{
    public bool Equals(LeiauteDoRelatorio? outro) =>
        outro is not null
        && string.Equals(Codigo, outro.Codigo, StringComparison.Ordinal)
        && string.Equals(Versao, outro.Versao, StringComparison.Ordinal)
        && string.Equals(Hash, outro.Hash, StringComparison.Ordinal)
        && Dados.SequenceEqual(outro.Dados);

    public override int GetHashCode() => HashCode.Combine(Codigo, Versao, Hash, Dados.Count);

    /// <summary>Se o leiaute tem o fato entre os dados do arquivo, em coluna obrigatória ou opcional.</summary>
    public bool Declara(string fato) => Dados.Any(dado => string.Equals(dado.FatoCodigo, fato, StringComparison.Ordinal));

    /// <summary>Se o relatório traz o fato em toda linha do arquivo.</summary>
    public bool Traz(string fato) =>
        Dados.Any(dado => dado.Presenca == PresencaDoDadoNoArquivo.EmTodaLinha && string.Equals(dado.FatoCodigo, fato, StringComparison.Ordinal));
}

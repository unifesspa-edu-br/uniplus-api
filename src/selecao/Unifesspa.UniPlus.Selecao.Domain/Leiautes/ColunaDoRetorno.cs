namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>Uma posição do arquivo de retorno, com o campo que ela alimenta e o leitor do valor.</summary>
/// <param name="Posicao">Posição da coluna na linha, a partir de zero.</param>
/// <param name="Campo">O campo do Uni+ alimentado.</param>
/// <param name="Area">O código da área do ENEM, para nota e situação; nulo nos demais campos.</param>
/// <param name="Leitor">O leitor do formato do valor.</param>
public sealed record ColunaDoRetorno(int Posicao, CampoDoRetorno Campo, string? Area, ILeitorDeColuna Leitor)
{
    public static ColunaDoRetorno Cpf(int posicao) => new(posicao, CampoDoRetorno.Cpf, null, new LeitorDeCpf());

    public static ColunaDoRetorno Nome(int posicao) => new(posicao, CampoDoRetorno.Nome, null, new LeitorDeNome());

    public static ColunaDoRetorno Sexo(int posicao) => new(posicao, CampoDoRetorno.Sexo, null, new LeitorDeSexo());

    public static ColunaDoRetorno NumeroDeInscricaoNoEnem(int posicao) =>
        new(posicao, CampoDoRetorno.NumeroDeInscricaoNoEnem, null, new LeitorDeNumeroDeInscricaoNoEnem());

    public static ColunaDoRetorno Nota(int posicao, string area) =>
        new(posicao, CampoDoRetorno.Nota, area, new LeitorDeNotaDoEnem());

    public static ColunaDoRetorno Situacao(int posicao, string area, ILeitorDeColuna leitor) =>
        new(posicao, CampoDoRetorno.Situacao, area, leitor);

    public static ColunaDoRetorno Ignorada(int posicao) =>
        new(posicao, CampoDoRetorno.Ignorado, null, new LeitorDeColunaIgnorada());

    public static ColunaDoRetorno Vazia(int posicao) =>
        new(posicao, CampoDoRetorno.Ignorado, null, new LeitorDeColunaVazia());
}

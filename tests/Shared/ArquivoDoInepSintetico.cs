namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Globalization;
using System.Text;

/// <summary>
/// Monta arquivos de retorno do INEP sintéticos a partir das personas fictícias do portal. As colunas
/// que o Uni+ ignora (nome, data de nascimento, dados cadastrais) vêm preenchidas de propósito, para
/// provar que não são lidas. Nenhum arquivo real é usado.
/// </summary>
internal static class ArquivoDoInepSintetico
{
    /// <summary>Notas na ordem das colunas do arquivo: CN, CH, LC, MT, Redação — todas distintas.</summary>
    public static readonly string[] NotasPadrao = ["611.1", "722.2", "833.3", "544.4", "960"];

    public static readonly string[] SituacoesPadrao = ["7", "7", "7", "7", "7"];

    public static string NumeroDeInscricao(int ordem) =>
        (240000000000L + ordem).ToString(CultureInfo.InvariantCulture);

    public static string Linha(
        PersonasDeTeste.PessoaFicticia pessoa,
        int ordem,
        string[]? notas = null,
        string[]? situacoes = null,
        string? cpf = null,
        string? numeroDeInscricao = null,
        string? nome = null) =>
        string.Join(
            ';',
            [
                numeroDeInscricao ?? NumeroDeInscricao(ordem),
                cpf ?? pessoa.Cpf,
                nome ?? pessoa.Nome,
                .. notas ?? NotasPadrao,
                .. situacoes ?? SituacoesPadrao,
                pessoa.DataDeNascimento,
                pessoa.Sexo,
                pessoa.Cidade,
                pessoa.Uf,
                pessoa.Sexo,
                pessoa.Cidade,
                string.Empty,
            ]);

    /// <summary>O leiaute anterior, com três campos a mais: não pode ser lido com o de 20.</summary>
    public static string LinhaDe23Campos(PersonasDeTeste.PessoaFicticia pessoa, int ordem) =>
        Linha(pessoa, ordem) + ";x;y;z";

    /// <summary>A linha de quem faltou às provas: nota zero e situação de ausente em todas as áreas.</summary>
    public static string LinhaDeAusente(PersonasDeTeste.PessoaFicticia pessoa, int ordem) =>
        Linha(pessoa, ordem, notas: ["0", "0", "0", "0", "0"], situacoes: ["6", "6", "6", "6", "6"]);

    public static byte[] Bytes(
        IEnumerable<string> linhas,
        string fimDeLinha = "\n",
        bool comBom = false,
        bool quebraFinal = true)
    {
        string texto = string.Join(fimDeLinha, linhas) + (quebraFinal ? fimDeLinha : string.Empty);
        byte[] conteudo = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(texto);

        return comBom ? [0xEF, 0xBB, 0xBF, .. conteudo] : conteudo;
    }
}

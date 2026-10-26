namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Text;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Confere o arquivo de retorno contra a estrutura do leiaute e o lê. Qualquer divergência recusa o
/// arquivo inteiro: ler com o leiaute errado gravaria a nota de uma área como se fosse de outra.
/// </summary>
/// <remarks>
/// As mensagens de erro dizem a linha e a coluna e nunca o conteúdo da célula, que pode ser CPF ou
/// nota.
/// </remarks>
public static class LeitorDeArquivoDoInep
{
    private static readonly byte[] MarcaBom = [0xEF, 0xBB, 0xBF];

    public static Result<ArquivoDeRetornoLido> Ler(ReadOnlySpan<byte> conteudo, LeiauteDeRetornoDoInep leiaute)
    {
        ArgumentNullException.ThrowIfNull(leiaute);

        ReadOnlySpan<byte> bytes = conteudo.StartsWith(MarcaBom) ? conteudo[MarcaBom.Length..] : conteudo;

        string texto;
        try
        {
            texto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Result<ArquivoDeRetornoLido>.Failure(ForaDoLeiaute("O arquivo não está codificado em UTF-8."));
        }

        List<string> linhas = SepararLinhas(texto);
        if (linhas.Count == 0)
        {
            return Result<ArquivoDeRetornoLido>.Failure(ForaDoLeiaute("O arquivo não tem nenhuma linha."));
        }

        List<LinhaDeRetorno> lidas = new(linhas.Count);
        for (int i = 0; i < linhas.Count; i++)
        {
            Result<LinhaDeRetorno> linha = LerLinha(linhas[i], i + 1, leiaute);
            if (linha.IsFailure)
            {
                return Result<ArquivoDeRetornoLido>.Failure(linha.Error!);
            }

            lidas.Add(linha.Value!);
        }

        return Result<ArquivoDeRetornoLido>.Success(new ArquivoDeRetornoLido(lidas));
    }

    /// <summary>
    /// Divide por <c>\n</c>, descartando o <c>\r</c> que o antecede (CRLF). A quebra final única é
    /// tolerada porque não produz linha; um <c>\r</c> que sobra em qualquer linha fica no texto e é
    /// recusado adiante.
    /// </summary>
    private static List<string> SepararLinhas(string texto)
    {
        List<string> linhas = [];
        int inicio = 0;
        for (int i = 0; i < texto.Length; i++)
        {
            if (texto[i] != '\n')
            {
                continue;
            }

            int fim = i > inicio && texto[i - 1] == '\r' ? i - 1 : i;
            linhas.Add(texto[inicio..fim]);
            inicio = i + 1;
        }

        if (inicio < texto.Length)
        {
            linhas.Add(texto[inicio..]);
        }

        return linhas;
    }

    private static Result<LinhaDeRetorno> LerLinha(string linha, int numeroDaLinha, LeiauteDeRetornoDoInep leiaute)
    {
        if (linha.Length == 0)
        {
            return Result<LinhaDeRetorno>.Failure(ForaDoLeiaute($"Linha {numeroDaLinha}: linha em branco."));
        }

        if (linha.Contains('\r', StringComparison.Ordinal))
        {
            return Result<LinhaDeRetorno>.Failure(ForaDoLeiaute($"Linha {numeroDaLinha}: retorno de carro fora do fim de linha."));
        }

        string[] campos = linha.Split(LeiauteDeRetornoDoInep.Separador);
        if (campos.Length != leiaute.QuantidadeDeCampos)
        {
            return Result<LinhaDeRetorno>.Failure(ForaDoLeiaute($"Linha {numeroDaLinha}: o arquivo tem {campos.Length} campos e o leiaute escolhido espera {leiaute.QuantidadeDeCampos}."));
        }

        string cpf = string.Empty;
        string? nome = null;
        string? sexo = null;
        string numeroDeInscricao = string.Empty;
        Dictionary<string, decimal> notas = [];
        Dictionary<string, SituacaoDaProvaNoEnem> situacoes = [];

        foreach (ColunaDoRetorno coluna in leiaute.Colunas)
        {
            LeituraDeCelula leitura = coluna.Leitor.Ler(campos[coluna.Posicao]);
            if (!leitura.Valida)
            {
                return RecusaDaCelula(leitura.Falha!.Value, numeroDaLinha, coluna);
            }

            switch (coluna.Campo)
            {
                case CampoDoRetorno.Cpf:
                    cpf = (string)leitura.Valor!;
                    break;
                case CampoDoRetorno.Nome:
                    nome = (string?)leitura.Valor;
                    break;
                case CampoDoRetorno.Sexo:
                    sexo = (string?)leitura.Valor;
                    break;
                case CampoDoRetorno.NumeroDeInscricaoNoEnem:
                    numeroDeInscricao = (string)leitura.Valor!;
                    break;
                case CampoDoRetorno.Nota:
                    notas[coluna.Area!] = (decimal)leitura.Valor!;
                    break;
                case CampoDoRetorno.Situacao:
                    situacoes[coluna.Area!] = (SituacaoDaProvaNoEnem)leitura.Valor!;
                    break;
                case CampoDoRetorno.Ignorado:
                    break;
            }
        }

        // Prova ausente não tem nota: um valor diferente de zero ao lado dela é arquivo
        // inconsistente, e a apuração, que usa a nota do lançamento, não pode adivinhar qual dos
        // dois campos está certo.
        foreach (ColunaDoRetorno coluna in leiaute.Colunas)
        {
            if (coluna.Campo == CampoDoRetorno.Nota
                && situacoes[coluna.Area!] == SituacaoDaProvaNoEnem.Ausente
                && notas[coluna.Area!] != 0m)
            {
                return Result<LinhaDeRetorno>.Failure(ForaDoLeiaute(
                    $"Linha {numeroDaLinha}, coluna {coluna.Posicao + 1} (nota): a prova está ausente e a nota não é zero."));
            }
        }

        IReadOnlyList<ResultadoDaAreaNoRetorno> areas =
            [.. AreaDoEnem.Todas.Select(a => new ResultadoDaAreaNoRetorno(a, notas[a], situacoes[a]))];

        return Result<LinhaDeRetorno>.Success(new LinhaDeRetorno(numeroDaLinha, cpf, nome, sexo, numeroDeInscricao, areas));
    }

    private static Result<LinhaDeRetorno> RecusaDaCelula(FalhaDeCelula falha, int numeroDaLinha, ColunaDoRetorno coluna)
    {
        int numeroDaColuna = coluna.Posicao + 1;
        if (falha == FalhaDeCelula.CodigoDeSituacaoDesconhecido)
        {
            return Result<LinhaDeRetorno>.Failure(new DomainError(
                NotasDoEnemErrorCodes.CodigoDeSituacaoDesconhecido,
                $"Linha {numeroDaLinha}, coluna {numeroDaColuna}: código de situação da prova que o leiaute não reconhece."));
        }

        return Result<LinhaDeRetorno>.Failure(ForaDoLeiaute($"Linha {numeroDaLinha}, coluna {numeroDaColuna} ({Descrever(coluna.Campo)}): valor fora do formato do leiaute."));
    }

    private static string Descrever(CampoDoRetorno campo) => campo switch
    {
        CampoDoRetorno.Cpf => "CPF",
        CampoDoRetorno.Nome => "nome",
        CampoDoRetorno.Sexo => "sexo (F ou M)",
        CampoDoRetorno.NumeroDeInscricaoNoEnem => "número de inscrição no ENEM",
        CampoDoRetorno.Nota => "nota",
        CampoDoRetorno.Situacao => "situação da prova",
        _ => "campo sem uso",
    };

    private static DomainError ForaDoLeiaute(string mensagem) =>
        new(NotasDoEnemErrorCodes.ArquivoForaDoLeiaute, mensagem);
}

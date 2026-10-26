namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Text;

/// <summary>Gera o arquivo de envio ao INEP no leiaute escolhido.</summary>
public static class EscritorDeArquivoDeEnvio
{
    /// <summary>Um CPF por linha, na ordem recebida, com fim de linha depois de cada uma.</summary>
    public static byte[] Escrever(LeiauteDeEnvioDoInep leiaute, IReadOnlyList<string> cpfs)
    {
        ArgumentNullException.ThrowIfNull(leiaute);
        ArgumentNullException.ThrowIfNull(cpfs);

        StringBuilder texto = new();
        foreach (string cpf in cpfs)
        {
            texto.Append(cpf).Append(LeiauteDeEnvioDoInep.FimDeLinha);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(texto.ToString());
    }
}

namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;

using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O pedido de verificação na forma da fila: o que a confirmação publica e a reconciliação republica.
/// </summary>
public static class PedidoDeVerificacao
{
    public static PedidoDeVerificacaoDeArquivo De(ArquivoDaApresentacao arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);

        return new PedidoDeVerificacaoDeArquivo(
            arquivo.Id,
            arquivo.ObjectKey,
            arquivo.ETag ?? throw new InvalidOperationException($"O arquivo {arquivo.Id} em verificação não tem ETag."),
            arquivo.ChaveDefinitivaPrevista,
            FormatosDoArquivoDoCandidato.ParaContrato(arquivo.Formato),
            arquivo.TamanhoMaximoBytes);
    }
}

namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// O bucket privado onde vivem os arquivos que o administrador envia ao processo — o pendente e a
/// cópia selada do confirmado. É dele que o acervo público copia.
/// </summary>
internal static class BucketDosArquivosEnviados
{
    internal static string De(StorageOptions opcoes) =>
        opcoes.BucketName
        ?? throw new InvalidOperationException("Storage:BucketName não configurado — obrigatório para o envio de arquivos do processo seletivo.");
}

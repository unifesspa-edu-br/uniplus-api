namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.ResultadosDaVerificacao;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Trata o resultado da verificação de um tipo de documento da Autorização. A fila traz só o id do arquivo, então cada
/// receptor procura o seu documento por esse id e diz se o reconheceu.
/// </summary>
public interface IReceptorDoResultadoDaVerificacao
{
    Task<DesfechoDoRecebimento> ReceberAsync(ResultadoDaVerificacaoDeArquivo resultado, CancellationToken cancellationToken);
}

/// <summary>O que um receptor fez com o resultado: não o reconheceu, ou o tratou e devolveu as mensagens de saída.</summary>
public sealed record DesfechoDoRecebimento(bool Reconhecido, IReadOnlyList<EnviarEmail> Mensagens)
{
    public static DesfechoDoRecebimento NaoReconhecido { get; } = new(false, []);

    public static DesfechoDoRecebimento Tratado(IReadOnlyList<EnviarEmail>? mensagens = null) => new(true, mensagens ?? []);
}

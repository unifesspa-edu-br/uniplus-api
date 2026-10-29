namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Seguranca;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// <see cref="IResumoDeCodigoDeConfirmacao"/> como HMAC-SHA256 pelo índice cego da plataforma: o texto resumido
/// junta a instalação e o código, para que o código de uma instalação não confirme outra. Em produção a chave
/// <see cref="NomeDaChave"/> precisa existir no Transit engine, e a política da API liberar o HMAC dela.
/// </summary>
public sealed class ResumoDeCodigoDeConfirmacao : IResumoDeCodigoDeConfirmacao
{
    public const string NomeDaChave = "instalacao-codigo";

    private readonly IUniPlusBlindIndexService _blindIndex;

    public ResumoDeCodigoDeConfirmacao(IUniPlusBlindIndexService blindIndex)
    {
        ArgumentNullException.ThrowIfNull(blindIndex);
        _blindIndex = blindIndex;
    }

    public Task<byte[]> ResumirAsync(Guid instalacaoId, string codigo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);

        string texto = string.Create(CultureInfo.InvariantCulture, $"{instalacaoId:D}:{codigo}");

        return _blindIndex.ComputarAsync(NomeDaChave, Encoding.UTF8.GetBytes(texto), cancellationToken);
    }
}

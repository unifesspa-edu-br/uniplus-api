namespace Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Implementação de <see cref="IEnderecoNoAcervoPublico"/> sobre
/// <see cref="AcervoPublicoOptions.EnderecoBase"/>, validado na partida fora de Development.
/// </summary>
public sealed class EnderecoNoAcervoPublico : IEnderecoNoAcervoPublico
{
    private readonly IOptions<AcervoPublicoOptions> _opcoes;

    public EnderecoNoAcervoPublico(IOptions<AcervoPublicoOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        _opcoes = opcoes;
    }

    public Uri De(string chave)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chave);

        // Lido no uso: em Development a partida admite o endereço ausente, e só o certame com
        // documento no acervo precisa dele.
        string enderecoBase = _opcoes.Value.EnderecoBase is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException("AcervoPublico:EnderecoBase não configurado — obrigatório para divulgar o link de documento do acervo.");

        return new Uri($"{enderecoBase.TrimEnd('/')}/{chave.TrimStart('/')}", UriKind.Absolute);
    }
}

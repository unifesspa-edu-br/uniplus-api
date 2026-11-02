namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// As regras do endereço da inscrição que dependem do Geo: o CEP existe, é da cidade declarada e, quando
/// é o geral da cidade, o candidato descreveu o logradouro e o bairro. O nível da resolução e a cidade
/// vêm do Geo, nunca do que o navegador mandou (<c>nivelResolucao</c> e <c>origem</c> são ignorados).
/// As regras que não precisam do Geo — CEP presente com 8 dígitos e o limite do complemento — são do
/// agregado (<c>Inscricao.RecusasDoEndereco</c>).
/// </summary>
public static class ConferenciaDoEnderecoDaInscricao
{
    /// <summary>
    /// Confere cada endereço respondido. O Geo fora do ar nunca vira "CEP não encontrado": ao enviar, o endereço
    /// que o Geo não conferiu fica de fora e as conferências seguintes não esperam o Geo, e quem chama decide o que fazer com
    /// o <see cref="ConferenciaDosEnderecos.GeoIndisponivel"/>; ao salvar, a conferência é adiada para o envio e o
    /// rascunho continua salvável.
    /// </summary>
    /// <param name="aoEnviar">Envio: exige logradouro e bairro no CEP geral e não tolera Geo fora do ar.</param>
    public static async Task<ConferenciaDosEnderecos> ConferirAsync(
        IReadOnlyDictionary<string, JsonElement> respostas,
        RespostasAdmitidas admitidas,
        IConferenciaEnderecoGeo conferencia,
        bool aoEnviar,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(admitidas);
        ArgumentNullException.ThrowIfNull(conferencia);

        List<FieldError> recusas = [];
        Dictionary<string, CepResolvido> resolvidos = new(StringComparer.Ordinal);
        foreach ((string codigo, DefinicaoDaResposta definicao) in admitidas.Definicoes)
        {
            if (definicao.Tipo != TipoDoValorDeResposta.Endereco
                || !respostas.TryGetValue(codigo, out JsonElement valor)
                || !EnderecoDeclarado.TentarLer(valor, out EnderecoDeclarado endereco)
                || endereco.CepSomenteDigitos is not { } cep)
            {
                continue;
            }

            Result<CepResolvido> resolvido = await conferencia.ResolverCepAsync(cep, cancellationToken).ConfigureAwait(false);
            if (resolvido.IsFailure)
            {
                if (resolvido.Error!.Code == ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel)
                {
                    if (aoEnviar)
                    {
                        return new ConferenciaDosEnderecos(recusas, resolvidos, resolvido.Error);
                    }

                    continue;
                }

                recusas.Add(new FieldError(codigo, new DomainError(
                    InscricaoErrorCodes.EnderecoCepNaoEncontrado,
                    "CEP não encontrado. Confira o seu CEP na busca dos Correios; se o seu bairro não tem CEP próprio, use o CEP geral da sua cidade e descreva o endereço real no complemento.")));
                continue;
            }

            int recusasAntes = recusas.Count;
            recusas.AddRange(RecusasDoCep(codigo, endereco, resolvido.Value!, aoEnviar));
            if (recusas.Count == recusasAntes)
            {
                resolvidos[codigo] = resolvido.Value!;
            }
        }

        return new ConferenciaDosEnderecos(recusas, resolvidos, GeoIndisponivel: null);
    }

    private static IEnumerable<FieldError> RecusasDoCep(string codigo, EnderecoDeclarado endereco, CepResolvido resolvido, bool aoEnviar)
    {
        if (endereco.CidadeCodigoIbge is { Length: > 0 } cidade
            && (!string.Equals(cidade, resolvido.CodigoIbge, StringComparison.Ordinal)
                || (endereco.CidadeUf is { Length: > 0 } uf && !string.Equals(uf, resolvido.Uf, StringComparison.OrdinalIgnoreCase))))
        {
            yield return new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.EnderecoCepDeOutraCidade,
                "O CEP informado pertence a outra cidade que a do seu endereço. Confira o CEP e a cidade."));
        }

        if (!aoEnviar)
        {
            yield break;
        }

        // O número que o Geo não resolve de forma exata pode ser erro de digitação: o endereço só vale com a confirmação explícita.
        // Sem a confirmação só ela é cobrada: logradouro e bairro só valem a pena depois que o candidato confirma o CEP.
        if (resolvido.ExigeConfirmacao && endereco.CepConfirmado is not true)
        {
            yield return new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.EnderecoCepNaoConfirmado,
                "O CEP informado não corresponde a uma rua cadastrada nos Correios. Confira o número; se o seu endereço usa o CEP geral da cidade ou do bairro, confirme no formulário."));
            yield break;
        }

        if (!resolvido.ExigeLogradouroEBairro)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(endereco.Logradouro))
        {
            yield return new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.EnderecoLogradouroObrigatorio,
                "Informe o logradouro (rua, avenida ou similar) do seu endereço: o CEP informado não identifica uma rua."));
        }

        if (string.IsNullOrWhiteSpace(endereco.Bairro))
        {
            yield return new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.EnderecoBairroObrigatorio,
                "Informe o bairro do seu endereço: o CEP informado não identifica o bairro."));
        }
    }
}

/// <summary>
/// O que a conferência dos endereços apurou: as recusas por campo; o que o Geo resolveu pelo CEP de cada endereço
/// conferido sem recusa, pelo código do fato do endereço; e o erro do Geo quando ele ficou fora do ar.
/// </summary>
public sealed record ConferenciaDosEnderecos(
    IReadOnlyList<FieldError> Recusas, IReadOnlyDictionary<string, CepResolvido> Resolvidos, DomainError? GeoIndisponivel)
{
    /// <summary>A cidade que o Geo resolveu de cada endereço conferido sem recusa, como a inscrição enviada a guarda.</summary>
    public IReadOnlyDictionary<string, CidadeConferida> Cidades =>
        Resolvidos.ToDictionary(static r => r.Key, static r => new CidadeConferida(r.Value.CodigoIbge, r.Value.Cidade, r.Value.Uf), StringComparer.Ordinal);
}

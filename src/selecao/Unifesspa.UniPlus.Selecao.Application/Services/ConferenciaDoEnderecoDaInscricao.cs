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
    /// Confere cada endereço respondido. O Geo fora do ar nunca vira "CEP não encontrado": ao enviar, é a
    /// falha fechada <see cref="ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel"/> (como o município); ao
    /// salvar, a conferência é adiada para o envio e o rascunho continua salvável.
    /// </summary>
    /// <param name="aoEnviar">Envio: exige logradouro e bairro no CEP geral e não tolera Geo fora do ar.</param>
    public static async Task<Result<IReadOnlyList<FieldError>>> ConferirAsync(
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
                        return Result<IReadOnlyList<FieldError>>.Failure(resolvido.Error);
                    }

                    continue;
                }

                recusas.Add(new FieldError(codigo, new DomainError(
                    InscricaoErrorCodes.EnderecoCepNaoEncontrado,
                    "CEP não encontrado. Confira o seu CEP na busca dos Correios; se o seu bairro não tem CEP próprio, use o CEP geral da sua cidade e descreva o endereço real no complemento.")));
                continue;
            }

            recusas.AddRange(RecusasDoCep(codigo, endereco, resolvido.Value!, aoEnviar));
        }

        return Result<IReadOnlyList<FieldError>>.Success(recusas);
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
        if (resolvido.ExigeConfirmacao && endereco.CepConfirmado is not true)
        {
            yield return new FieldError(codigo, new DomainError(
                InscricaoErrorCodes.EnderecoCepNaoConfirmado,
                "O CEP informado não corresponde a uma rua cadastrada nos Correios. Confira o número; se o seu endereço usa o CEP geral da cidade ou do bairro, confirme no formulário."));
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

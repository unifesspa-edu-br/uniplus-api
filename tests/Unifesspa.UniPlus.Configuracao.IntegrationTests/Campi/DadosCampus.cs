namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Campi;

internal static class DadosCampus
{
    internal static object CampusDeTeste(string sigla)
    {
        return new
        {
            sigla,
            nome = "Campus de Teste",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    private static object EnderecoCompleto()
    {
        return new
        {
            cep = "68507590",
            logradouro = "Folha 31, Quadra 7",
            numero = "s/n",
            bairro = "Nova Marabá",
            cidade = new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" },
            latitude = -5.368m,
            longitude = -49.118m,
            nivelResolucao = "logradouro",
            origem = "logradouro",
        };
    }

    internal static object CampusComEndereco(string sigla)
    {
        return new
        {
            sigla,
            nome = "Campus com Endereço",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = EnderecoCompleto(),
        };
    }

    private static object EnderecoCidadeIncoerente()
    {
        return new
        {
            cep = "66000000",
            cidade = new { codigoIbge = "1501402", nome = "Belém", uf = "PA" },
            nivelResolucao = "cidade",
            origem = "faixa-cidade",
        };
    }

    internal static object CampusComCidadeIncoerente()
    {
        return new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Incoerente",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = EnderecoCidadeIncoerente(),
        };
    }

    internal static object CampusComCidadeMalformada()
    {
        return new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Inválido",
            cidadeCodigoIbge = "150420", // 6 dígitos
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    private static object EnderecoCepInvalido()
    {
        return new
        {
            cep = "123", // formato inválido — não são 8 dígitos
            cidade = new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" },
            nivelResolucao = "cidade",
            origem = "faixa-cidade",
        };
    }

    internal static object CampusComCepInvalido()
    {
        return new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus CEP Inválido",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = EnderecoCepInvalido(),
        };
    }

    internal static object CampusSemSiglaENome()
    {
        return new
        {
            sigla = "",
            nome = "",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    internal static object CampusComSiglaAcentuada(string siglaAcentuada)
    {
        return new
        {
            sigla = siglaAcentuada,
            nome = "Campus com Acento",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    internal static object CampusComMarcaCombinante()
    {
        return new
        {
            sigla = "CA\u0301MAR",
            nome = "Campus com Acento Combinante",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    internal static object CampusComSiglaAcentuadaENomeVazio()
    {
        return new
        {
            sigla = "CÁMAR",
            nome = "",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    internal static object CampusParaAtualizar(string sigla)
    {
        return new
        {
            sigla,
            nome = "Campus para Atualizar",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }

    internal static object AtualizacaoComSiglaAcentuada(Guid id, string sigla)
    {
        return new
        {
            id,
            sigla = $"{sigla[..^1]}Á",
            nome = "Campus para Atualizar",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
    }
}

namespace Unifesspa.UniPlus.Kernel.Domain.Enderecos;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endereço estruturado como referência ao módulo <c>Geo</c> via CEP, com
/// display cache (snapshot) — ADR-0096. Value object compartilhado pelas
/// entidades institucionais com localização física (<c>Campus</c>,
/// <c>LocalOferta</c>, <c>Instituicao</c>), espelhando o padrão de referência
/// fraca já consolidado para a cidade (<see cref="ReferenciaCidadeGeo"/>,
/// ADR-0090): o backend <strong>não</strong> consulta o Geo e confia no
/// snapshot + <see cref="NivelResolucao"/> compostos pelo frontend a partir de
/// <c>GET /api/cep/{cep}</c>.
/// </summary>
/// <remarks>
/// <para>O <see cref="Cep"/> é a âncora de presença: um endereço só existe se há
/// CEP. Como o CEP sempre resolve ao menos à cidade, o trio de cidade do snapshot
/// (<see cref="CidadeCodigoIbge"/>/<see cref="CidadeNome"/>/<see cref="CidadeUf"/>),
/// o <see cref="NivelResolucao"/> e a <see cref="Origem"/> são obrigatórios.
/// Logradouro, número, complemento, bairro, distrito e coordenada são opcionais —
/// o modelo tolera resolução parcial (<see cref="NivelResolucao"/> raso).</para>
/// <para>A validação é só de <strong>formato</strong> (CEP 8 dígitos; trio de
/// cidade coerente via <see cref="ReferenciaCidadeGeo"/>; nível no vocabulário;
/// faixas de coordenada). A coerência entre o snapshot de cidade do endereço e a
/// referência de cidade da entidade é cross-field — fica a cargo da entidade via
/// <see cref="ValidarCoerencia"/>. Referência fraca: vale só para dado público
/// estável; nunca para invariante de autorização/elegibilidade/financeiro/legal.</para>
/// </remarks>
public sealed record ReferenciaEnderecoGeo
{
    /// <summary>Comprimento exato do CEP (8 dígitos numéricos).</summary>
    public const int CepLength = 8;

    public const int LogradouroMaxLength = 200;
    public const int NumeroMaxLength = 20;
    public const int ComplementoMaxLength = 100;
    public const int BairroMaxLength = 150;
    public const int DistritoMaxLength = 150;

    public const decimal LatitudeMin = -90m;
    public const decimal LatitudeMax = 90m;
    public const decimal LongitudeMin = -180m;
    public const decimal LongitudeMax = 180m;

    /// <summary>CEP (8 dígitos numéricos) — âncora de presença do endereço.</summary>
    public string Cep { get; }

    public string? Logradouro { get; }
    public string? Numero { get; }
    public string? Complemento { get; }
    public string? Bairro { get; }
    public string? Distrito { get; }

    /// <summary>Código IBGE (7 dígitos) da cidade resolvida pelo CEP.</summary>
    public string CidadeCodigoIbge { get; }

    /// <summary>Nome da cidade resolvida pelo CEP (display cache).</summary>
    public string CidadeNome { get; }

    /// <summary>UF da cidade resolvida pelo CEP (display cache).</summary>
    public string CidadeUf { get; }

    public decimal? Latitude { get; }
    public decimal? Longitude { get; }

    /// <summary>Até onde o CEP resolveu (<see cref="NivelResolucaoEndereco"/>).</summary>
    public string NivelResolucao { get; }

    /// <summary>Estratégia de resolução do CEP (ramo da cascata DNE).</summary>
    public string Origem { get; }

    /// <summary>Instante do carimbo server-side do display cache (frescura).</summary>
    public DateTimeOffset? DisplayAtualizadoEm { get; }

    private ReferenciaEnderecoGeo(
        string cep,
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? distrito,
        string cidadeCodigoIbge,
        string cidadeNome,
        string cidadeUf,
        decimal? latitude,
        decimal? longitude,
        string nivelResolucao,
        string origem,
        DateTimeOffset? displayAtualizadoEm)
    {
        Cep = cep;
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Distrito = distrito;
        CidadeCodigoIbge = cidadeCodigoIbge;
        CidadeNome = cidadeNome;
        CidadeUf = cidadeUf;
        Latitude = latitude;
        Longitude = longitude;
        NivelResolucao = nivelResolucao;
        Origem = origem;
        DisplayAtualizadoEm = displayAtualizadoEm;
    }

    /// <summary>
    /// Cria a referência de endereço validando formato. Retorna falha de domínio
    /// (códigos de <see cref="EnderecoReferenciaErrorCodes"/> /
    /// <see cref="CidadeReferenciaErrorCodes"/>) no primeiro problema. O
    /// <paramref name="displayAtualizadoEm"/> é carimbado server-side pelo handler.
    /// </summary>
    public static Result<ReferenciaEnderecoGeo> Criar(
        string? cep,
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? distrito,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        decimal? latitude,
        decimal? longitude,
        string? nivelResolucao,
        string? origem,
        DateTimeOffset? displayAtualizadoEm)
    {
        if (string.IsNullOrWhiteSpace(cep))
        {
            return Falha(EnderecoReferenciaErrorCodes.CepObrigatorio, "CEP do endereço é obrigatório.");
        }

        string cepNormalizado = cep.Trim();
        if (cepNormalizado.Length != CepLength || !cepNormalizado.All(char.IsAsciiDigit))
        {
            return Falha(
                EnderecoReferenciaErrorCodes.CepFormatoInvalido,
                $"CEP do endereço deve ter exatamente {CepLength} dígitos numéricos.");
        }

        Result cidade = ReferenciaCidadeGeo.Validar(cidadeCodigoIbge, cidadeNome, cidadeUf);
        if (cidade.IsFailure)
        {
            return Result<ReferenciaEnderecoGeo>.Failure(cidade.Error!);
        }

        // ReferenciaCidadeGeo.Validar garante o trio não-nulo no sucesso; os guards
        // explícitos comunicam isso ao analisador (CA1062) sem usar null-forgiving.
        if (string.IsNullOrWhiteSpace(cidadeCodigoIbge)
            || string.IsNullOrWhiteSpace(cidadeNome)
            || string.IsNullOrWhiteSpace(cidadeUf))
        {
            return Falha(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio, "Cidade do endereço é obrigatória.");
        }

        Result? tamanho = ValidarTamanhos(logradouro, numero, complemento, bairro, distrito);
        if (tamanho is { IsFailure: true } falhaTamanho)
        {
            return Result<ReferenciaEnderecoGeo>.Failure(falhaTamanho.Error!);
        }

        if (latitude is { } lat && lat is < LatitudeMin or > LatitudeMax)
        {
            return Falha(
                EnderecoReferenciaErrorCodes.LatitudeForaDeFaixa,
                $"Latitude deve estar entre {LatitudeMin} e {LatitudeMax}.");
        }

        if (longitude is { } lon && lon is < LongitudeMin or > LongitudeMax)
        {
            return Falha(
                EnderecoReferenciaErrorCodes.LongitudeForaDeFaixa,
                $"Longitude deve estar entre {LongitudeMin} e {LongitudeMax}.");
        }

        if (string.IsNullOrWhiteSpace(nivelResolucao))
        {
            return Falha(
                EnderecoReferenciaErrorCodes.NivelResolucaoObrigatorio,
                "Nível de resolução do endereço é obrigatório.");
        }

        string nivelNormalizado = nivelResolucao.Trim();
        if (!NivelResolucaoEndereco.EhValido(nivelNormalizado))
        {
            return Falha(
                EnderecoReferenciaErrorCodes.NivelResolucaoInvalido,
                "Nível de resolução inválido — use logradouro, bairro, distrito ou cidade.");
        }

        if (string.IsNullOrWhiteSpace(origem))
        {
            return Falha(
                EnderecoReferenciaErrorCodes.OrigemObrigatoria,
                "Origem da resolução do endereço é obrigatória.");
        }

        string origemNormalizada = origem.Trim();
        if (origemNormalizada.Length > ReferenciaCidadeGeo.OrigemMaxLength)
        {
            return Falha(
                EnderecoReferenciaErrorCodes.OrigemTamanho,
                $"Origem do endereço deve ter no máximo {ReferenciaCidadeGeo.OrigemMaxLength} caracteres.");
        }

        var endereco = new ReferenciaEnderecoGeo(
            cepNormalizado,
            NormalizarOpcional(logradouro),
            NormalizarOpcional(numero),
            NormalizarOpcional(complemento),
            NormalizarOpcional(bairro),
            NormalizarOpcional(distrito),
            cidadeCodigoIbge.Trim(),
            cidadeNome.Trim(),
            cidadeUf.Trim().ToUpperInvariant(),
            latitude,
            longitude,
            nivelNormalizado,
            origemNormalizada,
            displayAtualizadoEm);

        return Result<ReferenciaEnderecoGeo>.Success(endereco);
    }

    /// <summary>Predicado de validação de formato (sem carimbo) para FluentValidation.</summary>
    public static bool EhValido(
        string? cep,
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? distrito,
        string? cidadeCodigoIbge,
        string? cidadeNome,
        string? cidadeUf,
        decimal? latitude,
        decimal? longitude,
        string? nivelResolucao,
        string? origem) =>
        Criar(
            cep, logradouro, numero, complemento, bairro, distrito,
            cidadeCodigoIbge, cidadeNome, cidadeUf, latitude, longitude,
            nivelResolucao, origem, displayAtualizadoEm: null).IsSuccess;

    private static Result<ReferenciaEnderecoGeo> Falha(string code, string mensagem) =>
        Result<ReferenciaEnderecoGeo>.Failure(new DomainError(code, mensagem));

    /// <summary>
    /// Valida a coerência cidade↔CEP (CA-04): quando há snapshot de cidade no
    /// endereço e referência de cidade na entidade, ambos devem ter o mesmo
    /// código IBGE e a mesma UF. Sucesso quando qualquer lado está ausente
    /// (a obrigatoriedade da cidade quando há endereço é regra da entidade).
    /// </summary>
    public static Result ValidarCoerencia(
        string? enderecoCidadeCodigoIbge,
        string? enderecoCidadeUf,
        string? cidadeCodigoIbge,
        string? cidadeUf)
    {
        if (string.IsNullOrWhiteSpace(enderecoCidadeCodigoIbge)
            || string.IsNullOrWhiteSpace(cidadeCodigoIbge))
        {
            return Result.Success();
        }

        bool codigoCoerente = string.Equals(
            enderecoCidadeCodigoIbge.Trim(), cidadeCodigoIbge.Trim(), StringComparison.Ordinal);
        bool ufCoerente = string.Equals(
            enderecoCidadeUf?.Trim(), cidadeUf?.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!codigoCoerente || !ufCoerente)
        {
            return Result.Failure(new DomainError(
                EnderecoReferenciaErrorCodes.CidadeIncoerente,
                "A cidade do endereço (resolvida pelo CEP) deve coincidir com a referência de cidade da entidade."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Compara o conteúdo de dois endereços ignorando
    /// <see cref="DisplayAtualizadoEm"/> — usado para decidir o re-carimbo do
    /// display cache só quando o conteúdo muda (espelha a semântica da cidade).
    /// </summary>
    public bool ConteudoEquivale(ReferenciaEnderecoGeo? outro)
    {
        if (outro is null)
        {
            return false;
        }

        return string.Equals(Cep, outro.Cep, StringComparison.Ordinal)
            && string.Equals(Logradouro, outro.Logradouro, StringComparison.Ordinal)
            && string.Equals(Numero, outro.Numero, StringComparison.Ordinal)
            && string.Equals(Complemento, outro.Complemento, StringComparison.Ordinal)
            && string.Equals(Bairro, outro.Bairro, StringComparison.Ordinal)
            && string.Equals(Distrito, outro.Distrito, StringComparison.Ordinal)
            && string.Equals(CidadeCodigoIbge, outro.CidadeCodigoIbge, StringComparison.Ordinal)
            && string.Equals(CidadeNome, outro.CidadeNome, StringComparison.Ordinal)
            && string.Equals(CidadeUf, outro.CidadeUf, StringComparison.Ordinal)
            && Latitude == outro.Latitude
            && Longitude == outro.Longitude
            && string.Equals(NivelResolucao, outro.NivelResolucao, StringComparison.Ordinal)
            && string.Equals(Origem, outro.Origem, StringComparison.Ordinal);
    }

    private static Result? ValidarTamanhos(
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? distrito)
    {
        (string? valor, int max, string code)[] campos =
        [
            (logradouro, LogradouroMaxLength, EnderecoReferenciaErrorCodes.LogradouroTamanho),
            (numero, NumeroMaxLength, EnderecoReferenciaErrorCodes.NumeroTamanho),
            (complemento, ComplementoMaxLength, EnderecoReferenciaErrorCodes.ComplementoTamanho),
            (bairro, BairroMaxLength, EnderecoReferenciaErrorCodes.BairroTamanho),
            (distrito, DistritoMaxLength, EnderecoReferenciaErrorCodes.DistritoTamanho),
        ];

        foreach ((string? valor, int max, string code) in campos)
        {
            if (valor is not null && valor.Trim().Length > max)
            {
                return Result.Failure(new DomainError(
                    code, $"Campo de endereço excede o tamanho máximo de {max} caracteres."));
            }
        }

        return null;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

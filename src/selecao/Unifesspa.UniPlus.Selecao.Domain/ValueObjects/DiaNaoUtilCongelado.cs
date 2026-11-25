namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Um dia não útil copiado por valor do calendário vigente no momento da publicação
/// (UNI-REQ-0080, ADR-0061). É o que permite recontar um prazo depois de o calendário de
/// origem deixar de ser vigente — ou ser removido do cadastro.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Abrangencia"/> decide onde o dia incide, e é ela que governa quais dos demais
/// campos existem. A <c>CalendarioVigenteView</c> do módulo Configuração carrega
/// <c>MunicipioUf</c> e <c>Uf</c> em campos separados, um para cada abrangência; aqui os dois
/// colapsam em <see cref="Uf"/>, porque nenhum dia tem as duas ao mesmo tempo e manter dois
/// campos mutuamente exclusivos convidaria a preencher o errado.
/// </para>
/// <para>
/// A coerência entre <see cref="MunicipioIbge"/> e <see cref="Uf"/> não é conferida aqui de
/// forma própria: passa por <see cref="ReferenciaCidadeGeo"/>, o mesmo caminho que
/// <c>Campus</c>, <c>LocalOferta</c> e <see cref="LocalidadeRegente"/> já usam. Uma segunda
/// tabela de prefixos de UF divergiria da primeira no dia em que uma delas fosse atualizada.
/// </para>
/// </remarks>
public sealed record DiaNaoUtilCongelado
{
    private DiaNaoUtilCongelado(
        DateOnly data,
        AbrangenciaDoDiaNaoUtil abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? uf)
    {
        Data = data;
        Abrangencia = abrangencia;
        MunicipioIbge = municipioIbge;
        MunicipioNome = municipioNome;
        Uf = uf;
    }

    /// <summary>A data não útil.</summary>
    public DateOnly Data { get; }

    /// <summary>Token canônico da abrangência — um valor de <see cref="AbrangenciaDoDiaNaoUtil"/>.</summary>
    public AbrangenciaDoDiaNaoUtil Abrangencia { get; }

    /// <summary>Código IBGE de sete dígitos, presente apenas quando a abrangência é municipal.</summary>
    public string? MunicipioIbge { get; }

    /// <summary>Nome do município, presente apenas quando a abrangência é municipal.</summary>
    public string? MunicipioNome { get; }

    /// <summary>Sigla da UF, presente nas abrangências municipal e estadual, ausente nas demais.</summary>
    public string? Uf { get; }

    /// <summary>
    /// Cria o dia congelado validando a combinação territorial contra a abrangência declarada.
    /// </summary>
    /// <remarks>
    /// As quatro abrangências têm formas distintas e todas são recusadas quando não fecham:
    /// nacional e institucional não admitem município nem UF, porque incidem em todo lugar;
    /// estadual exige a UF e proíbe município; municipal exige o trio completo, com o prefixo
    /// do código IBGE coerente com a UF.
    /// </remarks>
    public static Result<DiaNaoUtilCongelado> Criar(
        DateOnly data,
        string? abrangencia,
        string? municipioIbge,
        string? municipioNome,
        string? uf)
    {
        if (data == default)
        {
            return Falha(
                DiaNaoUtilCongeladoErrorCodes.DataAusente,
                "A data do dia não útil não pode ser o valor default (0001-01-01) — é assim que uma data omitida se materializa.");
        }

        if (!CatalogoDe<AbrangenciaDoDiaNaoUtil>.TentarObter(abrangencia, out AbrangenciaDoDiaNaoUtil valor))
        {
            return Falha(DiaNaoUtilCongeladoErrorCodes.AbrangenciaInvalida, "A abrangência não pertence ao calendário de dias úteis.");
        }

        return CriarComAbrangencia(data, valor, municipioIbge, municipioNome, uf);
    }

    private static Result<DiaNaoUtilCongelado> CriarComAbrangencia(
        DateOnly data, AbrangenciaDoDiaNaoUtil valor, string? municipioIbge, string? municipioNome, string? uf)
    {
        string? ibge = Vazio(municipioIbge) ? null : municipioIbge?.Trim();
        string? nome = Vazio(municipioNome) ? null : municipioNome?.Trim();
        string? siglaUf = Vazio(uf) ? null : uf?.Trim();

        return valor switch
        {
            AbrangenciaDoDiaNaoUtil.Municipal => CriarMunicipal(data, ibge, nome, siglaUf),
            AbrangenciaDoDiaNaoUtil.Estadual => CriarEstadual(data, ibge, nome, siglaUf),
            _ => CriarSemTerritorio(data, valor, ibge, nome, siglaUf),
        };
    }

    private static Result<DiaNaoUtilCongelado> CriarMunicipal(
        DateOnly data, string? ibge, string? nome, string? uf)
    {
        // ReferenciaCidadeGeo nomeia cada causa (código ausente, formato inválido, nome vazio,
        // UF incoerente com o prefixo) e é a MESMA verificação do cadastro de cidade. Repetir
        // as regras aqui criaria uma segunda verdade sobre o que é um município válido.
        Result trio = ReferenciaCidadeGeo.Validar(ibge, nome, uf);
        if (trio.IsFailure)
        {
            return Result<DiaNaoUtilCongelado>.Failure(trio.Error!);
        }

        return Result<DiaNaoUtilCongelado>.Success(
            new DiaNaoUtilCongelado(data, AbrangenciaDoDiaNaoUtil.Municipal, ibge, nome, uf!.ToUpperInvariant()));
    }

    private static Result<DiaNaoUtilCongelado> CriarEstadual(
        DateOnly data, string? ibge, string? nome, string? uf)
    {
        if (ibge is not null || nome is not null)
        {
            return Falha(
                DiaNaoUtilCongeladoErrorCodes.MunicipioEmDiaEstadual,
                "Dia de abrangência estadual não carrega município — a UF sozinha determina onde ele incide.");
        }

        if (uf is null)
        {
            return Falha(DiaNaoUtilCongeladoErrorCodes.UfAusenteEmDiaEstadual, "Dia de abrangência estadual exige a UF.");
        }

        return CriarComUfEstadual(data, uf);
    }

    private static Result<DiaNaoUtilCongelado> CriarComUfEstadual(DateOnly data, string uf)
    {
        string normalizada = uf.ToUpperInvariant();
        if (!ReferenciaCidadeGeo.EhUfValida(normalizada))
        {
            return Falha(DiaNaoUtilCongeladoErrorCodes.UfInvalida, $"UF '{uf}' não é uma unidade federativa reconhecida.");
        }

        return Result<DiaNaoUtilCongelado>.Success(
            new DiaNaoUtilCongelado(data, AbrangenciaDoDiaNaoUtil.Estadual, null, null, normalizada));
    }

    private static Result<DiaNaoUtilCongelado> CriarSemTerritorio(
        DateOnly data, AbrangenciaDoDiaNaoUtil abrangencia, string? ibge, string? nome, string? uf)
    {
        if (ibge is not null || nome is not null || uf is not null)
        {
            return Falha(
                DiaNaoUtilCongeladoErrorCodes.TerritorioEmDiaSemRecorte,
                $"Dia de abrangência {abrangencia.Rotulo()} incide em todo lugar e não carrega município nem UF.");
        }

        return Result<DiaNaoUtilCongelado>.Success(
            new DiaNaoUtilCongelado(data, abrangencia, null, null, null));
    }

    private static bool Vazio(string? valor) => string.IsNullOrWhiteSpace(valor);

    private static Result<DiaNaoUtilCongelado> Falha(string code, string mensagem) =>
        Result<DiaNaoUtilCongelado>.Failure(new DomainError(code, mensagem));
}

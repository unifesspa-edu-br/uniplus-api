namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Traduz a leitura do calendário vigente do módulo Configuração para o snapshot por valor que
/// a raiz consome e o envelope congela (ADR-0061).
/// </summary>
/// <remarks>
/// <para>
/// <b>Uma leitura por operação.</b> O chamador lê o reader <b>uma vez</b> e passa a resposta
/// adiante: a mesma serve ao gate e ao congelamento. Duas leituras abririam a janela em que o
/// calendário muda entre validar e congelar, e a versão publicada carregaria um calendário que o
/// gate não aprovou.
/// </para>
/// <para>
/// <b>Onde a tradução mora.</b> Na Application, e não no Domain, porque é aqui que o contrato
/// do outro módulo é conhecido — <c>CalendarioVigenteView</c> vive em
/// <c>Configuracao.Contracts</c>, e o Domain de Seleção não o referencia. O que atravessa a
/// fronteira do agregado é o valor já traduzido.
/// </para>
/// <para>
/// <b>Sobre a recusa.</b> Os dias vêm de um calendário que o cadastro de Configuração já validou
/// na criação — abrangência, coerência territorial, duplicata e versão têm recusa nomeada lá,
/// e um calendário vigente incoerente não é estado alcançável pelo fluxo público. A tradução
/// revalida assim mesmo, como defesa em profundidade: o VO é o mesmo que o decoder do envelope
/// reconstrói, e afrouxar aqui abriria caminho para congelar o que a decodificação recusaria.
/// A falha, se acontecer, é defeito de dado — não tem código público próprio e sobe como erro
/// interno, que é onde bug deve cair.
/// </para>
/// </remarks>
public static class LeituraDoCalendarioVigente
{
    /// <summary>
    /// Converte a view lida do módulo Configuração, ou devolve <see langword="null"/> quando
    /// não há calendário vigente — ausência não é falha aqui: quem decide se ela impede a operação
    /// é o gate da raiz, que só a recusa quando o certame tem contagem sobre dia útil.
    /// </summary>
    public static Result<CalendarioDiasUteisCongelado?> Traduzir(CalendarioVigenteView? vigente)
    {
        if (vigente is null)
        {
            return Result<CalendarioDiasUteisCongelado?>.Success(null);
        }

        Result<IReadOnlyList<DiaNaoUtilCongelado>> dias = TraduzirDias(vigente.DiasNaoUteis);
        if (dias.IsFailure)
        {
            return Result<CalendarioDiasUteisCongelado?>.Failure(dias.Error!);
        }

        return CriarCalendario(vigente, dias.Value!);
    }

    private static Result<CalendarioDiasUteisCongelado?> CriarCalendario(CalendarioVigenteView vigente, IReadOnlyList<DiaNaoUtilCongelado> dias)
    {
        Result<CalendarioDiasUteisCongelado> calendario = CalendarioDiasUteisCongelado.Criar(vigente.Id, vigente.VersaoDoCalendario, dias);
        return calendario.IsFailure
            ? Result<CalendarioDiasUteisCongelado?>.Failure(calendario.Error!)
            : Result<CalendarioDiasUteisCongelado?>.Success(calendario.Value);
    }

    private static Result<IReadOnlyList<DiaNaoUtilCongelado>> TraduzirDias(IEnumerable<DiaNaoUtilView> origem)
    {
        List<DiaNaoUtilCongelado> dias = [];
        foreach (DiaNaoUtilView dia in origem)
        {
            Result<DiaNaoUtilCongelado> congelado = TraduzirDia(dia);
            if (congelado.IsFailure)
            {
                return Result<IReadOnlyList<DiaNaoUtilCongelado>>.Failure(congelado.Error!);
            }

            dias.Add(congelado.Value!);
        }

        return Result<IReadOnlyList<DiaNaoUtilCongelado>>.Success(dias);
    }

    private static Result<DiaNaoUtilCongelado> TraduzirDia(DiaNaoUtilView dia)
    {
        Result forma = ConferirFormaBruta(dia);
        if (forma.IsFailure)
        {
            return Result<DiaNaoUtilCongelado>.Failure(forma.Error!);
        }

        string? uf = dia.Abrangencia switch
        {
            "MUNICIPAL" => dia.MunicipioUf,
            "ESTADUAL" => dia.Uf,
            _ => null,
        };
        return DiaNaoUtilCongelado.Criar(dia.Data, dia.Abrangencia, dia.MunicipioIbge, dia.MunicipioNome, uf);
    }

    /// <summary>
    /// Confere que o dia lido traz exatamente os campos territoriais que a sua abrangência
    /// admite, antes de a UF colapsar num campo só.
    /// </summary>
    /// <remarks>
    /// Cada uma destas combinações tem recusa nomeada no cadastro de origem, e um calendário
    /// vigente que as viole não é estado alcançável pelo fluxo público. A conferência existe
    /// porque o descarte silencioso seria pior que a falha: o campo proibido sumiria sem
    /// registro, e a versão publicada afirmaria um território que o cadastro não declarou.
    /// </remarks>
    private static Result ConferirFormaBruta(DiaNaoUtilView dia)
    {
        bool temMunicipio = TemMunicipio(dia);
        bool temUfEstadual = !string.IsNullOrWhiteSpace(dia.Uf);

        return dia.Abrangencia switch
        {
            "MUNICIPAL" when temUfEstadual => Recusa(
                dia, "dia municipal não carrega UF estadual — a UF vem do município"),
            "ESTADUAL" when temMunicipio => Recusa(
                dia, "dia estadual não carrega município"),
            "NACIONAL" or "INSTITUCIONAL"
                when temMunicipio || temUfEstadual => Recusa(
                    dia, $"dia de abrangência {dia.Abrangencia} incide em todo lugar e não carrega recorte territorial"),
            _ => Result.Success(),
        };
    }

    private static bool TemMunicipio(DiaNaoUtilView dia) =>
        !string.IsNullOrWhiteSpace(dia.MunicipioIbge)
        || !string.IsNullOrWhiteSpace(dia.MunicipioNome)
        || !string.IsNullOrWhiteSpace(dia.MunicipioUf);

    private static Result Recusa(DiaNaoUtilView dia, string motivo) =>
        Result.Failure(new DomainError(
            "CalendarioVigente.FormaTerritorialInvalida",
            $"O calendário vigente traz {dia.Data:yyyy-MM-dd} em forma incoerente: {motivo}."));
}

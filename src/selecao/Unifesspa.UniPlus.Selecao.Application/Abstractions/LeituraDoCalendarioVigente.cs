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
/// dataset muda entre validar e congelar, e a versão publicada carregaria um calendário que o
/// gate não aprovou.
/// </para>
/// <para>
/// <b>Onde a tradução mora.</b> Na Application, e não no Domain, porque é aqui que o contrato
/// do outro módulo é conhecido — <c>CalendarioVigenteView</c> vive em
/// <c>Configuracao.Contracts</c>, e o Domain de Seleção não o referencia. O que atravessa a
/// fronteira do agregado é o valor já traduzido.
/// </para>
/// <para>
/// <b>Sobre a recusa.</b> Os dias vêm de um dataset que o cadastro de Configuração já validou
/// na criação — abrangência, coerência territorial, duplicata e versão têm recusa nomeada lá,
/// e um dataset vigente incoerente não é estado alcançável pelo fluxo público. A tradução
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
    /// não há dataset vigente — ausência não é falha aqui: quem decide se ela impede a operação
    /// é o gate da raiz, que só a recusa quando o certame tem contagem sobre dia útil.
    /// </summary>
    public static Result<CalendarioDiasUteisCongelado?> Traduzir(CalendarioVigenteView? vigente)
    {
        if (vigente is null)
        {
            return Result<CalendarioDiasUteisCongelado?>.Success(null);
        }

        List<DiaNaoUtilCongelado> dias = [];
        foreach (DiaNaoUtilView dia in vigente.DiasNaoUteis)
        {
            // A forma bruta é conferida ANTES do colapso. Escolher a UF pela abrangência e
            // seguir adiante descartaria em silêncio um campo que a abrangência proíbe — um dia
            // municipal que também trouxesse Uf preenchida seria congelado como se ela não
            // existisse, e a contradição do dado de origem desapareceria dentro de um artefato
            // imutável. Depois do colapso, o value object não teria como saber que havia um
            // segundo campo.
            Result forma = ConferirFormaBruta(dia);
            if (forma.IsFailure)
            {
                return Result<CalendarioDiasUteisCongelado?>.Failure(forma.Error!);
            }

            // A view separa MunicipioUf e Uf, um por abrangência; o snapshot tem um campo só.
            string? uf = dia.Abrangencia switch
            {
                AbrangenciaDiaNaoUtil.Municipal => dia.MunicipioUf,
                AbrangenciaDiaNaoUtil.Estadual => dia.Uf,
                _ => null,
            };

            Result<DiaNaoUtilCongelado> congelado = DiaNaoUtilCongelado.Criar(
                dia.Data, dia.Abrangencia, dia.MunicipioIbge, dia.MunicipioNome, uf);
            if (congelado.IsFailure)
            {
                return Result<CalendarioDiasUteisCongelado?>.Failure(congelado.Error!);
            }

            dias.Add(congelado.Value!);
        }

        Result<CalendarioDiasUteisCongelado> calendario =
            CalendarioDiasUteisCongelado.Criar(vigente.Id, vigente.VersaoDataset, dias);

        return calendario.IsFailure
            ? Result<CalendarioDiasUteisCongelado?>.Failure(calendario.Error!)
            : Result<CalendarioDiasUteisCongelado?>.Success(calendario.Value);
    }

    /// <summary>
    /// Confere que o dia lido traz exatamente os campos territoriais que a sua abrangência
    /// admite, antes de a UF colapsar num campo só.
    /// </summary>
    /// <remarks>
    /// Cada uma destas combinações tem recusa nomeada no cadastro de origem, e um dataset
    /// vigente que as viole não é estado alcançável pelo fluxo público. A conferência existe
    /// porque o descarte silencioso seria pior que a falha: o campo proibido sumiria sem
    /// registro, e a versão publicada afirmaria um território que o cadastro não declarou.
    /// </remarks>
    private static Result ConferirFormaBruta(DiaNaoUtilView dia)
    {
        bool temMunicipio = !string.IsNullOrWhiteSpace(dia.MunicipioIbge)
            || !string.IsNullOrWhiteSpace(dia.MunicipioNome)
            || !string.IsNullOrWhiteSpace(dia.MunicipioUf);
        bool temUfEstadual = !string.IsNullOrWhiteSpace(dia.Uf);

        return dia.Abrangencia switch
        {
            AbrangenciaDiaNaoUtil.Municipal when temUfEstadual => Recusa(
                dia, "dia municipal não carrega UF estadual — a UF vem do município"),
            AbrangenciaDiaNaoUtil.Estadual when temMunicipio => Recusa(
                dia, "dia estadual não carrega município"),
            AbrangenciaDiaNaoUtil.Nacional or AbrangenciaDiaNaoUtil.Institucional
                when temMunicipio || temUfEstadual => Recusa(
                    dia, $"dia de abrangência {dia.Abrangencia} incide em todo lugar e não carrega recorte territorial"),
            _ => Result.Success(),
        };
    }

    private static Result Recusa(DiaNaoUtilView dia, string motivo) =>
        Result.Failure(new DomainError(
            "CalendarioVigente.FormaTerritorialInvalida",
            $"O calendário vigente traz {dia.Data:yyyy-MM-dd} em forma incoerente: {motivo}."));
}

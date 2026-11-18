namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>Os blocos da configuração reconstruídos a partir de uma versão congelada.</summary>
/// <remarks>
/// As entidades dos blocos são mutáveis; a comparação da configuração se faz pelo conteúdo congelado,
/// sem prometer igualdade de valor para as listas e as entidades que ela contém.
/// </remarks>
public sealed class ConfiguracaoPublicada
{
    public ConfiguracaoPublicada(
        CronogramaDeclarado cronograma, AvaliacaoDeclarada avaliacao, VagasDeclaradas vagas,
        ExigenciasDeclaradas exigencias, ColetaDeclarada coleta, DivulgacaoETaxaDeclaradas divulgacaoETaxa,
        IdentificadorLegivel? identificadorLegivel)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(vagas);
        ArgumentNullException.ThrowIfNull(exigencias);
        ArgumentNullException.ThrowIfNull(coleta);
        ArgumentNullException.ThrowIfNull(divulgacaoETaxa);
        Cronograma = cronograma;
        Avaliacao = avaliacao;
        Vagas = vagas;
        Exigencias = exigencias;
        Coleta = coleta;
        DivulgacaoETaxa = divulgacaoETaxa;
        IdentificadorLegivel = identificadorLegivel;
    }

    public CronogramaDeclarado Cronograma { get; }
    public AvaliacaoDeclarada Avaliacao { get; }
    public VagasDeclaradas Vagas { get; }
    public ExigenciasDeclaradas Exigencias { get; }
    public ColetaDeclarada Coleta { get; }
    public DivulgacaoETaxaDeclaradas DivulgacaoETaxa { get; }
    public IdentificadorLegivel? IdentificadorLegivel { get; }

    /// <summary>As derivações configuradas e as que a união das ofertas escolhidas define.</summary>
    public IReadOnlyList<ConfiguracaoDerivacaoFato> DerivacoesEfetivas(IReadOnlyCollection<Guid>? ofertasDeCurso = null) =>
        DerivacoesDaOferta.Efetivas(Coleta.RegrasDerivacao, DerivacoesDaOferta.DistribuicaoDasOfertas(Vagas.DistribuicaoVagas, ofertasDeCurso ?? []));
}

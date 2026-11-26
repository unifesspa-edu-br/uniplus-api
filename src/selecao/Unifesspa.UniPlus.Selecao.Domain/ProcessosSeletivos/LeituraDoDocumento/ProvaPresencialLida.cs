namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial que a leitura do documento entregou como declaração, com o limite de municípios que a conferiu, e a origem que
/// guarda a recusa da forma. A conferência é a da publicação (<see cref="ConferenciaDaProvaPresencial"/>): o bloco ausente é o
/// processo por declarar, e o bloco recusado fica sem declaração e só devolve as causas da leitura, porque o que a leitura recusou
/// não é declaração por fazer nem vai ao Geo.
/// </summary>
public sealed record ProvaPresencialLida(DeclaracaoDaProvaPresencial? Declaracao, OrigemNoDocumento Origem, int LimiteDeMunicipios)
{
    /// <summary>Confere o bloco do documento pelas regras de forma da declaração e guarda a recusa, com o ponteiro pela posição enviada.</summary>
    public static ProvaPresencialLida Ler(ProvaPresencialNoDocumento? bloco, int limiteDeMunicipios)
    {
        if (bloco is null)
        {
            return new(null, OrigemNoDocumento.Identidade, limiteDeMunicipios);
        }

        Result<DeclaracaoDaProvaPresencial> declaracao =
            DeclaracaoDaProvaPresencial.Declarar(bloco.TemProvaPresencial, bloco.Municipios, limiteDeMunicipios);

        return declaracao.IsSuccess
            ? new(declaracao.Value, OrigemNoDocumento.Identidade, limiteDeMunicipios)
            : new(
                null,
                OrigemNoDocumento.SoComPartesRecusadas([.. declaracao.Errors.Select(static causa => ParteRecusada.DoBloco(DeclaracaoDaProvaPresencial.Campo, causa))]),
                limiteDeMunicipios);
    }

    /// <summary>As causas que a conferência da prova presencial aponta no documento: as da leitura, se a recusou, ou as da conferência com o Geo.</summary>
    public IReadOnlyList<FieldError> Causas(MunicipiosConferidosNoGeo conferidos)
    {
        ArgumentNullException.ThrowIfNull(conferidos);

        return Origem.PartesRecusadas.Count > 0
            ? [.. Origem.PartesRecusadas.Select(static parte => parte.Causa)]
            : PonteiroNoDocumento.Converter(ConferenciaDaProvaPresencial.Conferir(Declaracao, conferidos, LimiteDeMunicipios), Origem);
    }
}

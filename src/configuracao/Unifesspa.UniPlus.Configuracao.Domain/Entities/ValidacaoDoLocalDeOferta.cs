namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere, sem I/O, a declaração de um local de oferta e devolve os campos já normalizados, ou todas as recusas de uma
/// vez (ADR-0125), cada uma rotulada pelo campo declarado. A coerência entre a cidade e o endereço só é conferida quando a
/// referência de cidade é válida: do contrário a causa raiz (formato) ficaria mascarada por "incoerente".
/// </summary>
internal sealed class ValidacaoDoLocalDeOferta(DeclaracaoDoLocalDeOferta declaracao)
{
    private const int CodigoEmecMaxLength = 20;

    private readonly List<FieldError> _erros = [];

    public Result<CamposDoLocalDeOferta> Resolver()
    {
        ConferirTipo();
        ConferirCidadeEEndereco();
        ConferirCodigoEmec();

        return _erros.Count > 0
            ? Result<CamposDoLocalDeOferta>.ValidationFailure(_erros)
            : Result<CamposDoLocalDeOferta>.Success(Montar());
    }

    private CamposDoLocalDeOferta Montar() =>
        new(
            declaracao.Tipo,
            declaracao.CampusResponsavelId,
            (declaracao.CidadeCodigoIbge ?? string.Empty).Trim(),
            (declaracao.CidadeNome ?? string.Empty).Trim(),
            (declaracao.CidadeUf ?? string.Empty).Trim().ToUpperInvariant(),
            declaracao.Endereco,
            string.IsNullOrWhiteSpace(declaracao.CodigoEmec) ? null : declaracao.CodigoEmec.Trim());

    private void ConferirTipo()
    {
        if (!Enum.IsDefined(declaracao.Tipo) || declaracao.Tipo == TipoLocalOferta.Nenhum)
        {
            _erros.Add(new("tipo", new DomainError(
                LocalOfertaErrorCodes.TipoInvalido,
                "Tipo de Local de Oferta inválido — use um valor definido em TipoLocalOferta, diferente de Nenhum.")));
        }
    }

    private void ConferirCidadeEEndereco()
    {
        Result cidade = ReferenciaCidadeGeo.Validar(declaracao.CidadeCodigoIbge, declaracao.CidadeNome, declaracao.CidadeUf);
        if (cidade.IsSuccess)
        {
            ConferirCoerenciaDoEndereco();
            return;
        }

        foreach (FieldError erroCidade in cidade.Errors)
        {
            _erros.Add(new(CampoDaCidade(erroCidade.Error.Code), erroCidade.Error));
        }
    }

    private void ConferirCoerenciaDoEndereco()
    {
        ReferenciaEnderecoGeo? endereco = declaracao.Endereco;
        Result coerencia = ReferenciaEnderecoGeo.ValidarCoerencia(
            endereco?.CidadeCodigoIbge, endereco?.CidadeUf, declaracao.CidadeCodigoIbge, declaracao.CidadeUf);
        if (coerencia.IsFailure)
        {
            _erros.Add(new("endereco", coerencia.Error!));
        }
    }

    private void ConferirCodigoEmec()
    {
        if (declaracao.CodigoEmec is not null && declaracao.CodigoEmec.Trim().Length > CodigoEmecMaxLength)
        {
            _erros.Add(new("codigoEmec", new DomainError(
                LocalOfertaErrorCodes.CodigoEmecTamanho,
                $"Código e-MEC do Local de Oferta deve ter no máximo {CodigoEmecMaxLength} caracteres.")));
        }
    }

    // O código de erro da referência de cidade diz qual campo do payload (camelCase, ADR-0023) causou a recusa; sem isso
    // todo erro de cidade seria rotulado com o código IBGE, mesmo quando a causa é o nome ou a UF.
    private static string CampoDaCidade(string codigoErro) => codigoErro switch
    {
        CidadeReferenciaErrorCodes.NomeObrigatorio
            or CidadeReferenciaErrorCodes.NomeCaractereInvalido
            or CidadeReferenciaErrorCodes.NomeTamanho => "cidadeNome",
        CidadeReferenciaErrorCodes.UfObrigatoria
            or CidadeReferenciaErrorCodes.UfIncoerente => "cidadeUf",
        _ => "cidadeCodigoIbge",
    };
}

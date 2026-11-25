namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Cadastro de fato declarado pelo administrador (ADR-0136). Os eixos chegam como token canônico
/// UPPER_SNAKE; o vínculo ao campo de formulário é gerado a partir do código.
/// </summary>
public sealed record CriarFatoCandidatoCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    [property: AceitaCodigosDe<TipoDoDado>] string Dominio,
    [property: AceitaCodigosDe<QuantidadeDeValores>] string Cardinalidade,
    [property: AceitaCodigosDe<FonteValoresFato>] string? FonteValores,
    [property: AceitaCodigosDe<FormatoTexto>] string? Formato,
    string PontoResolucao,
    [property: AceitaCodigosDe<EscopoFato>] string Escopo,
    [property: AceitaCodigosDe<ClassificacaoDeProtecao>] string ClassificacaoProtecao,
    string FinalidadeTratamento,
    [property: AceitaCodigosDe<HipoteseLegalTratamento>] string HipoteseLegal,
    [property: AceitaCodigosDe<ComponenteDeExibicao>] string? ComponenteExibicao = null) : ICommand<Result<Guid>>;

namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Cadastro de fato derivado por regra pelo administrador (ADR-0136): booleano ou categórico, com o
/// vínculo à regra de derivação gerado a partir do código. As regras padrão vêm depois, por
/// <see cref="DefinirRegrasPadraoCommand"/>.
/// </summary>
public sealed record CriarFatoDerivadoCommand(
    string Codigo,
    string Nome,
    string? Descricao,
    [property: AceitaCodigosDe<TipoDoDado>] string Dominio,
    string PontoResolucao,
    [property: AceitaCodigosDe<EscopoFato>] string Escopo,
    [property: AceitaCodigosDe<ClassificacaoDeProtecao>] string ClassificacaoProtecao,
    string FinalidadeTratamento,
    [property: AceitaCodigosDe<HipoteseLegalTratamento>] string HipoteseLegal,
    [property: AceitaCodigosDe<ComponenteDeExibicao>] string? ComponenteExibicao = null) : ICommand<Result<Guid>>;

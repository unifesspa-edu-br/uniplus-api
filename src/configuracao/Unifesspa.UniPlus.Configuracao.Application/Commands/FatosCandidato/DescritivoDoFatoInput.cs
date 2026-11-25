namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Corpo da edição de um fato: o nome, a descrição e o componente de exibição, que são a apresentação do fato e
/// os campos editáveis também no fato de sistema. Sem componente, o fato volta ao padrão.
/// </summary>
public sealed record DescritivoDoFatoInput(string Nome, string? Descricao, [property: AceitaCodigosDe<ComponenteDeExibicao>] string? ComponenteExibicao = null);

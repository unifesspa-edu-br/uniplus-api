namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os blocos do envelope já lidos, antes da conferência de coerência entre eles. Só existe quando todos
/// foram lidos: o compilador recusa esquecer um deles.
/// </summary>
/// <param name="Configuracao">O edital, as etapas, as vagas, a classificação e o cronograma.</param>
/// <param name="Documentos">Os documentos exigidos, os formulários e os metadados dos fatos.</param>
/// <param name="Publicacao">A divulgação, a taxa, a localidade, o calendário e o identificador legível.</param>
/// <param name="Retificacao">O bloco de retificação, quando o envelope o carrega.</param>
/// <param name="Raizes">As raízes da árvore de satisfação das exigências.</param>
/// <param name="Coleta">Os fatos coletados, os grupos e a derivação.</param>
internal sealed record BlocosDoEnvelope(
    BlocosDaConfiguracao Configuracao,
    BlocosDosDocumentos Documentos,
    BlocosDaPublicacao Publicacao,
    RetificacaoInfo? Retificacao,
    IReadOnlyList<NoExigencia> Raizes,
    BlocosDeColeta Coleta);

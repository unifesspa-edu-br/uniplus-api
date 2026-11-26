namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>O código é nulo quando o edital não prevê a manifestação de interesse; o campo vem sempre no corpo.</summary>
public sealed record DefinirManifestacaoDeInteresseRequest(
    [property: JsonRequired, AceitaCodigosDe<ManifestacaoDeInteresse>] string? ManifestacaoDeInteresse);

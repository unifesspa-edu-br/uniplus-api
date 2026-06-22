namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Snapshot de cidade do endereço no payload de entrada (ADR-0096): trio
/// composto pelo front a partir da resolução do CEP. Mantido byte-equivalente à
/// cópia do módulo Configuração (ADR-0035).
/// </summary>
public sealed record CidadeReferenciaInput(string? CodigoIbge, string? Nome, string? Uf);

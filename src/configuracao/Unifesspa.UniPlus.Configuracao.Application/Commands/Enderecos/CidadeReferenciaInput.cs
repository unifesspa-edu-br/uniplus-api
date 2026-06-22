namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;

/// <summary>
/// Snapshot de cidade do endereço no payload de entrada (ADR-0096): trio
/// composto pelo front a partir da resolução do CEP. A proveniência/frescura
/// não viaja — é carimbada server-side.
/// </summary>
public sealed record CidadeReferenciaInput(string? CodigoIbge, string? Nome, string? Uf);

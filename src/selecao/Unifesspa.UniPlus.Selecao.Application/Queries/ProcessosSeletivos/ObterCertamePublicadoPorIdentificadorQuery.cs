namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve o certame publicado pelo identificador legível congelado na publicação — o endereço
/// público do certame. Mesma leitura e mesmas recusas de <see cref="ObterCertamePublicadoQuery"/>.
/// </summary>
/// <remarks>
/// Um identificador fora do formato do cadastro recebe a mesma recusa de um certame que não existe:
/// nenhum certame público pode tê-lo, e distinguir o formato inválido do certame ausente não ajuda o
/// chamador anônimo a nada além de sondar a regra.
/// </remarks>
public sealed record ObterCertamePublicadoPorIdentificadorQuery(
    string IdentificadorLegivel) : IQuery<Result<CertamePublicadoDto>>;
